/**
 * F-2g: signed device connect to the OpenClaw cloud.
 *
 * Everything here is taken from the installed openclaw source, not guessed:
 *   payload  : dist/device-auth-na9vtJo1.mjs      buildDeviceAuthPayloadV3
 *   deviceId : dist/device-identity-B_zMrBd6.mjs  deriveDeviceIdFromPublicKey
 *                                          = sha256hex(raw32bytePubKey)
 *   pubkey   : base64url(raw32bytePubKey)
 *   signature: base64url(ed25519(payload))
 *   signedAt : the server's connect.challenge.ts   (not local clock)
 *   nonce    : the server's connect.challenge.nonce
 *
 * Goal: find out whether a signed device gets real scopes, and if not, what the
 * server says. Either answer is a result.
 *
 * Usage: node cloud_signed.mjs [wss-url] [tokenSource]
 */
import { readFileSync, writeFileSync, existsSync } from "node:fs";
import crypto from "node:crypto";

const url = process.argv[2] ?? "wss://886841a9.openclaw.runware.run/";
const toksrc = process.argv[3] ?? "apiline93";
const log = (...a) => console.log(...a);
const j = (o) => JSON.stringify(o);
const sleep = (ms) => new Promise(r => setTimeout(r, ms));

// ---- credential ---------------------------------------------------------
const apib = readFileSync("C:/Users/DiDo/Desktop/APi.txt", "utf8");
const hex64 = [...apib.matchAll(/\b([0-9a-f]{64})\b/g)].map(m => m[1]);
const cfg = JSON.parse(readFileSync(`${process.env.USERPROFILE}/.openclaw/openclaw.json`, "utf8"));
let TOKEN;
if (toksrc === "local") TOKEN = cfg?.gateway?.auth?.token;
else {
  const ln = toksrc === "apiline57" ? 57 : 93;
  TOKEN = (apib.split(/\r?\n/)[ln - 1] ?? "").match(/\b[0-9a-f]{64}\b/)?.[0];
}
if (!TOKEN) { log("no token"); process.exit(1); }
log(`token from ${toksrc}, len ${TOKEN.length}`);

// ---- device identity (reuse the one already in this dir, else mint one) --
const ID_FILE = "device_identity_agentF.json";
let id;
if (existsSync(ID_FILE)) {
  id = JSON.parse(readFileSync(ID_FILE, "utf8"));
  log(`reusing device id ${id.deviceId.slice(0, 16)}…`);
} else {
  const { publicKey, privateKey } = crypto.generateKeyPairSync("ed25519");
  const rawPub = publicKey.export({ type: "spki", format: "der" }).subarray(12); // drop SPKI prefix
  const pubB64u = rawPub.toString("base64url");
  const deviceId = crypto.createHash("sha256").update(rawPub).digest("hex");
  id = { deviceId, publicKey: pubB64u, privateKeyPem: privateKey.export({ type: "pkcs8", format: "pem" }) };
  writeFileSync(ID_FILE, JSON.stringify(id, null, 2));
  log(`minted device id ${deviceId.slice(0, 16)}… (private key NOT committed — see .gitignore note)`);
}

const CLIENT_ID = "cli";
const CLIENT_MODE = "cli";
const ROLE = "operator";
const PLATFORM = "win32";
const DEVICE_FAMILY = "desktop";
const SCOPES = ["operator.read", "operator.write", "operator.admin", "operator.approvals"];

function payloadV3({ deviceId, clientId, clientMode, role, scopes, signedAtMs, token, nonce, platform, deviceFamily }) {
  const lower = (s) => (typeof s === "string" ? s.trim().replace(/[A-Z]/g, c => String.fromCharCode(c.charCodeAt(0) + 32)) : "");
  return ["v3", deviceId, clientId, clientMode, role, scopes.join(","), String(signedAtMs), token ?? "", nonce, lower(platform), lower(deviceFamily)].join("|");
}

function sign(privPem, payload) {
  const key = crypto.createPrivateKey(privPem);
  return crypto.sign(null, Buffer.from(payload, "utf8"), key).toString("base64url");
}

// ---- connect ------------------------------------------------------------
function run() {
  return new Promise((resolve) => {
    const ws = new WebSocket(url);
    const out = { url, tokenSource: toksrc, deviceId: id.deviceId, steps: [] };
    let challenge = null, settled = false;
    const done = (r) => { if (!settled) { settled = true; try { ws.close(); } catch {} resolve({ ...out, ...r }); } };
    const hardStop = setTimeout(() => done({ verdict: "TIMEOUT" }), 45000);

    ws.addEventListener("open", () => log("  ** socket open **"));
    ws.addEventListener("error", (e) => { clearTimeout(hardStop); log(`  !! ${e.message ?? e.type}`); done({ verdict: "TRANSPORT_ERROR" }); });

    ws.addEventListener("message", async (ev) => {
      let f; try { f = JSON.parse(ev.data); } catch { return; }

      if (f.type === "event" && f.event === "connect.challenge") {
        challenge = f.payload;
        log(`  <- challenge nonce=${challenge.nonce} ts=${challenge.ts}`);

        const payload = payloadV3({
          deviceId: id.deviceId, clientId: CLIENT_ID, clientMode: CLIENT_MODE,
          role: ROLE, scopes: SCOPES, signedAtMs: challenge.ts, token: TOKEN,
          nonce: challenge.nonce, platform: PLATFORM, deviceFamily: DEVICE_FAMILY,
        });
        const signature = sign(id.privateKeyPem, payload);
        log(`  payload  = ${payload.replace(TOKEN, "<TOKEN>")}`);
        log(`  signature= ${signature.slice(0, 32)}… (${signature.length} chars)`);

        ws.send(j({
          type: "req", id: "c1", method: "connect",
          params: {
            minProtocol: 4, maxProtocol: 4,
            client: { id: CLIENT_ID, displayName: "agent-F signed", version: "2026.9.6", platform: PLATFORM, deviceFamily: DEVICE_FAMILY, mode: CLIENT_MODE },
            role: ROLE, scopes: SCOPES, caps: [], commands: [], permissions: {},
            auth: { token: TOKEN }, locale: "en-US", userAgent: "openclaw-cli/2026.9.6",
            device: { id: id.deviceId, publicKey: id.publicKey, signature, signedAt: challenge.ts, nonce: challenge.nonce },
          },
        }));
        return;
      }

      if (f.type === "res" && f.id === "c1") {
        clearTimeout(hardStop);
        if (f.ok) {
          const p = f.payload ?? {};
          log(`\n*** SIGNED CONNECT OK ***  role=${p.auth?.role} scopes=${j(p.auth?.scopes)}`);
          log(`  server: ${j(p.server)}`);
          out.steps.push({ step: "connect", ok: true, auth: p.auth, server: p.server });
          out.grantedScopes = p.auth?.scopes ?? [];
          out.deviceToken = p.auth?.deviceToken ? "issued" : "none";
          out.methods = p.features?.methods ?? [];

          if (!out.grantedScopes.length) { out.verdict = "CONNECTED_BUT_NO_SCOPES"; done(out); return; }

          // we have scopes -> now answer the real question
          for (const [m, prm] of [["system.info", {}], ["channels.status", {}], ["terminal.list", {}], ["models.list", { view: "available" }]]) {
            const r = await rpc(ws, m, prm, 20000);
            log(`\n### ${m}\n  ${r.ok ? "OK " + j(r.payload).slice(0, 900) : "ERR " + j(r.error).slice(0, 400)}`);
            out.steps.push({ step: m, ...r, payload: r.ok ? truncate(r.payload) : undefined });
          }
          out.verdict = "SCOPED_ACCESS_OK";
          done(out);
        } else {
          log(`\n*** SIGNED CONNECT REJECTED ***\n  ${j(f.error)}`);
          out.steps.push({ step: "connect", ok: false, error: f.error });
          out.verdict = "REJECTED";
          done(out);
        }
      }
    });
  });
}

function truncate(o) {
  const s = j(o);
  return s.length > 1200 ? JSON.parse(s.slice(0, 0) + "{}") && { _truncated: true, preview: s.slice(0, 1200) } : o;
}

function rpc(ws, method, params, timeoutMs) {
  return new Promise((res) => {
    const id = `x${Math.random().toString(36).slice(2, 9)}`;
    const t = setTimeout(() => res({ ok: false, error: { code: "TIMEOUT" } }), timeoutMs);
    const onMsg = (ev) => {
      let f; try { f = JSON.parse(ev.data); } catch { return; }
      if (f.type === "res" && f.id === id) { clearTimeout(t); ws.removeEventListener("message", onMsg); res(f.ok ? { ok: true, payload: f.payload } : { ok: false, error: f.error }); }
    };
    ws.addEventListener("message", onMsg);
    ws.send(j({ type: "req", id, method, params }));
  });
}

log("=".repeat(78));
log(`F-2g  SIGNED DEVICE CONNECT  ${url}`);
log("=".repeat(78));
run().then((r) => {
  log("\n" + "=".repeat(78));
  log("VERDICT: " + r.verdict);
  if (r.grantedScopes) log("granted scopes: " + j(r.grantedScopes));
  if (r.deviceToken) log("device token: " + r.deviceToken);
  if (r.methods) log(`methods advertised: ${r.methods.length}`);
  log("=".repeat(78));
  writeFileSync("cloud_signed_result.json", JSON.stringify(r, null, 2));
  log("[saved] cloud_signed_result.json");
  process.exit(0);
});
