"""
Minimal, dependency-free OpenClaw Gateway client.

Why hand-rolled: the cloud box has no `websockets` / `websocket-client`, and
pip-installing anything onto it is a change we do not own. RFC 6455 needs about
80 lines, so we just implement it. Pure stdlib: socket + ssl + base64 + json.

Protocol (read out of the deployed Control UI bundle, not guessed):
  transport : WebSocket on the same host/port/path as the control UI
  request   : {"type":"req","id":<str>,"method":<str>,"params":{...}}
  response  : {"type":"res","id":<same>,"ok":true,"result":{...}}
              {"type":"res","id":<same>,"ok":false,"error":{...}}
  event     : {"type":"event","event":<str>,"payload":{...},"seq":<int>}
  connect   : method "connect", params = buildConnectParams() from
              assets/gateway-*.js -- minProtocol/maxProtocol 4, role "operator",
              scopes operator.*, caps ["tool-events"], auth {token|password}

Usage:
    from openclaw_ws import Gateway
    with Gateway("886841a9.openclaw.runware.run", token=TOKEN) as gw:
        print(gw.hello)
        print(gw.call("system.info", {}))
"""
import base64
import json
import os
import socket
import ssl
import struct
import time
import uuid

DEFAULT_HOST = "886841a9.openclaw.runware.run"
DEFAULT_PATH = "/"

OP_CONT, OP_TEXT, OP_BIN = 0x0, 0x1, 0x2
OP_CLOSE, OP_PING, OP_PONG = 0x8, 0x9, 0xA

SCOPES = ["operator.admin", "operator.read", "operator.write",
          "operator.approvals", "operator.pairing"]


class GatewayError(RuntimeError):
    pass


class Gateway:
    def __init__(self, host=DEFAULT_HOST, path=DEFAULT_PATH, token=None,
                 password=None, port=443, timeout=60.0, client_name="ops-cloud",
                 client_version="agentF/1.0", platform="python", mode="operator"):
        self.host = host
        self.path = path or "/"
        self.token = (token or "").strip() or None
        self.password = (password or "").strip() or None
        self.port = port
        self.timeout = timeout
        self.client_name = client_name
        self.client_version = client_version
        self.platform = platform
        self.mode = mode
        self.sock = None
        self.hello = None
        self.events = []
        self._buf = b""
        self._id = 0

    # ---------- low level socket ----------
    def _open(self):
        raw = socket.create_connection((self.host, self.port), timeout=self.timeout)
        raw.settimeout(self.timeout)
        ctx = ssl.create_default_context()
        self.sock = ctx.wrap_socket(raw, server_hostname=self.host)

    def _read_http_headers(self):
        data = b""
        while b"\r\n\r\n" not in data:
            chunk = self.sock.recv(4096)
            if not chunk:
                raise GatewayError("socket closed during handshake")
            data += chunk
            if len(data) > 65536:
                raise GatewayError("handshake response too large")
        head, _, rest = data.partition(b"\r\n\r\n")
        self._buf = rest
        return head.decode("latin-1")

    def _handshake(self):
        key = base64.b64encode(os.urandom(16)).decode()
        req = (
            f"GET {self.path} HTTP/1.1\r\n"
            f"Host: {self.host}\r\n"
            "Upgrade: websocket\r\n"
            "Connection: Upgrade\r\n"
            f"Sec-WebSocket-Key: {key}\r\n"
            "Sec-WebSocket-Version: 13\r\n"
            "Origin: https://%s\r\n" % self.host +
            "User-Agent: ops-cloud-agentF/1.0\r\n"
            "\r\n"
        )
        self.sock.sendall(req.encode())
        head = self._read_http_headers()
        status = head.split("\r\n", 1)[0]
        if "101" not in status:
            raise GatewayError(f"upgrade refused: {status}")
        return head

    # ---------- framing (RFC 6455) ----------
    def _recv_exact(self, n):
        while len(self._buf) < n:
            chunk = self.sock.recv(65536)
            if not chunk:
                raise GatewayError("connection closed by peer")
            self._buf += chunk
        out, self._buf = self._buf[:n], self._buf[n:]
        return out

    def send_text(self, text):
        payload = text.encode("utf-8")
        mask = os.urandom(4)
        n = len(payload)
        hdr = bytearray([0x80 | OP_TEXT])
        if n < 126:
            hdr.append(0x80 | n)
        elif n < 65536:
            hdr.append(0x80 | 126)
            hdr += struct.pack(">H", n)
        else:
            hdr.append(0x80 | 127)
            hdr += struct.pack(">Q", n)
        hdr += mask
        masked = bytes(b ^ mask[i % 4] for i, b in enumerate(payload))
        self.sock.sendall(bytes(hdr) + masked)

    def _read_frame(self):
        b0, b1 = self._recv_exact(2)
        fin = bool(b0 & 0x80)
        opcode = b0 & 0x0F
        masked = bool(b1 & 0x80)
        ln = b1 & 0x7F
        if ln == 126:
            ln = struct.unpack(">H", self._recv_exact(2))[0]
        elif ln == 127:
            ln = struct.unpack(">Q", self._recv_exact(8))[0]
        mkey = self._recv_exact(4) if masked else None
        data = self._recv_exact(ln) if ln else b""
        if mkey:
            data = bytes(b ^ mkey[i % 4] for i, b in enumerate(data))
        return fin, opcode, data

    def recv_text(self, max_bytes=8 * 1024 * 1024):
        """Read one complete (possibly fragmented) text message."""
        buf = b""
        op = None
        while True:
            fin, opcode, data = self._read_frame()
            if opcode == OP_PING:
                self._send_control(OP_PONG, data)
                continue
            if opcode == OP_PONG:
                continue
            if opcode == OP_CLOSE:
                code = struct.unpack(">H", data[:2])[0] if len(data) >= 2 else None
                raise GatewayError(f"peer closed ws, code={code}, reason={data[2:][:200]!r}")
            if opcode in (OP_TEXT, OP_BIN):
                op, buf = opcode, data
            elif opcode == OP_CONT:
                buf += data
            if fin and op is not None:
                if len(buf) > max_bytes:
                    raise GatewayError("message too large")
                return buf.decode("utf-8", errors="replace")

    def _send_control(self, opcode, data=b""):
        mask = os.urandom(4)
        self.sock.sendall(bytes([0x80 | opcode, 0x80 | len(data)]) + mask +
                          bytes(b ^ mask[i % 4] for i, b in enumerate(data)))

    # ---------- RPC ----------
    def _next_id(self):
        self._id += 1
        return f"f{self._id}-{uuid.uuid4().hex[:8]}"

    def _pump_until(self, want_id, deadline):
        while time.time() < deadline:
            self.sock.settimeout(max(0.5, deadline - time.time()))
            try:
                msg = json.loads(self.recv_text())
            except socket.timeout:
                continue
            except json.JSONDecodeError:
                continue
            if msg.get("type") == "event":
                self.events.append(msg)
                if len(self.events) > 400:
                    del self.events[:200]
                continue
            if msg.get("type") == "res" and msg.get("id") == want_id:
                return msg
        raise GatewayError(f"timeout waiting for response to {want_id}")

    def call(self, method, params=None, timeout=None):
        rid = self._next_id()
        self.send_text(json.dumps({"type": "req", "id": rid, "method": method,
                                   "params": params or {}}))
        msg = self._pump_until(rid, time.time() + (timeout or self.timeout))
        if msg.get("ok"):
            return msg.get("result")
        err = msg.get("error") or {}
        raise GatewayError(f"{method} -> {err.get('code') or err.get('name')}: "
                           f"{err.get('message') or json.dumps(err)[:400]}")

    # ---------- lifecycle ----------
    def connect(self):
        self._open()
        self._handshake()
        auth = {}
        if self.token:
            auth["token"] = self.token
        if self.password:
            auth["password"] = self.password
        params = {
            "minProtocol": 4,
            "maxProtocol": 4,
            "client": {"id": self.client_name, "version": self.client_version,
                       "platform": self.platform, "mode": self.mode,
                       "instanceId": f"{self.client_name}-ops-cloud"},
            "role": "operator",
            "scopes": SCOPES,
            "device": None,
            "caps": ["tool-events"],
            "auth": auth,
            "userAgent": "ops-cloud-agentF/1.0",
            "locale": "en",
        }
        self.hello = self.call("connect", params, timeout=45)
        return self.hello

    def close(self):
        try:
            if self.sock:
                self._send_control(OP_CLOSE, struct.pack(">H", 1000))
                self.sock.close()
        except Exception:
            pass
        finally:
            self.sock = None

    def __enter__(self):
        self.connect()
        return self

    def __exit__(self, *a):
        self.close()
