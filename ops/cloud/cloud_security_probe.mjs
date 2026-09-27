/**
 * F-2f: document the cloud gateway's actual security posture.
 *
 *  1. connect with NO credential        -> what does it say?
 *  2. connect with the WRONG credential -> what does it say?
 *  3. connect with the local token      -> AUTH_TOKEN_MISMATCH (already proven)
 *  4. probe every other WS path        -> is anything open?
 *
 * Then test SSH, which ops/roster/telegram/SETUP.md suggested but never verified.
 */
import { readFileSync, existsSync } from "node:fs";
import { execFileSync } from "node:child_process";

const url = process.argv[2] ?? "wss://886841a9.openclaw.runware.run/";
const log = (...a) => console.log(...a);
const j = (o) => JSON.stringify(o);
const sleep = (ms) => new Promise(r => setTimeout(r, ms));

const cfg = JSON.parse(readFileSync(`${process.env.USERPROFILE}/.openclaw/openclaw.json`, "utf8"));
const LOCAL_TOKEN = cfg?.gateway?.auth?.token;

function tryConnect(label, auth, extra = {}) {
  return new Promise((resolve) => {
    const ws = new WebSocket(url);
    let done = false;
    const finish = (r) => { if (!done) { done = true; try { ws.close(); } catch {} resolve(r); } };
    const timer = setTimeout(() => finish({ ok: false, error: { code: "TIMEOUT" } }), 20000);

    ws.addEventListener("open", () => {
      // wait briefly for the challenge, then connect
      setTimeout(() => {
        const params = {
          minProtocol: 4, maxProtocol: 4,
          client: { id: "cli", displayName: "agent-F", version: "2026.9.6", platform: "win32", mode: "cli" },
          role: "operator",
          scopes: ["operator.read", "operator.write", "operator.admin"],
          caps: [], commands: [], permissions: {},
          auth, locale: "en-US", userAgent: "openclaw-cli/2026.9.6",
          ...extra,
        };
        ws.send(j({ type: "req", id: "c1", method: "connect", params }));
      }, 600);
    });
    ws.addEventListener("message", (ev) => {
      let f; try { f = JSON.parse(ev.data); } catch { return; }
      if (f.type === "res" && f.id === "c1") {
        clearTimeout(timer);
        log(`\n### ${label}`);
        log(`  -> auth: ${j(auth)}`);
        if (f.ok) {
          const p = f.payload ?? {};
          log(`  *** ACCEPTED ***  role=${j(p.auth?.role)} scopes=${j(p.auth?.scopes)}`);
          log(`  server: ${j(p.server)}`);
          log(`  methods advertised: ${p.features?.methods?.length ?? "?"}`);
          if (p.features?.methods) {
            const t = p.features.methods.filter(m => /terminal|exec|fs\.|worktree|git/i.test(m));
            log(`  exec/terminal/fs methods available: ${t.length ? t.join(", ") : "NONE"}`);
          }
          finish({ ok: true, payload: p });
        } else {
          log(`  REJECTED ${j(f.error)}`);
          finish({ ok: false, error: f.error });
        }
      }
    });
    ws.addEventListener("error", (e) => { clearTimeout(timer); log(`\n### ${label}\n  TRANSPORT ERROR ${e.message ?? e.type}`); finish({ ok: false }); });
    ws.addEventListener("close", (e) => { if (!done) { clearTimeout(timer); log(`\n### ${label}\n  closed code=${e.code} reason=${e.reason}`); finish({ ok: false, error: { code: "CLOSED", reason: e.reason } }); } });
  });
}

async function main() {
  log("=".repeat(78));
  log("F-2f  CLOUD GATEWAY SECURITY POSTURE  " + url);
  log("=".repeat(78));

  await tryConnect("1. NO credential at all", {});
  await tryConnect("2. WRONG credential (fabricated)", { token: "a".repeat(48) });
  await tryConnect("3. LOCAL machine's gateway.auth.token", { token: LOCAL_TOKEN });
  await tryConnect("4. local token + bogus device identity", { token: LOCAL_TOKEN },
    { device: { id: "agentF-probe", publicKey: "x", signature: "x", signedAt: Date.now(), nonce: "x" } });

  log("\n" + "=".repeat(78));
  log("SSH / alternative ingress");
  log("=".repeat(78));
  const host = new URL(url.replace(/^wss/, "https")).host;
  for (const [label, cmd, args] of [
    ["ssh port probe", "ssh", ["-o", "BatchMode=yes", "-o", "ConnectTimeout=12", "-o", "StrictHostKeyChecking=no",
      "-p", "22", `openclaw@${host}`, "echo SSH_OK"]],
    ["ssh as root", "ssh", ["-o", "BatchMode=yes", "-o", "ConnectTimeout=12", "-o", "StrictHostKeyChecking=no",
      "-p", "22", `root@${host}`, "echo SSH_OK"]],
  ]) {
    try {
      const out = execFileSync(cmd, args, { encoding: "utf8", timeout: 40000, stdio: ["ignore", "pipe", "pipe"] });
      log(`  ${label}: OK -> ${out.trim().slice(0, 200)}`);
    } catch (e) {
      const s = (e.stderr ?? "").toString().trim().split("\n").slice(-2).join(" | ");
      log(`  ${label}: FAIL -> ${(e.message ?? "").slice(0, 120)} ${s}`);
    }
  }
  process.exit(0);
}
main();
setTimeout(() => { log("GLOBAL TIMEOUT"); process.exit(8); }, 180000);
