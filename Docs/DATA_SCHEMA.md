# CounselCue local research data (schema v2)

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
| `schemaVersion` | int | `2` since this release. Records without it are v1 (no `caseId`, `sessionId` only in turn records). |
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
| `conversationEngine` | `local`, `persona-llm`, or `gpt-realtime-2.1`. |

## `counseling-session-summaries.jsonl`

`caseId`, `trainingMode`, `focusSkill`, `replaySourceTurn`, `timedOut`, `elapsedSeconds`, `turnCount`, `finalStage`, `alignedCount`, `mismatchCount`, `relationalSafety`, `guardedness`, `willingnessToDisclose`.

## `counseling-self-assessments.jsonl`

`caseId`, `trainingMode`, `sourceTurn`, `selfAssessment` (`잘된 장면` = effective, `다시 연습 필요` = needs another try), `skill`, `quality` (system values for comparison with the learner's own judgment).

## Handling guidance for studies

- Treat `counselorUtterance` as potentially identifying free text; pseudonymize or redact before analysis outside the device.
- Record the `lexiconVersion` and `culturalProfileId` with any analysis; scores are pilot rules, not validated competency measures.
- Retention limits and institutional storage are not implemented in the app; define them in the study protocol.
