# Real-time analysis with Jev

[Jev](https://openrouter.ai/docs/guides/community/jev) (TypeSafe's "System 1" decision model) answers typed questions with calibrated probabilities in about 0.1–0.5 s instead of generating text. CounselCue reaches it through OpenRouter's Decisions API (`typesafe/jev-1.13`) on the same `OPENROUTER_API_KEY`.

## What it decides each counselor turn (`POST /analyze`, `src/jev.js`)
| Question | Type | Used for |
|---|---|---|
| `skill` | choice over the 10 codes of ko-codebook-1 (same codes as `/code` and Unity) | Provisional skill code with probabilities |
| `quality` | score 0–3 (same anchors as the LLM coder) | Provisional quality |
| `attends_to_feeling` | yes/no probability | Whether the counselor acknowledged the client's feeling |
| `client_affect`, `client_intensity` | choice and score, only when the client's reply is known (live mode) | The face follows the live voice even without a `set_client_affect` report |

## How the session uses it
`/analyze` and `/code` start together.
1. Jev usually answers first. Coaching mode then shows `실시간 분석 · 감정 반영 + 탐색 · 확신도 81% · 정밀 분석 중…`.
2. The LLM coder then gives the final code, rationale, focus cues and alternative response.

| Mode (`?analysis=`) | Behaviour |
|---|---|
| `shadow` (default) | The LLM coder decides; Jev is provisional and recorded. This is the right setting until Jev's Korean agreement is known. |
| `jev` | Jev's code decides the turn when its calibrated confidence is at or above `jevMin` (default 0.75). The LLM's rationale is kept when it agrees. |
| `off` | No Jev calls. |

The research record gets `analysisMode`, `jevCode`, `jevConfidence`, `jevQuality`, `jevAttendsToFeeling`, `jevLatencyMs`, `jevModel` and `jevClientAffect`. Comparing them with `skillCode` and `codingSource` gives Jev's agreement with the LLM coder on every real turn.

## Validation before relying on it
`WORKER_URL=https://counselcue-api.jewoong-moon.workers.dev node eval/coding-agreement.mjs` adds two pairs, `jev_vs_A` (against the reference labels) and `jev_vs_llm`, and reports Jev's median and p95 latency and its calibration error (ECE). Adopt `jev` mode only once it matches or beats the LLM coder's κ on the expert-coded set. The current set is a draft seed (`eval/coding/README.md`).

Caveats:
- Jev's Korean performance is not published; these measurements are the evidence.
- Counselor text goes to OpenRouter and TypeSafe, the same data flow as the LLM coder. Check that the consent text covers it.
- Pin the model (`JEV_MODEL`) for a study; the `-latest` alias changes without notice.
