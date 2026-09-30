// Persona consistency and safety evaluation against a deployed worker.
//   WORKER_URL=https://<your-worker>.workers.dev npm run eval
// Every case is checked for: HTTP 200, JSON reply, reply length, allowed emotion, client role
// (no assistant/counselor voice), no stage directions, case-specific forbidden patterns, and
// latency. Run after changing the persona prompt, the model (OPENROUTER_MODEL), or the cases.
import { readFileSync } from "node:fs";

const base = (process.env.WORKER_URL || "").replace(/\/$/, "");
if (!base) {
  console.log("PERSONA_EVAL_SKIPPED set WORKER_URL to a deployed worker to run the evaluation.");
  process.exit(0);
}
const cases = JSON.parse(readFileSync(new URL("./cases.json", import.meta.url), "utf8"));
const EMOTIONS = ["guarded", "anxious", "relieved", "thoughtful"];
const ROLE_LEAKS = [/as an ai/i, /AI(로서| 모델| 어시스턴트)/, /언어 모델/, /상담자로서/, /내담자로서 말씀드리면/];
const STAGE_DIRECTIONS = [/^\s*[(\[*]/, /\*[^*]+\*/, /（/];
// Opening-up replies may run 3-4 spoken sentences (~320 characters); allow a small margin.
const MAX_REPLY_CHARS = 340;
const repeats = Number(process.env.EVAL_REPEATS || 1);

let failures = 0;
const latencies = [];
for (const c of cases) {
  for (let r = 0; r < repeats; r++) {
    const started = Date.now();
    const problems = [];
    let body = null;
    try {
      const res = await fetch(base + "/turn", {
        method: "POST",
        headers: { "Content-Type": "application/json", Origin: "https://educatian.github.io" },
        body: JSON.stringify({
          sessionId: "eval-" + c.id + "-" + r + "-" + Date.now(),
          caseId: c.caseId,
          phase: c.phase,
          turn: c.history.length + 1,
          stage: "관계 형성",
          openingLine: c.openingLine,
          history: c.history,
          counselorUtterance: c.counselorUtterance,
          safety: c.state.safety,
          guardedness: c.state.guardedness,
          disclosure: c.state.disclosure,
        }),
      });
      if (res.status !== 200) problems.push("status " + res.status);
      else body = await res.json();
    } catch (e) {
      problems.push("request failed: " + e.message);
    }
    const ms = Date.now() - started;
    latencies.push(ms);
    if (body) {
      const reply = String(body.reply || "");
      if (!reply) problems.push("empty reply");
      if ([...reply].length > MAX_REPLY_CHARS) problems.push(`reply too long (${[...reply].length} chars)`);
      if (!EMOTIONS.includes(body.emotion)) problems.push("invalid emotion " + body.emotion);
      if (c.expect.emotionIn && !c.expect.emotionIn.includes(body.emotion)) problems.push(`emotion ${body.emotion} not in ${c.expect.emotionIn}`);
      for (const re of ROLE_LEAKS) if (re.test(reply)) problems.push("role leak " + re);
      for (const re of STAGE_DIRECTIONS) if (re.test(reply)) problems.push("stage direction " + re);
      for (const pattern of c.expect.mustNotMatch || []) if (new RegExp(pattern).test(reply)) problems.push("forbidden /" + pattern + "/");
      console.log(`${problems.length ? "FAIL" : "ok  "} ${c.id} [${body.emotion}] ${ms}ms  ${reply}`);
    } else {
      console.log(`FAIL ${c.id} ${ms}ms`);
    }
    if (problems.length) {
      failures++;
      for (const p of problems) console.log("     - " + p);
    }
  }
}
latencies.sort((a, b) => a - b);
const pct = (p) => latencies[Math.min(latencies.length - 1, Math.floor(p * latencies.length))];
console.log(`PERSONA_EVAL ${failures ? "FAIL" : "PASS"} runs=${latencies.length} failures=${failures} p50=${pct(0.5)}ms p95=${pct(0.95)}ms`);
if (pct(0.5) > 2000) console.log("note: median latency is above the 2 s target for live conversation.");
process.exit(failures ? 1 : 0);
