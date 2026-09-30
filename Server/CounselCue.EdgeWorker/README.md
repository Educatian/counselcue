# CounselCue Edge Worker

Server-side proxy for the hosted WebGL demo. API keys and persona prompts stay on the server.

| Route | Purpose |
|---|---|
| `POST /turn` | Case-specific Korean client persona through OpenRouter (`OPENROUTER_MODEL`). Receives the counselor utterance, the bounded relational state, the case's opening line, and up to 8 recent exchanges so the client stays consistent and discloses gradually. |
| `POST /voice` | ElevenLabs v3 client speech with bounded emotion tags. The voice is chosen per case from `ELEVENLABS_VOICE_IDS`, falling back to `ELEVENLABS_VOICE_ID`. |
| `GET /health` | Reports which services are configured, never the secrets themselves. |

## Configuration

Secrets (`wrangler secret put …`): `OPENROUTER_API_KEY`, `ELEVENLABS_API_KEY`.

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
