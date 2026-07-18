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
