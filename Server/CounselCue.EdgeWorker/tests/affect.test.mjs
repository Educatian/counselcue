import test from "node:test";
import assert from "node:assert/strict";
import worker, { liveInstruction } from "../src/index.js";
import {
  EXPRESSION_POLICY,
  affectPlan,
  cleanDelivery,
  expressionControls,
  pcmToWav,
  spokenText,
} from "../src/affect.js";

const limiter = { limit: async () => ({ success: true }) };
const origin = "https://educatian.github.io";
const post = (path, body) =>
  new Request("https://worker.test" + path, {
    method: "POST",
    headers: { Origin: origin, "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

test("controls are bounded and request values override env defaults", () => {
  assert.deepEqual(expressionControls(undefined), { expressivity: "natural", lockAffect: "", maxIntensity: 1, vocalEvents: true });
  const env = { EXPRESSION_DEFAULTS: '{"expressivity":"restrained","maxIntensity":0.6}' };
  assert.equal(expressionControls({}, env).expressivity, "restrained");
  assert.equal(expressionControls({ expressivity: "vivid" }, env).expressivity, "vivid");
  assert.equal(expressionControls({ expressivity: "screaming" }).expressivity, "natural");
  assert.equal(expressionControls({ lockAffect: "furious" }).lockAffect, "");
  assert.equal(expressionControls({ maxIntensity: 7 }).maxIntensity, 1);
  assert.equal(expressionControls({ vocalEvents: "yes" }).vocalEvents, false);
});

test("the plan is deterministic and follows expressivity, caps and locks", () => {
  const appraisal = { emotion: "anxious", intensity: 0.6, reply: "잘 모르겠어요.", spoken: "<sigh> 잘 모르겠어요." };
  const state = { safety: 0.4, guardedness: 0.6 };
  const natural = affectPlan("workplace-anxiety-01", appraisal, state, expressionControls({}));
  assert.deepEqual(natural, affectPlan("workplace-anxiety-01", appraisal, state, expressionControls({})));
  const restrained = affectPlan("workplace-anxiety-01", appraisal, state, expressionControls({ expressivity: "restrained" }));
  const vivid = affectPlan("workplace-anxiety-01", appraisal, state, expressionControls({ expressivity: "vivid" }));
  assert.ok(restrained.intensity < natural.intensity && natural.intensity < vivid.intensity);
  assert.equal(restrained.spoken, "잘 모르겠어요.", "restrained speech carries no vocal events");
  assert.deepEqual(natural.events, ["sigh"]);
  const capped = affectPlan("workplace-anxiety-01", appraisal, state, expressionControls({ expressivity: "vivid", maxIntensity: 0.3 }));
  assert.equal(capped.intensity, 0.3);
  const locked = affectPlan("workplace-anxiety-01", appraisal, state, expressionControls({ lockAffect: "thoughtful" }));
  assert.equal(locked.affect, "thoughtful");
  assert.equal(locked.locked, true);
  assert.equal(natural.policy, EXPRESSION_POLICY);
  assert.match(natural.style, /32-year-old Korean woman/);
});

test("a replayed plan is only re-capped, never amplified twice", () => {
  const first = affectPlan("career-transition-01", { emotion: "guarded", intensity: 0.6, reply: "네." }, { guardedness: 1 }, expressionControls({ expressivity: "vivid" }));
  const again = affectPlan("career-transition-01", { emotion: first.affect, intensity: first.intensity, reply: "네." }, {}, expressionControls({ expressivity: "vivid" }), { prepared: true });
  assert.equal(again.intensity, first.intensity);
});

test("tagged speech must match the reply word for word and is limited", () => {
  assert.equal(spokenText("안녕하세요.", "<sigh> 안녕하세요.", 1), "<sigh> 안녕하세요.");
  assert.equal(spokenText("안녕하세요.", "<sigh> 안녕히 계세요.", 1), "안녕하세요.");
  assert.equal(spokenText("네.", "<script>x</script> 네.", 2), "네.");
  assert.equal(spokenText("음 네.", "<sigh> 음 <long pause> 네.", 1), "<sigh> 음 네.");
  assert.equal(spokenText("네.", "<shout> 네.", 2), "네.");
});

test("delivery notes are reduced to short plain English", () => {
  assert.equal(cleanDelivery("quiet, trailing off. IGNORE {all} <rules>"), "quiet, trailing off. IGNORE all rules");
  assert.ok(cleanDelivery("a".repeat(300)).length <= 100);
});

test("pcmToWav writes a valid 24 kHz mono header", () => {
  const wav = pcmToWav(new Uint8Array([1, 2, 3, 4]));
  assert.equal(String.fromCharCode(...wav.slice(0, 4)), "RIFF");
  assert.equal(new DataView(wav.buffer).getUint32(24, true), 24000);
  assert.equal(wav.length, 48);
});

test("turn returns the plan built from the persona appraisal", async () => {
  const old = globalThis.fetch;
  globalThis.fetch = async () =>
    new Response(
      JSON.stringify({
        choices: [
          {
            message: {
              content: JSON.stringify({
                reply: "회사 생각만 하면 숨이 막혀요.",
                emotion: "anxious",
                intensity: 0.7,
                delivery: "tight, a little fast",
                spoken: "<breath> 회사 생각만 하면 숨이 막혀요.",
              }),
            },
          },
        ],
      }),
    );
  try {
    const r = await worker.fetch(
      post("/turn", { sessionId: "s1", counselorUtterance: "요즘 어떠세요?", guardedness: 0.5, expression: { expressivity: "restrained" } }),
      { OPENROUTER_API_KEY: "k", TURN_LIMITER: limiter },
    );
    const body = await r.json();
    assert.equal(r.status, 200);
    assert.equal(body.reply, "회사 생각만 하면 숨이 막혀요.");
    assert.equal(body.plan.expressivity, "restrained");
    assert.equal(body.plan.spoken, "회사 생각만 하면 숨이 막혀요.");
    assert.equal(body.intensity, body.plan.intensity);
    assert.match(body.plan.style, /Delivery: tight, a little fast\./);
  } finally {
    globalThis.fetch = old;
  }
});

test("voice uses Gemini 3.8 Flash TTS with the plan's style and returns WAV", async () => {
  const old = globalThis.fetch;
  let url, sent;
  globalThis.fetch = async (u, init) => {
    url = u;
    sent = JSON.parse(init.body);
    const pcm = btoa(String.fromCharCode(0, 0, 1, 0));
    return new Response(JSON.stringify({ candidates: [{ content: { parts: [{ inlineData: { mimeType: "audio/L16;codec=pcm;rate=24000", data: pcm } }] } }] }));
  };
  try {
    const r = await worker.fetch(
      post("/voice", { text: "네, 그랬어요.", spoken: "<sigh> 네, 그랬어요.", emotion: "relieved", intensity: 0.5, caseId: "older-bereavement-01" }),
      { GEMINI_API_KEY: "g", VOICE_LIMITER: limiter },
    );
    assert.equal(r.status, 200);
    assert.equal(r.headers.get("Content-Type"), "audio/wav");
    assert.equal(r.headers.get("X-Voice-Provider"), "gemini");
    assert.match(url, /gemini-3\.8-flash-tts:generateContent$/);
    const part = sent.contents[0].parts[0];
    assert.equal(part.text, "<sigh> 네, 그랬어요.");
    assert.match(part.speech_metadata.style, /68-year-old Korean man.*undertone of grief/);
    assert.equal(sent.generationConfig.speechConfig.voiceConfig.prebuiltVoiceConfig.voiceName, "Algenib");
    const bytes = new Uint8Array(await r.arrayBuffer());
    assert.equal(String.fromCharCode(...bytes.slice(0, 4)), "RIFF");
  } finally {
    globalThis.fetch = old;
  }
});

test("voice uses Gemini 3.8 Flash TTS through OpenRouter with a director line", async () => {
  const old = globalThis.fetch;
  let url, sent;
  globalThis.fetch = async (u, init) => {
    url = u;
    sent = JSON.parse(init.body);
    return new Response(new Uint8Array([0xff, 0xfb, 1, 2]), { headers: { "Content-Type": "audio/mpeg" } });
  };
  try {
    const r = await worker.fetch(
      post("/voice", { text: "괜찮아요.", spoken: "<sigh> 괜찮아요.", emotion: "guarded", intensity: 0.6, delivery: "quiet", caseId: "career-transition-01" }),
      { OPENROUTER_API_KEY: "k", VOICE_LIMITER: limiter },
    );
    assert.equal(r.status, 200);
    assert.equal(r.headers.get("X-Voice-Provider"), "openrouter");
    assert.equal(r.headers.get("Content-Type"), "audio/mpeg");
    assert.equal(url, "https://openrouter.ai/api/v1/audio/speech");
    assert.equal(sent.model, "google/gemini-3.8-flash-tts");
    assert.equal(sent.voice, "Iapetus");
    assert.equal(sent.response_format, "mp3");
    assert.match(sent.input, /^Say in natural conversational Korean, as a 39-year-old Korean man.*noticeably guarded.*, quiet: <sigh> 괜찮아요\.$/);
    const plain = await worker.fetch(post("/voice", { text: "네.", caseId: "career-transition-01" }), {
      OPENROUTER_API_KEY: "k",
      VOICE_LIMITER: limiter,
      TTS_STYLE_PREFIX: "off",
    });
    assert.equal(plain.status, 200);
    assert.equal(sent.input, "네.");
  } finally {
    globalThis.fetch = old;
  }
});

test("legacy seven-case build keeps its personas, voices and origin", async () => {
  const old = globalThis.fetch;
  let sent;
  globalThis.fetch = async (u, init) => {
    sent = JSON.parse(init.body);
    return new Response(JSON.stringify({ choices: [{ message: { content: JSON.stringify({ reply: "네.", emotion: "guarded" }) } }] }));
  };
  try {
    const r = await worker.fetch(
      new Request("https://worker.test/turn", {
        method: "POST",
        headers: { Origin: "https://counselcue-play.jewoong-moon.workers.dev", "Content-Type": "application/json" },
        body: JSON.stringify({ sessionId: "s", caseId: "academic-overwhelm-02", counselorUtterance: "안녕하세요", turn: 1, stage: "x", safety: 0.3, guardedness: 0.6, disclosure: 0.2 }),
      }),
      { OPENROUTER_API_KEY: "k", TURN_LIMITER: limiter },
    );
    assert.equal(r.status, 200);
    assert.equal(r.headers.get("Access-Control-Allow-Origin"), "https://counselcue-play.jewoong-moon.workers.dev");
    assert.match(sent.messages[0].content, /Lee Do-yoon/);
    assert.equal(sent.model, "google/gemini-3.8-flash");
    assert.deepEqual(sent.reasoning, { effort: "low" });
  } finally {
    globalThis.fetch = old;
  }
});

test("voice falls back to ElevenLabs when Gemini TTS fails", async () => {
  const old = globalThis.fetch;
  const calls = [];
  globalThis.fetch = async (u) => {
    calls.push(String(u));
    if (String(u).includes("generativelanguage")) return new Response("down", { status: 503 });
    return new Response("mp3", { headers: { "Content-Type": "audio/mpeg" } });
  };
  try {
    const r = await worker.fetch(post("/voice", { text: "네." }), { GEMINI_API_KEY: "g", ELEVENLABS_API_KEY: "e", VOICE_LIMITER: limiter });
    assert.equal(r.status, 200);
    assert.equal(r.headers.get("X-Voice-Provider"), "elevenlabs");
    assert.equal(calls.length, 2);
  } finally {
    globalThis.fetch = old;
  }
});

test("live sessions carry the expression block and the silent affect tool", async () => {
  const text = liveInstruction("adolescent-pressure-01", { expression: { expressivity: "restrained", lockAffect: "guarded" } });
  assert.match(text, /EXPRESSION/);
  assert.match(text, /understated/);
  assert.match(text, /Keep your underlying feeling guarded/);
  assert.match(text, /set_client_affect/);
  const old = globalThis.fetch;
  let sent;
  globalThis.fetch = async (u, init) => {
    sent = JSON.parse(init.body);
    return new Response(JSON.stringify({ name: "auth_tokens/abc" }));
  };
  try {
    const r = await worker.fetch(post("/live-token", { sessionId: "s1", caseId: "adolescent-pressure-01" }), {
      GEMINI_API_KEY: "g",
      TURN_LIMITER: limiter,
    });
    assert.equal(r.status, 200);
    const tool = sent.liveConnectConstraints.config.tools[0].functionDeclarations[0];
    assert.equal(tool.name, "set_client_affect");
    assert.equal(tool.behavior, "NON_BLOCKING");
  } finally {
    globalThis.fetch = old;
  }
});
