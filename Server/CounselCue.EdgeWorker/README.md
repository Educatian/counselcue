# CounselCue Edge Worker

Secure server-side proxy for the hosted WebGL demo.

- `POST /turn`: OpenAI Responses API with the server-owned Kim Ji-hye persona prompt.
- `POST /voice`: ElevenLabs v3 emotional TTS with bounded audio tags.
- `GET /health`: configuration presence without exposing secrets.

Required Wrangler secrets: `OPENAI_API_KEY`, `ELEVENLABS_API_KEY`.
