# Expression pipeline (Gemini 3.8)

One decision about how the client feels drives the voice, the face and the research record. A study can control that decision.

```
counselor turn
   │
   ▼
1 Appraisal: persona LLM (/turn)
  reply, emotion, intensity 0–1, delivery note, tagged "spoken" text
   │
   ▼
2 Control layer: Server/CounselCue.EdgeWorker/src/affect.js (deterministic, versioned "expr-1")
  · expressivity gain: restrained 0.65 · natural 1.0 · vivid 1.3
  · relationship nudge: guardedness raises tense affect, safety lets relief show
  · researcher caps and locks: maxIntensity, lockAffect, vocalEvents
  · vocal events: ≤0/1/2 of <sigh> <breath> <short pause> <long pause> <chuckle>,
    and only when the tagged text matches the reply word for word
  · TTS style: case voice ("a 68-year-old Korean man, slow and soft-spoken") + affect tone
    scaled by intensity + the model's delivery note (plain English, ≤100 chars)
   │  AffectPlan { policy, affect, intensity, valence, arousal, style, spoken, events, … }
   ▼
3 Renderers
  · Voice: /voice → Gemini 3.8 Flash TTS. Default route: OpenRouter
    (google/gemini-3.8-flash-tts, /api/v1/audio/speech, MP3), with the plan in a "Say …:" director
    line followed by the tagged words. With GEMINI_API_KEY: the Gemini API directly, with
    speech_metadata.style (WAV). Both use the case's prebuilt Gemini voice (the same as live mode).
    Falls back to ElevenLabs v3 on failure.
  · Face: ClientAvatarController.SetAffect + SetAffectIntensity: the FACS layer's gain
    (Docs/FACIAL_BEHAVIOR.md).
  · Record: clientAffect, clientAffectIntensity, expressionPolicy, expressivity (DATA_SCHEMA.md).
```

## Real-time voice (Gemini 3.8 Live)
Gemini 3.8 Live has no affective-dialog switch; Google removed `enable_affective_dialog` for this model. Expression is instead controlled in two ways:
1. **Instruction.** The locked ephemeral-token instruction carries an `EXPRESSION` block from the same controls: case voice, expressivity guidance, any locked affect or cap, and whether audible sighs are allowed.
2. **Silent affect reports.** The session declares the `set_client_affect(affect, intensity)` tool with `behavior: NON_BLOCKING`. The persona calls it at the start of each spoken turn. The browser answers with `scheduling: SILENT`, so speech never waits for it, and forwards the report to Unity (`OnLiveAffect`). The face follows the voice in real time, and the record logs `expressionPolicy = live-report`.

## Controls
| Who | Where | What |
|---|---|---|
| Instructor or learner | Briefing card, 감정 표현 row | Restrained, natural or vivid (saved per browser) |
| Researcher | Page URL: `?expressivity=restrained&lockAffect=guarded&maxIntensity=0.6&vocalEvents=0` | Fixes the settings for a study and locks the briefing control |
| Operator | Worker vars: `EXPRESSION_DEFAULTS` (JSON), `VOICE_PROVIDER` (`gemini` or `elevenlabs`), `GEMINI_TTS_MODEL` | Server-wide defaults and voice provider |

Every value is bounded on the server. The browser never sends a free-form prompt to the TTS model: `/voice` re-derives the style from the plan with the same policy and re-validates the tagged text.

## Models and secrets
- `OPENROUTER_API_KEY` covers everything except Live:
  - persona and coder: `google/gemini-3.8-flash`, with `reasoning.effort = low`; override with `PERSONA_MODEL` / `CODER_MODEL`;
  - voice: `google/gemini-3.8-flash-tts`; override with `OPENROUTER_TTS_MODEL`; `TTS_STYLE_PREFIX=off` sends the words alone.
- `GEMINI_API_KEY` is needed only for Gemini 3.8 Live, since OpenRouter does not relay the Live WebSocket. Without it, the briefing's live mode falls back to text.
- `ELEVENLABS_API_KEY`: optional fallback voice.

## Earlier seven-case build
`src/legacy.js` keeps the personas of the earlier seven-case WebGL build (counselcue-play / counselcue-webgl), so that build keeps working against this worker. It now also uses Gemini 3.8 and the expression policy.
