/**
 * F-2e: minimal OpenClaw gateway WS client (Node >=22 global WebSocket).
 *
 * Implements the documented handshake:
 *   1. server -> event connect.challenge {nonce, ts}
 *   2. client -> req   connect {minProtocol:4,maxProtocol:4,client,role,scopes,auth,...}
 *   3. server -> res   hello-ok {protocol,server,features,snapshot,auth,policy}
 *
 * Tries token-only first; if the gateway demands device identity it says so and
 * we report that verbatim rather than guessing.
 *
 * Usage: node cloud_ws.mjs <wss-url> <mode>
 *   mode = probe     -> connect + system.info + channels.status
 *   mode = terminal  -> connect + terminal.open + run a command
 */
import { readFileSync } from "node:fs";
import { createHash } from "node:crypto";

const url = process.argv[2] ?? "wss://886841a9.openclaw.runware.run/";
const mode = process.argv[3] ?? "probe";
// server said: "at /client/mode: must be equal to one of the allowed values"
// -> the allowed set is local|remote (remote.md: LAN/tailnet/same-host are "remote")
const CLIENT_MODE = process.argv[4] ?? "remote";

const cfg = JSON.parse(readFileSync(`${process.env.USERPROFILE}/.openclaw/openclaw.json`, "utf8"));
const TOKEN = cfg?.gateway?.auth?.token;
const PW = cfg?.gateway?.auth?.password;

const log = (...a) => console.log(...a);
const j = (o) => JSON.stringify(o);

let ws, nextId = 1, connected = false;
const pending = new Map();
const events = [];
let challenge = null;

function send(obj) {
  const s = j(obj);
  const safe = TOKEN ? s.split(TOKEN).join(`<TOKEN:${TOKEN.length}>`)
                     : s.split(PW ?? "\u0000").join(`<PW:${(PW ?? "").length}>`);
  log(`  -> ${safe.length > 500 ? safe.slice(0, 500) + "..." : safe}`);
  ws.send(s);
}

function req(method, params = {}, timeoutMs = 25000) {
  const id = `r${nextId++}`;
  return new Promise((resolve) => {
    const t = setTimeout(() => {
      pending.delete(id);
      resolve({ ok: false, error: { code: "CLIENT_TIMEOUT", message: `no response to ${method} in ${timeoutMs}ms` } });
    }, timeoutMs);
    pending.set(id, (res) => { clearTimeout(t); resolve(res); });
    send({ type: "req", id, method, params });
  });
}

function handle(frame) {
  if (frame.type === "event") {
    events.push(frame);
    log(`  <- EVENT ${frame.event} ${j(frame.payload).slice(0, 300)}`);
    if (frame.event === "connect.challenge") challenge = frame.payload;
    return;
  }
  if (frame.type === "res") {
    const r = pending.get(frame.id);
    if (r) { pending.delete(frame.id); r(frame); }
    else log(`  <- RES(unmatched id=${frame.id}) ok=${frame.ok} ${j(frame.payload ?? frame.error).slice(0, 300)}`);
    return;
  }
  if (frame.type === "ping" || frame.type === "pong") { log(`  <- ${frame.type}`); return; }
  log(`  <- ${j(frame).slice(0, 300)}`);
}

async function connectFrame(withDevice) {
  const params = {
    minProtocol: 4,
    maxProtocol: 4,
    client: { id: "cli", version: "2026.9.6", platform: "win32", mode: CLIENT_MODE },
    role: "operator",
    scopes: ["operator.read", "operator.write", "operator.admin", "operator.approvals"],
    caps: [],
    commands: [],
    permissions: {},
    auth: TOKEN ? { token: TOKEN } : {},
    locale: "en-US",
    userAgent: "openclaw-cli/2026.9.6 (agent-F probe)",
  };
  if (PW) params.auth.password = PW;
  const res = await req("connect", params, 25000);
  if (res.ok) {
    connected = true;
    const p = res.payload ?? {};
    log("\n*** CONNECT OK ***");
    log("  protocol :", p.protocol);
    log("  server   :", j(p.server));
    log("  auth     :", j(p.auth));
    log("  policy   :", j(p.policy));
    const sn = p.snapshot ?? {};
    log("  snapshot keys:", Object.keys(sn).join(", ").slice(0, 900));
    if (sn.gateway) log("  snapshot.gateway:", j(sn.gateway).slice(0, 900));
    if (sn.nodes) log("  snapshot.nodes:", j(sn.nodes).slice(0, 500));
    if (p.features?.methods) log("  method count:", p.features.methods.length);
    if (p.features?.capabilities) log("  capabilities:", j(p.features.capabilities));
  } else {
    log("\n*** CONNECT REJECTED ***");
    log("  error:", j(res.error));
  }
  return res;
}

async function main() {
  log(`connecting to ${url}  mode=${mode}`);
  log(`token: ${TOKEN ? `present (len ${TOKEN.length})` : "ABSENT"}`);
  ws = new WebSocket(url, { headers: { "User-Agent": "openclaw-cli/2026.9.6" } });

  const opened = new Promise((res, rej) => {
    ws.addEventListener("open", () => { log("  ** socket open **"); res(); });
    ws.addEventListener("error", (e) => { log("  !! socket error:", e.message ?? e.type); rej(new Error("ws error")); });
    ws.addEventListener("close", (e) => log(`  ** socket close code=${e.code} reason=${e.reason} **`));
  });
  ws.addEventListener("message", (ev) => {
    let f; try { f = JSON.parse(ev.data); } catch { log("  <- non-json:", String(ev.data).slice(0, 200)); return; }
    handle(f);
  });

  try { await opened; } catch { log("RESULT: TRANSPORT FAILURE"); process.exit(2); }

  // wait for the challenge
  for (let i = 0; i < 50 && !challenge; i++) await new Promise(r => setTimeout(r, 100));
  log(`challenge: ${challenge ? j(challenge) : "NONE RECEIVED"}`);

  log(`\n--- attempt 1: token only, no device identity (client.mode=${CLIENT_MODE}) ---`);
  let res = await connectFrame(false);
  if (!res.ok) {
    log("\n--- attempt 2: retry after a pause ---");
    await new Promise(r => setTimeout(r, 1500));
    res = await connectFrame(false);
  }
  if (!res.ok) { log("\nRESULT: HANDSHAKE REFUSED"); ws.close(); process.exit(3); }

  // ---- probes -----------------------------------------------------------
  const calls = mode === "terminal"
    ? [["system.info", {}], ["channels.status", {}], ["terminal.list", {}]]
    : [["system.info", {}], ["channels.status", {}], ["models.list", { view: "available" }], ["usage.cost", {}]];

  for (const [m, p] of calls) {
    log(`\n### ${m}`);
    const r = await req(m, p, 25000);
    log(r.ok ? `  OK ${j(r.payload).slice(0, 1500)}` : `  ERR ${j(r.error).slice(0, 800)}`);
  }

  log("\n=== all events seen ===");
  for (const e of events) log(`  ${e.event} ${j(e.payload).slice(0, 200)}`);

  ws.close();
  process.exit(0);
}

main().catch((e) => { log("FATAL", e); process.exit(9); });
setTimeout(() => { log("GLOBAL TIMEOUT"); process.exit(8); }, 120000);
