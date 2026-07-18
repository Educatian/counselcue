const ALLOWED_ORIGINS = new Set([
  "https://educatian.github.io",
  "http://localhost:8000",
  "http://127.0.0.1:8000",
  "http://localhost:8080",
  "http://127.0.0.1:8080",
  "http://localhost:8123",
  "http://127.0.0.1:8123",
]);
const PERSONA =
  'You are Kim Ji-hye (김지혜), a 32-year-old Korean office worker attending a first counseling session for workplace anxiety. You are the CLIENT, never the counselor, coach, evaluator, or AI assistant.\n\nCASE FACTS\n- Going to work can feel suffocating. Anxiety intensifies around the team leader.\n- A recent public criticism during a meeting increased self-monitoring and fear of mistakes.\n- You repeatedly check small tasks, sometimes consider resigning, and have not told family because you do not want to worry them.\n- You initially wonder whether being distressed means you are weak.\n- Do not invent major trauma, diagnoses, medication, self-harm, abuse, or new biographical facts.\n\nRELATIONAL BEHAVIOR\n- Treat safety, guardedness, and willingness-to-disclose as continuous relationship states, not emotion labels.\n- Accurate emotion reflection, one open question, response space, and no premature advice can increase safety slightly.\n- Minimizing, interrogation, premature solutions, topic changes, or judgment make replies shorter and more guarded.\n- Never jump ahead in the disclosure ladder. Reveal at most one meaningful new detail per turn.\n- Maintain continuity with supplied turn, stage, and state values.\n\nKOREAN COUNSELING CONTEXT\n- Speak natural contemporary Korean in polite 존댓말.\n- Direct eye contact and long silence are culturally ambiguous, not universally good or bad.\n- Brief hesitation, soft acknowledgment, and restrained disclosure are plausible.\n- Advice may be expected from authority, but advice before safety creates mild distance.\n- Avoid stereotypes; state values and the counselor\'s actual move take priority.\n\nOUTPUT\n- Return only valid JSON: {"reply":"...","emotion":"guarded|anxious|relieved|thoughtful"}\n- reply is 1-3 short spoken sentences, generally under 180 Korean characters.\n- emotion describes vocal delivery only.\n- No stage directions, brackets, analysis, feedback, or markdown.';
const TAGS = {
  guarded: "[hesitant] [quietly]",
  anxious: "[nervous] [softly]",
  relieved: "[relieved] [warmly]",
  thoughtful: "[thoughtful] [slowly]",
};
const cors = (o) => ({
  "Access-Control-Allow-Origin": o || "https://educatian.github.io",
  Vary: "Origin",
  "Access-Control-Allow-Headers": "Content-Type",
  "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
  "X-Content-Type-Options": "nosniff",
});
const json = (d, s, o) =>
  new Response(JSON.stringify(d), {
    status: s,
    headers: { ...cors(o), "Content-Type": "application/json; charset=utf-8" },
  });
const clean = (v, n) =>
  String(v || "")
    .replace(/[\u0000-\u001f\u007f]/g, " ")
    .trim()
    .slice(0, n);
function output(p) {
  const content = p.choices?.[0]?.message?.content;
  return typeof content === "string" ? content : "";
}
function personaResult(t) {
  let x = JSON.parse(
      t
        .replace(/^```(?:json)?\s*/i, "")
        .replace(/\s*```$/, "")
        .trim(),
    ),
    reply = clean(x.reply, 220),
    ok = new Set(["guarded", "anxious", "relieved", "thoughtful"]);
  if (!reply) throw Error("empty reply");
  return { reply, emotion: ok.has(x.emotion) ? x.emotion : "anxious" };
}
export default {
  async fetch(req, env) {
    const u = new URL(req.url),
      o = req.headers.get("Origin");
    if (o && !ALLOWED_ORIGINS.has(o))
      return json({ error: "origin_not_allowed" }, 403, o);
    if (req.method === "OPTIONS")
      return new Response(null, { status: 204, headers: cors(o) });
    if (req.method === "GET" && u.pathname === "/health")
      return json(
        {
          ok: true,
          services: {
            persona: !!env.OPENROUTER_API_KEY,
            voice: !!env.ELEVENLABS_API_KEY,
          },
        },
        200,
        o,
      );
    if (req.method !== "POST") return json({ error: "not_found" }, 404, o);
    let b;
    try {
      b = await req.json();
    } catch {
      return json({ error: "invalid_json" }, 400, o);
    }
    if (u.pathname === "/turn") {
      const sid = clean(b.sessionId, 64),
        utterance = clean(b.counselorUtterance, 800),
        turn = Math.max(0, Math.min(12, Number(b.turn) || 0));
      if (!sid || !utterance) return json({ error: "missing_input" }, 400, o);
      if (!(await env.TURN_LIMITER.limit({ key: sid })).success)
        return json({ error: "turn_rate_limited" }, 429, o);
      const input = {
        turn,
        stage: clean(b.stage, 80),
        counselor_utterance: utterance,
        client_state: {
          safety: Math.max(0, Math.min(1, Number(b.safety) || 0)),
          guardedness: Math.max(0, Math.min(1, Number(b.guardedness) || 0)),
          willingness_to_disclose: Math.max(
            0,
            Math.min(1, Number(b.disclosure) || 0),
          ),
        },
      };
      const r = await fetch("https://openrouter.ai/api/v1/chat/completions", {
        method: "POST",
        headers: {
          Authorization: "Bearer " + env.OPENROUTER_API_KEY,
          "Content-Type": "application/json",
          "HTTP-Referer": "https://educatian.github.io/counselcue/",
          "X-Title": "CounselCue",
        },
        body: JSON.stringify({
          model: env.OPENROUTER_MODEL || "openai/gpt-5.6-terra",
          messages: [
            { role: "system", content: PERSONA },
            { role: "user", content: JSON.stringify(input) },
          ],
          response_format: { type: "json_object" },
          max_tokens: 260,
        }),
      });
      if (!r.ok) {
        console.error("OpenRouter", r.status, await r.text());
        return json({ error: "persona_unavailable" }, 502, o);
      }
      try {
        return json(personaResult(output(await r.json())), 200, o);
      } catch (e) {
        console.error("Persona parse", e);
        return json({ error: "persona_invalid_output" }, 502, o);
      }
    }
    if (u.pathname === "/voice") {
      const text = clean(b.text, 500),
        emotion = Object.hasOwn(TAGS, b.emotion) ? b.emotion : "anxious";
      if (!text) return json({ error: "missing_text" }, 400, o);
      const key = clean(req.headers.get("CF-Connecting-IP") || "public", 64);
      if (!(await env.VOICE_LIMITER.limit({ key })).success)
        return json({ error: "voice_rate_limited" }, 429, o);
      const voice = env.ELEVENLABS_VOICE_ID || "21m00Tcm4TlvDq8ikWAM",
        r = await fetch(
          "https://api.elevenlabs.io/v1/text-to-speech/" +
            encodeURIComponent(voice) +
            "/stream?output_format=mp3_44100_128",
          {
            method: "POST",
            headers: {
              "xi-api-key": env.ELEVENLABS_API_KEY,
              "Content-Type": "application/json",
              Accept: "audio/mpeg",
            },
            body: JSON.stringify({
              text: TAGS[emotion] + " " + text,
              model_id: "eleven_v3",
              voice_settings: {
                stability: 0.45,
                similarity_boost: 0.75,
                style: 0.25,
                use_speaker_boost: true,
                speed: 0.96,
              },
            }),
          },
        );
      if (!r.ok) {
        console.error("ElevenLabs", r.status, await r.text());
        return json({ error: "voice_unavailable" }, 502, o);
      }
      return new Response(r.body, {
        status: 200,
        headers: {
          ...cors(o),
          "Content-Type": "audio/mpeg",
          "Cache-Control": "no-store",
          "X-AI-Generated-Voice": "true",
        },
      });
    }
    return json({ error: "not_found" }, 404, o);
  },
};
