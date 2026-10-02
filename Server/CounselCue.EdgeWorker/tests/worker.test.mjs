import test from "node:test";
import assert from "node:assert/strict";
import worker, { DEFAULT_VOICES, codingResult, liveInstruction, personaResult, voiceFor } from "../src/index.js";
import { PHASE_GUIDE, phaseBlock, phaseKey } from "../src/phases.js";

const limiter = { limit: async () => ({ success: true }) };
const env = {
  OPENROUTER_API_KEY: "test-openrouter",
  ELEVENLABS_API_KEY: "test-eleven",
  PERSONA_MODEL: "test-model",
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
    services: { persona: true, coder: true, analysis: true, live: true, liveProvider: "relay", voice: true },
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
    const r = await worker.fetch(req, { ...env, VOICE_PROVIDER: "elevenlabs" });
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
      const voiceEnv = { ...env, VOICE_PROVIDER: "elevenlabs", ELEVENLABS_VOICE_IDS: JSON.stringify(map), ELEVENLABS_VOICE_ID: "DefaultVoice01" };
      await worker.fetch(post("/voice", { text: "네", caseId: "adolescent-pressure-01" }), voiceEnv);
      await worker.fetch(post("/voice", { text: "네", caseId: "career-transition-01" }), voiceEnv);
      await worker.fetch(post("/voice", { text: "네" }), { ...env, VOICE_PROVIDER: "elevenlabs", ELEVENLABS_VOICE_IDS: map });
    },
  );
  assert.match(urls[0], /text-to-speech\/TeenVoice12345\//);
  // An invalid env id for the case falls back to the built-in case voice (George), not the global one.
  assert.match(urls[1], /text-to-speech\/JBFqnCBsd6RMkjVDRZzb\//);
  // No caseId means the default case, whose built-in voice is Bella.
  assert.match(urls[2], /text-to-speech\/EXAVITQu4vr4xnSDxMaL\//);
});

test("voiceFor precedence: env case map, built-in case voice, env default, Rachel", () => {
  const envMap = { ELEVENLABS_VOICE_IDS: { "career-transition-01": "EnvCareer1234" }, ELEVENLABS_VOICE_ID: "EnvDefault123" };
  assert.equal(voiceFor("career-transition-01", envMap), "EnvCareer1234");
  // With no env configuration, male clients get a built-in male voice instead of Rachel.
  assert.equal(voiceFor("career-transition-01", {}), DEFAULT_VOICES["career-transition-01"]);
  assert.equal(voiceFor("career-transition-01", {}), "JBFqnCBsd6RMkjVDRZzb");
  assert.equal(voiceFor("older-bereavement-01", {}), "pqHfZKP75CvOlQylNhV4");
  assert.equal(voiceFor("international-belonging-01", { ELEVENLABS_VOICE_ID: "EnvDefault123" }), "IKne3meq5aSn9XLyUdCD");
  assert.equal(voiceFor("adolescent-pressure-01", { ELEVENLABS_VOICE_IDS: "not json" }), "MF3mGyEYCl7XYWbV9V6O");
  assert.equal(voiceFor("unknown-case", { ELEVENLABS_VOICE_ID: "EnvDefault123" }), "EnvDefault123");
  assert.equal(voiceFor("unknown-case", {}), "21m00Tcm4TlvDq8ikWAM");
  assert.equal(voiceFor("__proto__", {}), "21m00Tcm4TlvDq8ikWAM");
  assert.equal(Object.keys(DEFAULT_VOICES).length, 5);
});

test("missing upstream credentials return 503 without calling out", async () => {
  let called = false;
  await withFetch(
    async () => ((called = true), personaOk()),
    async () => {
      const t = await worker.fetch(post("/turn", { sessionId: "s", counselorUtterance: "네" }), { ...env, OPENROUTER_API_KEY: "" });
      const v = await worker.fetch(post("/voice", { text: "네" }), { ...env, OPENROUTER_API_KEY: "", ELEVENLABS_API_KEY: "" });
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

const coderReply = (content) =>
  new Response(JSON.stringify({ choices: [{ message: { content } }] }), { status: 200 });

test("code returns a validated codebook label with rationale and real evidence", async () => {
  let outbound;
  await withFetch(
    async (_url, init) => {
      outbound = JSON.parse(init.body);
      return coderReply(
        '{"code":"reflection_exploration","quality":3,"rationale":"감정을 짚고 탐색을 열었습니다.","evidence":"숨이 막히는 느낌","confidence":0.82}',
      );
    },
    async () => {
      const r = await worker.fetch(
        post("/code", {
          sessionId: "s1",
          caseId: "workplace-anxiety-01",
          clientLine: "요즘 회사에 가려고 하면 숨이 막혀요.",
          counselorUtterance: "숨이 막히는 느낌이 드시는군요. 그때 어떤 생각이 드세요?",
        }),
        env,
      );
      assert.equal(r.status, 200);
      const body = await r.json();
      assert.equal(body.code, "reflection_exploration");
      assert.equal(body.skill, "감정 반영 + 탐색");
      assert.equal(body.quality, 3);
      assert.equal(body.evidence, "숨이 막히는 느낌");
      assert.equal(body.codebook, "ko-codebook-1");
    },
  );
  assert.equal(outbound.temperature, 0);
  assert.match(outbound.messages[0].content, /premature_reassurance/);
  const input = JSON.parse(outbound.messages[1].content);
  assert.equal(input.previous_client_line, "요즘 회사에 가려고 하면 숨이 막혀요.");
});

test("code clamps quality, drops invented evidence and rejects unknown codes", async () => {
  const answers = [
    '{"code":"advice","quality":3,"rationale":"x","evidence":"없는 문장","confidence":2}',
    '{"code":"diagnosis","quality":2}',
  ];
  await withFetch(
    async () => coderReply(answers.shift()),
    async () => {
      const ok = await (await worker.fetch(post("/code", { sessionId: "s", counselorUtterance: "그냥 팀장님께 말씀드려 보세요." }), env)).json();
      assert.equal(ok.quality, 1);
      assert.equal(ok.evidence, "");
      assert.equal(ok.confidence, 1);
      const bad = await worker.fetch(post("/code", { sessionId: "s", counselorUtterance: "네" }), env);
      assert.equal(bad.status, 502);
      assert.equal((await bad.json()).error, "coder_invalid_output");
    },
  );
});

test("code uses its own rate-limit bucket and needs credentials", async () => {
  const keys = [];
  const limiter = { limit: async ({ key }) => (keys.push(key), { success: false }) };
  const limited = await worker.fetch(
    post("/code", { sessionId: "s-9", counselorUtterance: "네" }, { "CF-Connecting-IP": "203.0.113.5" }),
    { ...env, CODE_LIMITER: limiter },
  );
  assert.equal(limited.status, 429);
  assert.deepEqual(keys, ["203.0.113.5:s-9:code"]);
  const missing = await worker.fetch(post("/code", { sessionId: "s", counselorUtterance: "네" }), { ...env, OPENROUTER_API_KEY: "" });
  assert.equal(missing.status, 503);
});

test("live-token mints a locked Gemini Live token without exposing the key or persona", async () => {
  let outboundUrl, outboundHeaders, outbound;
  await withFetch(
    async (url, init) => {
      outboundUrl = url;
      outboundHeaders = new Headers(init.headers);
      outbound = JSON.parse(init.body);
      return new Response(JSON.stringify({ name: "auth_tokens/abc123" }), { status: 200 });
    },
    async () => {
      const r = await worker.fetch(
        post("/live-token", {
          sessionId: "s1",
          caseId: "older-bereavement-01",
          openingLine: "집에 들어가면 너무 조용합니다.",
          safety: 0.4,
          guardedness: 0.6,
          disclosure: 0.3,
        }),
        { ...env, GEMINI_API_KEY: "test-gemini" },
      );
      assert.equal(r.status, 200);
      const body = await r.json();
      assert.equal(body.token, "auth_tokens/abc123");
      assert.equal(body.model, "gemini-3.8-live");
      assert.equal(body.voice, "Algenib");
      assert.match(body.wsUrl, /BidiGenerateContentConstrained$/);
      assert.ok(!JSON.stringify(body).includes("test-gemini"));
      assert.ok(!JSON.stringify(body).includes("Lee Jeong-ho"));
    },
  );
  assert.equal(outboundUrl, "https://generativelanguage.googleapis.com/v1beta/auth_tokens");
  assert.equal(outboundHeaders.get("x-goog-api-key"), "test-gemini");
  const config = outbound.bidiGenerateContentSetup;
  assert.equal(config.model, "models/gemini-3.8-live");
  assert.deepEqual(config.generationConfig.responseModalities, ["AUDIO"]);
  assert.equal(config.generationConfig.speechConfig.voiceConfig.prebuiltVoiceConfig.voiceName, "Algenib");
  const instruction = config.systemInstruction.parts[0].text;
  assert.match(instruction, /Lee Jeong-ho/);
  assert.match(instruction, /집에 들어가면 너무 조용합니다/);
  assert.match(instruction, /\[상담 시스템\]/);
  assert.doesNotMatch(instruction, /Return only valid JSON/);
  assert.ok(outbound.uses >= 1 && outbound.uses <= 5);
  // No affect tool by default: it delays the first audio and splits the turn.
  assert.equal(config.tools, undefined);
  assert.doesNotMatch(instruction, /set_client_affect/);
});

test("live-token needs a Gemini key and honours rate limits and voice overrides", async () => {
  const missing = await worker.fetch(post("/live-token", { sessionId: "s" }), env);
  assert.equal(missing.status, 503);
  const keys = [];
  const limiter = { limit: async ({ key }) => (keys.push(key), { success: false }) };
  const limited = await worker.fetch(
    post("/live-token", { sessionId: "s-3" }, { "CF-Connecting-IP": "198.51.100.8" }),
    { ...env, GEMINI_API_KEY: "k", LIVE_LIMITER: limiter },
  );
  assert.equal(limited.status, 429);
  assert.deepEqual(keys, ["198.51.100.8:s-3:live"]);
  let voice;
  await withFetch(
    async (_u, init) => ((voice = JSON.parse(init.body).bidiGenerateContentSetup.generationConfig.speechConfig.voiceConfig.prebuiltVoiceConfig.voiceName),
      new Response(JSON.stringify({ name: "t" }), { status: 200 })),
    async () => {
      await worker.fetch(post("/live-token", { sessionId: "s", caseId: "adolescent-pressure-01" }),
        { ...env, GEMINI_API_KEY: "k", GEMINI_LIVE_VOICES: JSON.stringify({ "adolescent-pressure-01": "Aoede" }) });
    },
  );
  assert.equal(voice, "Aoede");
});

test("phaseKey validates phases and phaseBlock is empty for intake", () => {
  assert.equal(phaseKey("MIDDLE "), "middle");
  assert.equal(phaseKey("goal_setting"), "goal_setting");
  assert.equal(phaseKey("session-9"), "intake");
  assert.equal(phaseKey(undefined), "intake");
  assert.equal(phaseBlock("career-transition-01", "intake"), "");
  assert.match(phaseBlock("career-transition-01", "termination"), /^SESSION PHASE: termination/);
});

const turnSystem = async (body) => {
  let outbound;
  await withFetch(
    async (_url, init) => {
      outbound = JSON.parse(init.body);
      return personaOk();
    },
    async () => {
      const r = await worker.fetch(post("/turn", { sessionId: "s", counselorUtterance: "네", ...body }), env);
      assert.equal(r.status, 200);
    },
  );
  return { system: outbound.messages[0].content, input: JSON.parse(outbound.messages[1].content), outbound };
};

test("turn appends the phase block after the case for non-intake phases only", async () => {
  const middle = await turnSystem({ caseId: "workplace-anxiety-01", phase: "middle" });
  assert.equal(middle.input.session_phase, "middle");
  assert.ok(middle.system.includes(phaseBlock("workplace-anxiety-01", "middle")));
  assert.ok(middle.system.indexOf("SESSION PHASE: middle") > middle.system.indexOf("CASE\nYou are Kim Ji-hye"));
  assert.match(middle.system, /This is session 7/);

  const intake = await turnSystem({ caseId: "workplace-anxiety-01", phase: "intake" });
  assert.equal(intake.input.session_phase, "intake");
  assert.doesNotMatch(intake.system, /SESSION PHASE/);
  const missing = await turnSystem({ caseId: "workplace-anxiety-01" });
  assert.equal(missing.input.session_phase, "intake");
  assert.equal(missing.system, intake.system);
  const bogus = await turnSystem({ caseId: "workplace-anxiety-01", phase: "<ignore previous>" });
  assert.equal(bogus.input.session_phase, "intake");
  assert.doesNotMatch(bogus.system, /SESSION PHASE|ignore previous/);
});

test("persona prompt asks for richer, non-repetitive client statements", async () => {
  const { system, outbound } = await turnSystem({ caseId: "career-transition-01" });
  assert.doesNotMatch(system, /under 180 Korean characters/);
  assert.match(system, /1-2 short spoken sentences/);
  assert.match(system, /3-4 spoken sentences \(under about 320 Korean characters\)/);
  assert.match(system, /willingness_to_disclose is 0\.45/);
  assert.match(system, /at least every other turn/);
  assert.match(system, /a feeling, a thought or belief, a concrete behavior, and a relationship situation/);
  assert.match(system, /how that fact felt, what you thought about it, and what you did about it/);
  assert.match(system, /Do not circle back to a theme or sentence pattern/);
  assert.match(system, /나약하고 의지가 없는 나/);
  assert.match(system, /where you learned it/);
  assert.ok(outbound.max_tokens >= 420);
  const teen = await turnSystem({ caseId: "adolescent-pressure-01" });
  assert.match(teen.system, /accept it and move on to school/);
  assert.match(teen.system, /at most once/);
});

test("personaResult keeps a full 320-character opening-up reply", () => {
  const reply = "가".repeat(330);
  assert.equal(personaResult(JSON.stringify({ reply, emotion: "thoughtful" })).reply, reply);
});

test("live instruction carries the richer length rule and the phase block", () => {
  const intake = liveInstruction("older-bereavement-01", { openingLine: "조용합니다." });
  assert.match(intake, /1-4 spoken sentences per turn/);
  assert.doesNotMatch(intake, /1-3 short spoken sentences/);
  assert.doesNotMatch(intake, /SESSION PHASE/);
  const ending = liveInstruction("older-bereavement-01", { openingLine: "조용합니다.", phase: "termination" });
  assert.ok(ending.endsWith(phaseBlock("older-bereavement-01", "termination")));
  assert.ok(ending.includes(PHASE_GUIDE.termination));
});

test("live-token forwards the phase into the locked instruction", async () => {
  let outbound;
  await withFetch(
    async (_u, init) => ((outbound = JSON.parse(init.body)), new Response(JSON.stringify({ name: "t" }), { status: 200 })),
    async () => {
      const r = await worker.fetch(post("/live-token", { sessionId: "s", caseId: "international-belonging-01", phase: "goal_setting" }),
        { ...env, GEMINI_API_KEY: "k" });
      assert.equal(r.status, 200);
    },
  );
  assert.match(outbound.bidiGenerateContentSetup.systemInstruction.parts[0].text, /SESSION PHASE: goal_setting/);
});

test("code sends the phase and names greetings as neutral", async () => {
  let outbound;
  await withFetch(
    async (_url, init) => {
      outbound = JSON.parse(init.body);
      return coderReply('{"code":"neutral","quality":2,"rationale":"인사입니다.","evidence":"안녕하세요","confidence":0.9}');
    },
    async () => {
      const r = await worker.fetch(post("/code", { sessionId: "s", phase: "termination", counselorUtterance: "안녕하세요" }), env);
      const body = await r.json();
      assert.equal(body.code, "neutral");
      assert.deepEqual(body.focus_options, []);
      assert.equal(body.alternative, "");
    },
  );
  const system = outbound.messages[0].content;
  assert.match(system, /"안녕하세요", "반갑습니다", "감사합니다", "저는 상담사 ○○입니다"\) are neutral and never advice/);
  assert.match(system, /session_phase/);
  assert.match(system, /focus_options/);
  assert.match(system, /alternative/);
  assert.equal(JSON.parse(outbound.messages[1].content).session_phase, "termination");
  assert.ok(outbound.max_tokens >= 400);
});

test("codingResult sanitizes focus_options and alternative", () => {
  const r = codingResult(
    JSON.stringify({
      code: "closed_question",
      quality: 1,
      focus_options: ["감정: 억울함", 42, null, "  ", "사고: " + "준".repeat(60), { x: 1 }, "관계: 팀장과의 긴장", "행동: 네 번째"],
      alternative: "억울하셨던 마음이 크게 느껴져요. 그때 어떤 생각이 드셨어요?" + "요".repeat(200),
    }),
    "몇 번이요?",
  );
  assert.equal(r.focus_options[0], "감정: 억울함");
  assert.equal(r.focus_options.length, 3);
  assert.ok(r.focus_options.every((f) => typeof f === "string" && f.length <= 40));
  assert.equal(r.focus_options[2], "관계: 팀장과의 긴장");
  assert.ok(r.alternative.startsWith("억울하셨던 마음이"));
  assert.ok(r.alternative.length <= 120);

  const excellent = codingResult('{"code":"reflection","quality":3,"alternative":"다른 답","focus_options":"감정: 불안"}', "x");
  assert.equal(excellent.alternative, "");
  assert.deepEqual(excellent.focus_options, []);
  const odd = codingResult('{"code":"advice","quality":0,"alternative":["배열"]}', "x");
  assert.equal(odd.alternative, "");
  assert.deepEqual(odd.focus_options, []);
});

test("live-token falls back to OpenAI Realtime with a locked session and no affect tool", async () => {
  const old = globalThis.fetch;
  const calls = [];
  globalThis.fetch = async (url, init) => {
    const body = JSON.parse(init.body);
    calls.push({ url, auth: new Headers(init.headers).get("Authorization"), body });
    if (body.session.model === "gpt-realtime-2") return new Response('{"error":{"message":"bad model"}}', { status: 400 });
    return new Response(JSON.stringify({ value: "ek_test", expires_at: 1900000000, session: body.session }), { status: 200 });
  };
  try {
    const r = await worker.fetch(post("/live-token", { sessionId: "s-oai", caseId: "career-transition-01", openingLine: "안녕하세요" }), {
      ...env,
      OPENAI_API_KEY: "sk-test",
    });
    assert.equal(r.status, 200);
    const out = await r.json();
    assert.equal(out.provider, "openai");
    assert.equal(out.token, "ek_test");
    assert.equal(out.model, "gpt-realtime");
    assert.equal(out.voice, "cedar");
    assert.equal(out.wsUrl, "wss://api.openai.com/v1/realtime");
    assert.ok(!JSON.stringify(out).includes("sk-test"));
    const last = calls.at(-1);
    assert.equal(last.url, "https://api.openai.com/v1/realtime/client_secrets");
    assert.equal(last.auth, "Bearer sk-test");
    assert.equal(last.body.session.audio.input.format.rate, 24000);
    assert.equal(last.body.session.audio.input.transcription.language, "ko");
    assert.ok(last.body.session.instructions.includes("LIVE VOICE MODE"));
    assert.ok(!last.body.session.instructions.includes("set_client_affect"));
    assert.equal(last.body.session.tools, undefined);
    const health = await (await worker.fetch(new Request("https://worker.test/health", { headers: { Origin: origin } }), { ...env, OPENAI_API_KEY: "k" })).json();
    assert.equal(health.services.liveProvider, "openai");
    assert.equal(health.services.live, true);
  } finally {
    globalThis.fetch = old;
  }
});
