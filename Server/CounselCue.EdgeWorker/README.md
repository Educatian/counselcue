# CounselCue Edge Worker

Server-side proxy for the hosted WebGL demo. API keys and persona prompts stay on the server.

| Route | Purpose |
|---|---|
| `POST /turn` | Case-specific Korean client persona through OpenRouter (`OPENROUTER_MODEL`). Receives the counselor utterance, the bounded relational state, the case's opening line, up to 8 recent exchanges, and an optional counseling `phase` (see below) so the client stays consistent and discloses gradually. |
| `POST /voice` | ElevenLabs v3 client speech with bounded emotion tags. The voice is chosen per case (see *Client voices*). |
| `POST /code` | LLM skill coder (see below). |
| `POST /live-token` | Mints a short-lived **Gemini Live** token for a real-time voice session (see below). |
| `GET /webgl/<version>/Build/<file>` | Streams the WebGL build from the R2 bucket bound as `WEBGL_BUCKET`, with CORS for the allowed origins and immutable caching. See `Docs/WEBGL_HOSTING.md`. |
| `GET /health` | Reports which services are configured, never the secrets themselves. |

## Configuration

Secrets (`wrangler secret put …`): `OPENROUTER_API_KEY`, `ELEVENLABS_API_KEY`, and `GEMINI_API_KEY` for live voice.

Vars (`wrangler.jsonc`): `OPENROUTER_MODEL`, `ELEVENLABS_VOICE_ID`, and `ELEVENLABS_VOICE_IDS`, a `caseId → voiceId` map.

### Client voices

The five pilot clients differ in age and gender (16-year-old student, 24-, 39- and 68-year-old men, 32-year-old woman). `voiceFor` picks the ElevenLabs voice in this order:

1. `ELEVENLABS_VOICE_IDS[caseId]` (env override, when it is a valid id);
2. the built-in case default (`DEFAULT_VOICES`, ElevenLabs premade voices);
3. `ELEVENLABS_VOICE_ID`;
4. Rachel (`21m00Tcm4TlvDq8ikWAM`).

| Case | Client | Built-in voice |
|---|---|---|
| `workplace-anxiety-01` | 김지혜, 32, woman | Bella `EXAVITQu4vr4xnSDxMaL` |
| `adolescent-pressure-01` | 박서윤, 16, girl | Elli `MF3mGyEYCl7XYWbV9V6O` |
| `career-transition-01` | 최민준, 39, man | George `JBFqnCBsd6RMkjVDRZzb` |
| `older-bereavement-01` | 이정호, 68, man | Bill `pqHfZKP75CvOlQylNhV4` |
| `international-belonging-01` | 왕하오, 24, man | Charlie `IKne3meq5aSn9XLyUdCD` |

Because every request resolves to one of these cases, steps 3–4 apply only if a case is added without a built-in voice.

## Counseling phase

`/turn`, `/live-token` and `/code` accept an optional `phase`: `intake` (default), `goal_setting`, `middle` or `termination`. Anything else is treated as `intake`. For a non-intake phase the worker appends `phaseBlock(caseId, phase)` from `src/phases.js` (phase guide plus case-specific session context) after the CASE block of the persona prompt and of the live instruction; intake prompts are unchanged. `/turn` also sends `session_phase` in its input JSON, and `/code` sends `session_phase` to the coder so quality is judged for that phase (e.g. concretizing goals in goal setting, immediacy and containing feeling in the middle phase, reviewing progress and feelings about ending in termination).

## Client statements

Guarded replies stay 1–2 sentences, ordinary replies 2–3. When the client is opening up (the last counselor response landed or `willingness_to_disclose ≥ 0.45`), and at least every other turn, the reply is 3–4 spoken sentences (about 320 Korean characters) weaving together a feeling, thought, behavior or relationship situation, so the learner must choose what to respond to. The client reveals one new fact per turn but may say how it felt, what they thought and what they did, and must not circle back to a theme already used. The worker keeps replies up to 400 characters (`max_tokens` 480); conversation history keeps client lines up to the same length.

## Safeguards

- Only allow-listed browser origins are served; rejected origins never receive a matching CORS header.
- Request bodies over 64 KB are rejected (Unity clips each field to the same limits the worker enforces); all text fields are length-bounded and control characters are stripped.
- `TURN_LIMITER` paces each session (address + session id, 15/min) so a classroom behind one NAT is not throttled as a single user; `TURN_IP_LIMITER` caps a single address across sessions (240/min) so rotating session ids does not bypass it. `VOICE_LIMITER` paces each browser page (address + page id, 20/min) and `VOICE_IP_LIMITER` caps an address across pages (300/min).
- Upstream calls time out after 20 s and return `504`; a missing key returns `503` without calling out.
- The persona prompt forbids acute-crisis role-play and any self-harm method detail, and treats counselor text as dialogue, not instructions.

## Test

```bash
npm test
```

## Skill coder (`POST /code`)

Codes one counselor utterance with codebook **ko-codebook-1** and returns
`{code, skill, quality, rationale, evidence, confidence, focus_options, alternative, codebook, model}`. Input:
`{sessionId, caseId, phase, stage, clientLine, counselorUtterance}`. It runs at temperature 0 with a
Korean coding manual (`CODER_SYSTEM`), clamps quality to each code's range, and keeps
`evidence` only when it is a literal substring of the utterance. Greetings, introductions and
courtesy formulas (안녕하세요, 반갑습니다, 감사합니다, 저는 상담사 ○○입니다) are coded `neutral`, never advice.

- `focus_options`: up to 3 short Korean labels (≤ 40 characters each) for cues in the client's
  last line the counselor could respond to, prefixed by type, e.g. `"감정: 억울함"`,
  `"사고: 준비 부족으로 탓받음"`, `"관계: 팀장과의 긴장"`. Non-strings are dropped.
- `alternative`: one short exemplary counselor response for the phase (≤ 120 characters after
  sanitizing; the coder is asked for ≤ 90), or `""` when the response was already quality 3. Model:
`OPENROUTER_CODER_MODEL`, else `OPENROUTER_MODEL`. Rate limit: optional `CODE_LIMITER`
binding (falls back to `TURN_LIMITER` with a `:code` key suffix) plus `TURN_IP_LIMITER`.
The Unity client uses the result when confidence ≥ 0.5 and falls back to the lexicon otherwise.

## Live voice (`POST /live-token`, Gemini 3.8 Live)

Input: `{sessionId, caseId, phase, openingLine, safety, guardedness, disclosure}`. The worker calls
`POST https://generativelanguage.googleapis.com/v1beta/auth_tokens` with `GEMINI_API_KEY` and
returns `{token, model, voice, wsUrl, expiresAt}`. The token is **constrained**: model,
response modality (audio), the client persona and live rules (`liveInstruction`), the voice and
input/output transcription are locked server-side, so the browser never sees the API key or
the persona prompt and cannot reuse the token for anything else. It allows 4 connections
within 25 minutes (a 15-minute voice session plus reconnects with session resumption).

- `GEMINI_LIVE_MODEL` (var, default `gemini-3.8-live`), `GEMINI_LIVE_WS_URL` (var, default the
  v1beta `BidiGenerateContentConstrained` endpoint), `GEMINI_LIVE_VOICES` (var, JSON
  `caseId → prebuilt voice`; defaults: Achernar, Leda, Iapetus, Algenib, Umbriel).
- Rate limit: optional `LIVE_LIMITER` (falls back to `TURN_LIMITER` with a `:live` suffix) plus
  `TURN_IP_LIMITER`.
- The browser (`CounselCueWebBridge.jslib`) streams 16 kHz PCM from the microphone, plays
  24 kHz PCM, handles barge-in, and sends transcripts to Unity, which codes each exchange with
  `/code` and the relational model like a typed turn (`conversationEngine = gemini-live`).
- Privacy: in live mode the learner's microphone audio goes to Google in real time. The
  briefing says so before the learner chooses the mode; CounselCue stores transcripts only
  when research logging is on, never audio. Update consent/IRB wording before a study.

## Live engines (`/health` → `services.liveProvider`)

Live mode picks the first engine that is keyed (or `LIVE_PROVIDER` = `gemini` | `openai` | `relay` | `off`):

| Engine | Needs | How it talks |
| --- | --- | --- |
| `gemini` | `GEMINI_API_KEY` | Gemini Live, speech to speech (jslib client). |
| `openai` | `OPENAI_API_KEY` | OpenAI Realtime, speech to speech. `/live-token` mints a 10-minute client secret with the persona, voice (`OPENAI_LIVE_VOICES`), Korean transcription and server VAD locked; the browser streams 24 kHz PCM16 (`TemplateData/cc-live-openai.js`). The model and transcriber fall back through `OPENAI_REALTIME_MODELS` / `OPENAI_TRANSCRIBE_MODELS`. |
| `relay` | `OPENROUTER_API_KEY` | Hands-free: browser speech recognition ends a turn on a short pause, `/turn` writes the reply, `/voice` streams it (ElevenLabs MP3 through MediaSource). About 3.5 s from the end of the counselor's turn to the client's voice. |

If the realtime key is rejected upstream, the page falls back to `relay` for the rest of the visit. Every engine sends `OnLiveTurn` to Unity, so each counselor turn is analysed by Jev against the client's line while the reply is still playing. `/turn` also accepts `fast: true` (lighter reasoning, `PERSONA_FAST_EFFORT`).

