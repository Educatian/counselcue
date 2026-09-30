# CounselCue Edge Worker

Server-side proxy for the hosted WebGL demo. API keys and persona prompts stay on the server.

| Route | Purpose |
|---|---|
| `POST /turn` | Case-specific Korean client persona through OpenRouter (`OPENROUTER_MODEL`). Receives the counselor utterance, the bounded relational state, the case's opening line, and up to 8 recent exchanges so the client stays consistent and discloses gradually. |
| `POST /voice` | ElevenLabs v3 client speech with bounded emotion tags. The voice is chosen per case from `ELEVENLABS_VOICE_IDS`, falling back to `ELEVENLABS_VOICE_ID`. |
| `POST /code` | LLM skill coder (see below). |
| `POST /live-token` | Mints a short-lived **Gemini Live** token for a real-time voice session (see below). |
| `GET /health` | Reports which services are configured, never the secrets themselves. |

## Configuration

Secrets (`wrangler secret put …`): `OPENROUTER_API_KEY`, `ELEVENLABS_API_KEY`, and `GEMINI_API_KEY` for live voice.

Vars (`wrangler.jsonc`): `OPENROUTER_MODEL`, `ELEVENLABS_VOICE_ID`, and `ELEVENLABS_VOICE_IDS`, a `caseId → voiceId` map. The five pilot clients differ in age and gender (16-year-old student, 24-, 39- and 68-year-old men, 32-year-old woman), so assign a matching voice to each case before a pilot.

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
`{code, skill, quality, rationale, evidence, confidence, codebook, model}`. Input:
`{sessionId, caseId, stage, clientLine, counselorUtterance}`. It runs at temperature 0 with a
Korean coding manual (`CODER_SYSTEM`), clamps quality to each code's range, and keeps
`evidence` only when it is a literal substring of the utterance. Model:
`OPENROUTER_CODER_MODEL`, else `OPENROUTER_MODEL`. Rate limit: optional `CODE_LIMITER`
binding (falls back to `TURN_LIMITER` with a `:code` key suffix) plus `TURN_IP_LIMITER`.
The Unity client uses the result when confidence ≥ 0.5 and falls back to the lexicon otherwise.

## Live voice (`POST /live-token`, Gemini 3.8 Live)

Input: `{sessionId, caseId, openingLine, safety, guardedness, disclosure}`. The worker calls
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
