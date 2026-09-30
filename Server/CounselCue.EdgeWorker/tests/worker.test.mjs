import test from "node:test";
import assert from "node:assert/strict";
import worker from "../src/index.js";

const limiter = { limit: async () => ({ success: true }) };
const env = {
  OPENROUTER_API_KEY: "test-openrouter",
  ELEVENLABS_API_KEY: "test-eleven",
  OPENROUTER_MODEL: "test-model",
  ELEVENLABS_VOICE_ID: "voice",
  TURN_LIMITER: limiter,
  VOICE_LIMITER: limiter,
};
const origin = "https://educatian.github.io";

test("health never exposes credentials", async () => {
  const r = await worker.fetch(
    new Request("https://worker.test/health", { headers: { Origin: origin } }),
    env,
  );
  assert.equal(r.status, 200);
  assert.deepEqual(await r.json(), {
    ok: true,
    services: { persona: true, voice: true },
  });
});

test("turn keeps the persona server-side and returns bounded state", async () => {
  const old = globalThis.fetch;
  let outbound, outboundUrl, outboundHeaders;
  globalThis.fetch = async (url, init) => {
    outboundUrl = url;
    outboundHeaders = new Headers(init.headers);
    outbound = JSON.parse(init.body);
    return new Response(
      JSON.stringify({
        choices: [
          {
            message: {
              content: '{"reply":"회사 입구에 도착할 때부터 숨이 답답해져요.","emotion":"anxious"}',
            },
          },
        ],
      }),
      { status: 200 },
    );
  };
  try {
    const req = new Request("https://worker.test/turn", {
      method: "POST",
      headers: { Origin: origin, "Content-Type": "application/json" },
      body: JSON.stringify({
        sessionId: "s1",
        turn: 1,
        stage: "관계 형성",
        counselorUtterance: "언제 가장 힘드신가요?",
        safety: 0.4,
        guardedness: 0.6,
        disclosure: 0.3,
      }),
    });
    const r = await worker.fetch(req, env),
      body = await r.json();
    assert.equal(r.status, 200);
    assert.equal(body.emotion, "anxious");
    assert.match(body.reply, /회사 입구/);
    assert.equal(outboundUrl, "https://openrouter.ai/api/v1/chat/completions");
    assert.equal(outboundHeaders.get("authorization"), "Bearer test-openrouter");
    assert.equal(
      outboundHeaders.get("http-referer"),
      "https://educatian.github.io/counselcue/",
    );
    assert.equal(outboundHeaders.get("x-title"), "CounselCue");
    assert.equal(outbound.model, "test-model");
    assert.deepEqual(
      outbound.messages.map(({ role }) => role),
      ["system", "user"],
    );
    assert.deepEqual(outbound.response_format, { type: "json_object" });
  } finally {
    globalThis.fetch = old;
  }
});

test("voice prepends a bounded Eleven v3 emotion tag", async () => {
  const old = globalThis.fetch;
  let outbound;
  globalThis.fetch = async (_url, init) => {
    outbound = JSON.parse(init.body);
    return new Response(new Uint8Array([1, 2, 3]), {
      status: 200,
      headers: { "Content-Type": "audio/mpeg" },
    });
  };
  try {
    const req = new Request("https://worker.test/voice", {
      method: "POST",
      headers: { Origin: origin, "Content-Type": "application/json" },
      body: JSON.stringify({ text: "조금 안심돼요.", emotion: "relieved" }),
    });
    const r = await worker.fetch(req, env);
    assert.equal(r.status, 200);
    assert.equal(r.headers.get("x-ai-generated-voice"), "true");
    assert.equal(outbound.model_id, "eleven_v3");
    assert.match(outbound.text, /^\[relieved\] \[warmly\]/);
  } finally {
    globalThis.fetch = old;
  }
});

test("untrusted browser origins are rejected", async () => {
  const r = await worker.fetch(
    new Request("https://worker.test/health", {
      headers: { Origin: "https://evil.example" },
    }),
    env,
  );
  assert.equal(r.status, 403);
});

const post = (path, body, headers = {}) =>
  new Request("https://worker.test" + path, {
    method: "POST",
    headers: { Origin: origin, "Content-Type": "application/json", ...headers },
    body: typeof body === "string" ? body : JSON.stringify(body),
  });

const withFetch = async (impl, run) => {
  const old = globalThis.fetch;
  globalThis.fetch = impl;
  try {
    await run();
  } finally {
    globalThis.fetch = old;
  }
};

const personaOk = () =>
  new Response(
    JSON.stringify({
      choices: [{ message: { content: '{"reply":"네, 조금 더 말해 볼게요.","emotion":"thoughtful"}' } }],
    }),
    { status: 200 },
  );

test("turn forwards bounded conversation history and the opening line", async () => {
  let outbound;
  const longHistory = Array.from({ length: 12 }, (_, i) => ({
    counselor: "상담자 " + i + "x".repeat(600),
    client: "내담자 " + i,
  }));
  await withFetch(
    async (_url, init) => {
      outbound = JSON.parse(init.body);
      return personaOk();
    },
    async () => {
      const r = await worker.fetch(
        post("/turn", {
          sessionId: "s1",
          caseId: "older-bereavement-01",
          turn: 13,
          counselorUtterance: "그 조용함이 어떻게 느껴지세요?",
          openingLine: "집에 들어가면 너무 조용합니다.",
          history: [...longHistory, "not-an-object", null],
        }),
        env,
      );
      assert.equal(r.status, 200);
    },
  );
  const input = JSON.parse(outbound.messages[1].content);
  assert.equal(input.opening_line, "집에 들어가면 너무 조용합니다.");
  assert.equal(input.conversation_so_far.length, 8);
  assert.ok(input.conversation_so_far.every((h) => h.counselor.length <= 400));
  assert.equal(input.turn, 13);
  assert.match(outbound.messages[0].content, /Lee Jeong-ho/);
  assert.match(outbound.messages[0].content, /never describe self-harm plans/);
});

test("unknown case ids fall back to the default persona", async () => {
  let outbound;
  await withFetch(
    async (_url, init) => {
      outbound = JSON.parse(init.body);
      return personaOk();
    },
    async () => {
      await worker.fetch(
        post("/turn", { sessionId: "s", caseId: "__proto__", counselorUtterance: "안녕하세요" }),
        env,
      );
    },
  );
  assert.match(outbound.messages[0].content, /Kim Ji-hye/);
});

test("turn rate limits pace each session and cap each address", async () => {
  const sessionKeys = [], addressKeys = [];
  const session = { limit: async ({ key }) => (sessionKeys.push(key), { success: true }) };
  const address = { limit: async ({ key }) => (addressKeys.push(key), { success: false }) };
  const r = await worker.fetch(
    post("/turn", { sessionId: "s-42", counselorUtterance: "네" }, { "CF-Connecting-IP": "203.0.113.9" }),
    { ...env, TURN_LIMITER: session, TURN_IP_LIMITER: address },
  );
  assert.equal(r.status, 429);
  assert.deepEqual(sessionKeys, ["203.0.113.9:s-42"]);
  assert.deepEqual(addressKeys, ["203.0.113.9"]);
});

test("voice uses the case-specific voice map and falls back safely", async () => {
  const urls = [];
  const map = { "adolescent-pressure-01": "TeenVoice12345", "career-transition-01": "bad id!" };
  await withFetch(
    async (url) => {
      urls.push(url);
      return new Response(new Uint8Array([1]), { status: 200 });
    },
    async () => {
      const voiceEnv = { ...env, ELEVENLABS_VOICE_IDS: JSON.stringify(map), ELEVENLABS_VOICE_ID: "DefaultVoice01" };
      await worker.fetch(post("/voice", { text: "네", caseId: "adolescent-pressure-01" }), voiceEnv);
      await worker.fetch(post("/voice", { text: "네", caseId: "career-transition-01" }), voiceEnv);
      await worker.fetch(post("/voice", { text: "네" }), { ...env, ELEVENLABS_VOICE_IDS: map });
    },
  );
  assert.match(urls[0], /text-to-speech\/TeenVoice12345\//);
  assert.match(urls[1], /text-to-speech\/DefaultVoice01\//);
  // env.ELEVENLABS_VOICE_ID ("voice") is not a valid id, so the built-in default is used.
  assert.match(urls[2], /text-to-speech\/21m00Tcm4TlvDq8ikWAM\//);
});

test("missing upstream credentials return 503 without calling out", async () => {
  let called = false;
  await withFetch(
    async () => ((called = true), personaOk()),
    async () => {
      const t = await worker.fetch(post("/turn", { sessionId: "s", counselorUtterance: "네" }), { ...env, OPENROUTER_API_KEY: "" });
      const v = await worker.fetch(post("/voice", { text: "네" }), { ...env, ELEVENLABS_API_KEY: "" });
      assert.equal(t.status, 503);
      assert.equal(v.status, 503);
    },
  );
  assert.equal(called, false);
});

test("oversized and malformed bodies are rejected before any upstream call", async () => {
  const big = await worker.fetch(post("/turn", JSON.stringify({ sessionId: "s", counselorUtterance: "가".repeat(30000) })), env);
  const bad = await worker.fetch(post("/turn", "{not json"), env);
  const nul = await worker.fetch(post("/turn", "null"), env);
  assert.equal(big.status, 413);
  assert.equal(bad.status, 400);
  assert.equal(nul.status, 400);
});

test("upstream network failures become a 504 instead of an exception", async () => {
  await withFetch(
    async () => {
      throw new DOMException("timed out", "TimeoutError");
    },
    async () => {
      const r = await worker.fetch(post("/turn", { sessionId: "s", counselorUtterance: "네" }), env);
      assert.equal(r.status, 504);
      assert.equal((await r.json()).error, "persona_timeout");
    },
  );
});

test("rejected origins do not receive their own origin in CORS headers", async () => {
  const r = await worker.fetch(
    new Request("https://worker.test/turn", { method: "POST", headers: { Origin: "https://evil.example" }, body: "{}" }),
    env,
  );
  assert.equal(r.status, 403);
  assert.notEqual(r.headers.get("access-control-allow-origin"), "https://evil.example");
});

test("voice limits pace each page and cap each address", async () => {
  const pageKeys = [], addressKeys = [];
  const page = { limit: async ({ key }) => (pageKeys.push(key), { success: true }) };
  const address = { limit: async ({ key }) => (addressKeys.push(key), { success: false }) };
  const r = await worker.fetch(
    post("/voice", { text: "네", clientId: "tab-7" }, { "CF-Connecting-IP": "198.51.100.4" }),
    { ...env, VOICE_LIMITER: page, VOICE_IP_LIMITER: address },
  );
  assert.equal(r.status, 429);
  assert.deepEqual(pageKeys, ["198.51.100.4:tab-7"]);
  assert.deepEqual(addressKeys, ["198.51.100.4"]);
});
