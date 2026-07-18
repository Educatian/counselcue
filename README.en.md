# CounselCue

[한국어 문서](README.ko.md) · [English documentation](README.en.md) · [Language index](README.md)

[**Launch the live WebGL demo →**](https://educatian.github.io/counselcue/)

A standalone Unity prototype for Korean one-to-one counselor training. It combines a Microsoft Rocketbox virtual client, local webcam-derived facial action signals, counseling micro-skill classification, and a relational state model.

![Counseling practice with a close, observable client](Screenshots/progress-35-reference-room.png)

> **Research and training prototype:** This is not a diagnostic tool, an emotion classifier, or an automated assessment of counseling competence. Facial movement values are observation signals for reflection and research validation only.

## Current capabilities

| Area | Implementation |
|---|---|
| One-to-one scene | Close counselor-view camera with observable face, upper body, and hand gestures |
| Session flow | Briefing → 15-minute practice → pause/resume/end → debrief |
| Focused practice | Three-minute, three-turn practice for emotion reflection, open questions, or delivery alignment |
| Reflection loop | Scene selection → self-assessment → system evidence → selected-scene replay |
| Case authoring | ScriptableObject cases with objectives, disclosure ladder, focus skills, and duration |
| Relational model | Safety, guardedness, and willingness to disclose shape the next client response |
| Facial input | Optional MediaPipe Face Landmarker bridge with 13 AU proxies and neutral calibration |
| Observation zoom | Mouse wheel, −/+, and reset controls |
| UI language | `UI: EN / UI: KO` toggle for fixed interface copy; client dialogue and counselor input preserve their original language |
| Data policy | Derived signals and session events are stored locally as JSONL; raw webcam video is not recorded |

## Environment and UI design

The room uses a close, barrier-free counseling composition inspired by a contemporary Korean private practice: warm ivory walls, light upholstery, sage curtains, walnut storage, a side table with tissues, plants, indirect lighting, and framed hanji landscape art. Client observability takes priority over decoration.

| Generated environment reference | Unity implementation |
|---|---|
| ![Generated Korean counseling-room reference](Assets/Art/References/KoreanCounselingRoomReference.png) | ![Unity room based on the reference](Screenshots/progress-35-reference-room.png) |

The UGUI panels, buttons, inputs, and dividers use selected high-resolution 9-slice sprites from the CC0-licensed Kenney UI Pack 2.0, tinted to the room's sage and ivory palette.

| Korean interface | English interface |
|---|---|
| ![Korean briefing UI](Screenshots/progress-36-polished-briefing.png) | ![English briefing UI](Screenshots/progress-37-english-ui.png) |

## Learning-experience loop

The simulator trains professional affective delivery rather than inferring what the counselor feels. A technically appropriate utterance can still be reviewed for possible mismatch with facial tension or delivery cues.

```text
Counseling micro-skill + calibrated delivery cues
        ↓ temporal alignment
aligned / possible mismatch / relational-order mismatch / insufficient evidence
        ↓
client safety · guardedness · willingness to disclose
        ↓
next response becomes more open or more guarded
```

System evidence and replay remain locked until the trainee records a judgment for the selected scene. Aggregate session feedback remains hidden until every turn has been self-assessed.

## Requirements

- Unity 6000.4.9f1 or a compatible Unity 6 editor
- Windows 64-bit for the included standalone build workflow
- Microsoft Rocketbox avatar at the project path expected by `CounselingRoomBuilder`
- Noto Sans KR font asset
- Optional webcam for AU-proxy input; the counseling flow works without it

## Build and validation

Open the project and run:

```text
Tools → CounselCue → Build Korean Counseling Room
```

The project also exposes command-line editor methods:

```text
AdieLab.AffectCounsel.Editor.CounselingRoomBuilder.BuildWindowsFromCommandLine
AdieLab.AffectCounsel.Editor.CounselingSessionFlowChecks.RunFromCommandLine
AdieLab.AffectCounsel.Editor.CounselingRoomBuilder.BuildWebGLFromCommandLine
```

Generated Windows output is written to `Builds/CounselCue/`; WebGL output is written to `Builds/WebGL/`. Both are intentionally excluded from Git. The hosted build adds browser-native Korean text input, Korean microphone dictation, spotlight onboarding, and ElevenLabs v3 emotional client speech. A server-owned OpenAI persona endpoint is used when configured, with deterministic local fallback. Provider keys stay on the edge worker. UDP AU input remains desktop-only.

## Privacy and validation boundaries

- Webcam imagery is processed locally and is not saved by the prototype.
- AU values are proxies, not emotion labels.
- Thresholds, focused-practice duration, and turn targets are pilot settings that require expert review and user research.
- Cultural interpretation rules for silence, eye contact, honorifics, backchannels, advice expectations, and authority require empirical validation.
- LLM-generated client responses require safety controls, latency handling, deterministic fallbacks, and supervision before deployment.

## Third-party assets

- Microsoft Rocketbox assets follow `Assets/ThirdParty/MicrosoftRocketbox/LICENSE.md`.
- [Kenney UI Pack 2.0](https://kenney.nl/assets/ui-pack) is licensed CC0; the original license is stored in `Assets/ThirdParty/Kenney/UI/LICENSE.txt`.
- Noto Sans KR is distributed under the SIL Open Font License 1.1; the license is included at `Assets/Fonts/OFL.txt`.
