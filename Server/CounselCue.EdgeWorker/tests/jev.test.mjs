import test from "node:test";
import assert from "node:assert/strict";
import worker from "../src/index.js";
import { SKILL_CRITERIA, analysisQuestions, analysisResult } from "../src/jev.js";

const limiter = { limit: async () => ({ success: true }) };
const origin = "https://educatian.github.io";
const post = (body) =>
  new Request("https://worker.test/analyze", {
    method: "POST",
    headers: { Origin: origin, "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
const env = { OPENROUTER_API_KEY: "k", TURN_LIMITER: limiter };

const decision = {
  model: "typesafe/jev-1.13-20260917",
  answers: {
    skill: { type: "choice", choice: "reflection_exploration", confidence: 0.81, probabilities: { reflection_exploration: 0.86, reflection: 0.12, open_question: 0.02 } },
    quality: { type: "score", score: 2.7, confidence: 0.77 },
    attends_to_feeling: { type: "noul", noul: 0.93 },
    client_affect: { type: "choice", choice: "relieved", confidence: 0.6 },
    client_intensity: { type: "score", score: 1.5 },
  },
};

test("the codebook is the same ten codes the LLM coder and Unity use", () => {
  assert.deepEqual(Object.keys(SKILL_CRITERIA).sort(), [
    "advice", "closed_question", "neutral", "open_question", "premature_reassurance",
    "reflection", "reflection_exploration", "silence", "validation", "why_question",
  ]);
  assert.equal(analysisQuestions(false).client_affect, undefined);
  assert.equal(analysisQuestions(true).client_affect.type, "choice");
  assert.equal(analysisQuestions(true).quality.criteria.length, 4);
});

test("analysisResult keeps calibrated probabilities and bounds every field", () => {
  const r = analysisResult(decision, 183.4);
  assert.equal(r.code, "reflection_exploration");
  assert.equal(r.confidence, 0.81);
  assert.equal(r.quality, 3);
  assert.equal(r.qualityScore, 2.7);
  assert.equal(r.attendsToFeeling, 0.93);
  assert.equal(r.probabilities.validation, 0);
  assert.equal(r.clientAffect, "relieved");
  assert.equal(r.clientIntensity, 0.5);
  assert.equal(r.latencyMs, 183);
  assert.throws(() => analysisResult({ answers: { skill: { choice: "hug" } } }, 1));
});

test("/analyze calls Jev through OpenRouter's Decisions API with typed questions", async () => {
  const old = globalThis.fetch;
  let url, sent, auth;
  globalThis.fetch = async (u, init) => {
    url = u;
    sent = JSON.parse(init.body);
    auth = new Headers(init.headers).get("Authorization");
    return new Response(JSON.stringify(decision));
  };
  try {
    const r = await worker.fetch(
      post({ sessionId: "s1", phase: "middle", clientLine: "숨이 막혀요.", counselorUtterance: "많이 답답하셨겠어요. 어떤 순간이 제일 힘드세요?", clientReply: "아침에 문 앞에서요." }),
      env,
    );
    const body = await r.json();
    assert.equal(r.status, 200);
    assert.equal(url, "https://openrouter.ai/api/alpha/decisions");
    assert.equal(auth, "Bearer k");
    assert.equal(sent.model, "typesafe/jev-1.13");
    assert.equal(sent.state.session_phase, "middle");
    assert.equal(sent.state.client_reply, "아침에 문 앞에서요.");
    assert.equal(sent.questions.skill.type, "choice");
    assert.equal(sent.questions.quality.type, "score");
    assert.equal(sent.questions.attends_to_feeling.type, "noul");
    assert.equal(body.code, "reflection_exploration");
    assert.equal(body.engine, "jev");
  } finally {
    globalThis.fetch = old;
  }
});

test("/analyze codes empty turns as silence without a call and fails safely", async () => {
  const old = globalThis.fetch;
  let calls = 0;
  globalThis.fetch = async () => {
    calls++;
    return new Response("busy", { status: 503 });
  };
  try {
    const silent = await worker.fetch(post({ sessionId: "s1", counselorUtterance: "" }), env);
    assert.equal((await silent.json()).code, "silence");
    assert.equal(calls, 0);
    const failed = await worker.fetch(post({ sessionId: "s1", counselorUtterance: "네" }), env);
    assert.equal(failed.status, 502);
    const off = await worker.fetch(post({ sessionId: "s1", counselorUtterance: "네" }), { ...env, ANALYSIS: "off" });
    assert.equal(off.status, 503);
  } finally {
    globalThis.fetch = old;
  }
});

test("/webgl serves versioned build files from R2 with long caching", async () => {
  const objects = { "v2/Build/WebGL.wasm.unityweb": { body: "WASM", size: 4, httpEtag: '"e1"' } };
  const bucket = { get: async (k) => objects[k] || null };
  const get = (path, origin = "https://counselcue.pages.dev") =>
    worker.fetch(new Request("https://worker.test" + path, { headers: { Origin: origin } }), { WEBGL_BUCKET: bucket });
  const ok = await get("/webgl/v2/Build/WebGL.wasm.unityweb");
  assert.equal(ok.status, 200);
  assert.equal(await ok.text(), "WASM");
  assert.equal(ok.headers.get("Content-Type"), "application/octet-stream");
  assert.match(ok.headers.get("Cache-Control"), /immutable/);
  assert.equal(ok.headers.get("Access-Control-Allow-Origin"), "https://counselcue.pages.dev");
  assert.equal((await get("/webgl/v2/Build/missing.js")).status, 404);
  assert.equal((await get("/webgl/../secrets")).status, 404);
  assert.equal((await get("/webgl/v2/index.html")).status, 404);
  assert.equal((await get("/webgl/v2/Build/WebGL.wasm.unityweb", "https://evil.example")).status, 403);
});
