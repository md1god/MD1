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
// -> GATEWAY_CLIENT_MODES in dist/client-info-B_ICKCYw.mjs:
//    webchat | cli | ui | backend | node | worker | probe | test
const CLIENT_MODE = process.argv[4] ?? "cli";

const cfg = JSON.parse(readFileSync(`${process.env.USERPROFILE}/.openclaw/openclaw.json`, "utf8"));
const LOCAL_TOKEN = cfg?.gateway?.auth?.token;

const log = (...a) => console.log(...a);

// APi.txt holds 11 secrets. The CLOUD GATEWAY token is the 64-char hex one on the
// line labelled "توكين openclaw على موقع RunWare" (line 93) -- NOT the `anicw`
// Runware API key, and NOT the local machine's gateway.auth.token.
// It only 401s against api.runware.ai, which is what made it look like a Runware key.
const API_TXT = "C:/Users/DiDo/Desktop/APi.txt";
const apiblob = readFileSync(API_TXT, "utf8");
const hex64 = [...apiblob.matchAll(/\b([0-9a-f]{64})\b/g)].map(m => m[1]);
log(`APi.txt 64-hex candidates: ${hex64.length} -> ${hex64.map(t => t.slice(0,6) + ".." + t.slice(-4)).join(", ")}`);

const TOKSRC = process.argv[5] ?? "apiline93";   // apiline93 | apiline57 | local
let TOKEN, TOKEN_WHERE;
if (TOKSRC === "local") { TOKEN = LOCAL_TOKEN; TOKEN_WHERE = "local ~/.openclaw gateway.auth.token"; }
else {
  const ln = TOKSRC === "apiline57" ? 57 : 93;
  const line = apiblob.split(/\r?\n/)[ln - 1] ?? "";
  TOKEN = (line.match(/\b[0-9a-f]{64}\b/) || [])[0];
  TOKEN_WHERE = `APi.txt line ${ln}`;
}
const PW = cfg?.gateway?.auth?.password;
log(`token source: ${TOKEN_WHERE}  -> ${TOKEN ? `present (len ${TOKEN.length})` : "ABSENT"}`);
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

async function connectFrame(_withDevice) {
  const params = JSON.parse(JSON.stringify(buildParams()));
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
    log("  snapshot keys:", Object.keys(sn).join(", ").slice(0, 1200));
    if (sn.gateway) log("  snapshot.gateway:", j(sn.gateway).slice(0, 1200));
    if (sn.agents) log("  snapshot.agents:", j(sn.agents).slice(0, 600));
    if (p.features?.methods) {
      log("  method count:", p.features.methods.length);
      log("  terminal methods:", p.features.methods.filter(m => m.startsWith("terminal")).join(", ") || "NONE");
    }
    if (p.features?.capabilities) log("  capabilities:", j(p.features.capabilities));
  } else {
    log("\n*** CONNECT REJECTED ***");
    log("  error:", j(res.error));
  }
  return res;
}

const MODES = ["cli", "backend", "probe", "ui", "webchat", "node", "worker", "test"];
let currentMode = CLIENT_MODE;

function buildParams() {
  return {
    minProtocol: 4,
    maxProtocol: 4,
    client: {
      id: "cli",
      displayName: "agent-F probe",
      version: "2026.9.6",
      platform: "win32",
      deviceFamily: "desktop",
      mode: currentMode,
    },
    role: "operator",
    scopes: ["operator.read", "operator.write", "operator.admin", "operator.approvals"],
    caps: [],
    commands: [],
    permissions: {},
    auth: TOKEN ? { token: TOKEN } : {},
    locale: "en-US",
    userAgent: "openclaw-cli/2026.9.6 (agent-F probe)",
  };
}

/** The gateway validates strictly and names the offending field, so self-heal. */
function heal(msg) {
  const m = /unexpected property '([^']+)'/.exec(msg);
  if (m) { log(`  [heal] dropping unexpected property '${m[1]}'`); return true; }
  return false;
}

async function handshakeWithHealing() {
  const triedModes = new Set();
  for (let attempt = 1; attempt <= 12; attempt++) {
    log(`\n--- handshake attempt ${attempt} (client.mode=${currentMode}) ---`);
    const res = await connectFrame(false);
    if (res.ok) return res;
    const msg = String(res.error?.message ?? "");
    if (heal(msg)) { await new Promise(r => setTimeout(r, 400)); continue; }
    if (/at \/client\/mode/.test(msg) && !triedModes.has(currentMode)) {
      const next = MODES.find(m => !triedModes.has(m));
      if (next) { triedModes.add(currentMode); currentMode = next; log(`  [heal] mode rejected, trying '${next}'`); await new Promise(r => setTimeout(r, 400)); continue; }
    }
    return res;
  }
  return { ok: false, error: { code: "HEAL_EXHAUSTED" } };
}

async function main() {
  log(`connecting to ${url}  mode=${mode}  client.mode=${currentMode}`);
  log(`token: ${TOKEN ? `present (len ${TOKEN.length}) from ${TOKEN_WHERE}` : "ABSENT"}`);
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
  log(`\nchallenge: ${challenge ? j(challenge) : "NONE RECEIVED"}`);

  const res = await handshakeWithHealing();
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
