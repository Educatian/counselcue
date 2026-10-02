# CounselCue local research data (schema v3)

CounselCue writes three JSON Lines files to `Application.persistentDataPath` **only after the learner opts in** on the briefing card ("연구용 로컬 기록에 동의합니다"). "Delete local records" removes all three files. Raw webcam video and audio are never stored. On WebGL the files live in the browser's IndexedDB-backed file system for that site.

| File | One line per | Written by |
|---|---|---|
| `counseling-sessions.jsonl` | counselor turn | `CounselingSessionController.WriteRecord` |
| `counseling-session-summaries.jsonl` | finished session (full, focused, evaluation, replay) | `CounselingSessionOrchestrator.WriteSummary` |
| `counseling-self-assessments.jsonl` | learner self-assessment of a scene | `CounselingReflectionController.SaveAssessment` |

Join the three files on `sessionId`. A scene replay starts a new `sessionId`; its summary records `replaySourceTurn`.

## Common fields

| Field | Type | Notes |
|---|---|---|
| `schemaVersion` | int | `3` since skill-coding provenance was added; `2` added `caseId`/`appVersion`; records without it are v1. |
| `sessionId` | string | Random GUID per session; not linked to any identity. |
| `timestampUtc` | string | ISO 8601, UTC. |
| `appVersion` | string | Unity `Application.version` (turn and summary records). |

## `counseling-sessions.jsonl` (turn)

| Field | Meaning |
|---|---|
| `caseId` | Case identifier, e.g. `older-bereavement-01`. |
| `lexiconVersion` | Version of the micro-skill term lists in use (`SkillLexicon.version`; expert overrides set their own). |
| `trainingMode`, `sessionStage`, `sessionElapsedSeconds`, `turn` | Session context. Stage labels are Korean display labels. |
| `counselorUtterance` | The learner's text, as typed or dictated. **Free text: review before sharing.** |
| `clientReply` | The client line shown (local ladder or AI persona). |
| `skill`, `counselingMove`, `quality` | Pilot detector output. `counselingMove` ∈ Silence, Reflection, Validation, ReflectionAndExploration, OpenQuestion, Advice, Neutral, PrematureReassurance. `quality` 0–3. |
| `deliveryAlignment`, `deliveryEvidenceAvailable`, `deliveryModifier`, `deliveryFeedback` | Language–delivery alignment result and the coaching text shown (Korean canonical). |
| `relationalSafety`, `guardedness`, `willingnessToDisclose` (`alliance` = safety, kept for v1 compatibility) | Client relational state after the turn, 0–1. |
| `culturalProfileId` | Interpretation profile, e.g. `ko-counseling-pilot-v1`. |
| `webcamSignalQuality`, `webcamMovement` | Derived webcam signal indicators only. |
| `auSource`, `auTracking`, `auCalibrated`, `au01` … `au45` | MediaPipe-derived AU **proxies** relative to the personal baseline; not FACS coding or emotion labels. |
| `conversationEngine` | `local`, `persona-llm`, `gemini-live` (real-time voice; utterances are speech transcripts), or `gpt-realtime-2.1`. |
| `liveInterrupted` | v3, Gemini Live only. The counselor spoke over the client's reply (barge-in); `clientReply` is then the part heard before the interruption. |
| `sessionPhase` | v3. Counseling phase practised: `intake`, `goal_setting`, `middle` (resistance, holding and containing) or `termination`. Also in session summaries. |
| `clientAffect` | v3. Client affect rendered for this reply (`guarded`, `anxious`, `relieved`, `thoughtful`), from the expression plan (text mode) or the persona's `set_client_affect` report (live mode). |
| `clientAffectIntensity` | v3. 0–1 intensity the voice and face rendered, after the expressivity gain and any cap. |
| `expressionPolicy` | v3. Expression policy version (`expr-1`), `live-report` for Gemini Live, or empty for local scripted replies. |
| `expressivity` | v3. Expressivity in force: `restrained`, `natural` or `vivid` (briefing card or study URL). |
| `skillCode`, `codebookVersion` | v3. The code from ko-codebook-1 (`reflection_exploration`, `reflection`, `validation`, `open_question`, `closed_question`, `why_question`, `advice`, `premature_reassurance`, `neutral`, `silence`) that drove the turn. |
| `codingSource` | v3. `llm` when the server coder (`POST /code`) answered with confidence ≥ 0.5, otherwise `lexicon`. |
| `codingModel`, `codingConfidence`, `codingRationale`, `codingEvidence` | v3. LLM coder model id, its confidence (0–1), the one-sentence rationale shown in the debrief, and the quoted span of the utterance it relied on (empty for the lexicon). |
| `lexiconCode`, `lexiconQuality` | v3. The lexicon's code for the same utterance, always recorded, so LLM–lexicon agreement can be computed from field data. |
| `relationalModelVersion` | v3. Version of `RelationalModelWeights` in use (`avp-prior-1` or a fitted `fit-YYYYMMDD-nN`). |
| `safetyBefore`, `guardednessBefore`, `disclosureBefore` | v3. Client state before the turn (the after-state is `relationalSafety`, `guardedness`, `willingnessToDisclose`). |

## `counseling-session-summaries.jsonl`

`caseId`, `trainingMode`, `focusSkill`, `replaySourceTurn`, `timedOut`, `elapsedSeconds`, `turnCount`, `finalStage`, `alignedCount`, `mismatchCount`, `relationalSafety`, `guardedness`, `willingnessToDisclose`.

## `counseling-self-assessments.jsonl`

`caseId`, `trainingMode`, `sourceTurn`, `selfAssessment` (`잘된 장면` = effective, `다시 연습 필요` = needs another try), `skill`, `skillCode`, `codingSource`, `quality` (system values for comparison with the learner's own judgment).

## Export bundle (for the instructor dashboard)

“기록 내보내기 / Export records” on the briefing card writes one JSON file (WebGL: a download;
desktop: `persistentDataPath/exports/`):

```json
{ "format": "counselcue-export", "version": 1, "exportedUtc": "…", "appVersion": "…",
  "schemaVersion": 3, "codebookVersion": "ko-codebook-1",
  "learnerId": "L-7F3A9C", "learnerCode": "(optional, typed by the learner)",
  "sessions": [ …turn records… ], "summaries": [ … ], "assessments": [ … ] }
```

`learnerId` is a random device-local pseudonym. The arrays are the JSONL lines exactly as
stored. The instructor dashboard (`Dashboard/dashboard.html`, deployed at `/dashboard/`) reads
these files, or the raw JSONL files, entirely in the browser; nothing is uploaded.

## Handling guidance for studies

- Treat `counselorUtterance` as potentially identifying free text; pseudonymize or redact before analysis outside the device.
- Record the `lexiconVersion` and `culturalProfileId` with any analysis; scores are pilot rules, not validated competency measures.
- Retention limits and institutional storage are not implemented in the app; define them in the study protocol.
