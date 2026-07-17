# Affect Counsel Unity

한국어 1:1 상담사 훈련을 위한 독립형 Unity 프로토타입입니다. Microsoft Rocketbox 성인 아바타와 로컬 웹캠 신호를 사용해 상담자의 언어적 반응과 비언어적 전달을 함께 연습합니다.

![가까운 거리의 1대1 상담 장면과 보정 완료 상태](Screenshots/progress-16-au-calibrated.png)

> **연구·훈련용 프로토타입:** 진단, 감정 판정, 상담역량 자동평가 도구가 아닙니다. 얼굴 동작값은 자기 점검과 연구 검증을 위한 관찰 신호로만 사용합니다.

## 현재 상태

| 영역 | 구현 상태 |
|---|---|
| 1:1 상담 장면 | 가까운 카메라, 얼굴·상체·손 제스처 관찰 가능 |
| 내담자 상호작용 | 불안 사례 기반 3턴 초기면담, 표정·시선·발화 애니메이션 |
| 상담 반응 피드백 | 반영, 정서 타당화, 개방형 질문, 조언 중심 반응 구분 |
| 얼굴 동작 입력 | MediaPipe Face Landmarker 기반 13개 AU proxy |
| 개인 기준선 | 시작 후 10초 동안 중립 표정 보정, 이후 기준선 차감 |
| 데이터 기록 | 원본 영상 없이 파생 신호와 대화 이벤트만 JSONL 기록 |

## 현재 구현

- 불안 사례 기반 3턴 초기면담 시뮬레이션
- 반영, 정서 타당화, 개방형 질문, 조언 중심 반응을 구분하는 로컬 휴리스틱 피드백
- Rocketbox 내담자 아바타의 시선, 표정, 발화 애니메이션
- 웹캠 프레임의 밝기 품질과 움직임 안정성 신호
- MediaPipe Face Landmarker의 52개 blendshape를 선택 AU proxy로 변환하는 로컬 브리지
- 10초 중립 표정 기준선 보정과 보정 전·후 상태 표시
- 관계 안전감 변화와 세션 JSONL 기록
- 한국 상담실을 참고한 따뜻한 목재·크림색·간접조명 환경

### 중립 표정 보정

| 보정 중 | 보정 완료 |
|---|---|
| ![중립 표정 보정 진행 상태](Screenshots/progress-15-au-calibrating.png) | ![중립 표정 보정 완료 후 AU proxy 표시](Screenshots/progress-16-au-calibrated.png) |

웹캠에서 얼굴이 추적되면 10초 동안 편안한 중립 표정의 개인 기준선을 계산합니다. 보정이 끝난 뒤에는 각 proxy에서 기준선을 차감하며, 세션 로그의 `auCalibrated` 값으로 보정 완료 여부를 구분합니다.

## 실행

- Unity: `6000.4.9f1`
- 시작 씬: `Assets/Scenes/KoreanCounselingRoom.unity`
- Windows 빌드: `Builds/AffectCounselDemo/AffectCounsel.exe`

에디터 메뉴 `Tools > Affect Counsel > Build Korean Counseling Room`으로 씬을 다시 생성할 수 있습니다.

## 개인정보와 해석 한계

- 웹캠 원본 영상은 저장하지 않습니다.
- 현재 웹캠 기능은 얼굴 감정 분류기가 아니라 조명 품질과 움직임 안정성을 계산하는 입력 어댑터입니다.
- 움직임 신호는 상담 능력이나 감정을 판정하지 않으며, 훈련자에게 자기 점검 단서로만 제시해야 합니다.
- 세션 기록에는 상담자 입력 문장과 파생 신호가 포함됩니다. 실제 교육 배포 전 명시적 동의, 보존 기간, 삭제 기능, 가명화 정책이 필요합니다.
- 기록 위치: Unity `Application.persistentDataPath/counseling-sessions.jsonl`

## 다음 개발 단계

1. GPT Realtime 대화 어댑터와 로컬 데모 어댑터를 공통 인터페이스로 분리
2. 백엔드에서 Realtime 임시 토큰을 발급하고 Unity는 WebRTC로 연결
3. 전문가가 검수한 사례·평가 루브릭을 ScriptableObject 데이터로 분리
4. 웹캠 처리의 온디바이스 보장, 동의 화면, 즉시 삭제 기능 구현
5. 교육자 대시보드와 세션 리플레이에는 원본 영상 대신 이벤트·점수 타임라인만 사용
6. 상담 전문가/학습자 대상 사용성 연구와 채점자 간 신뢰도 검증

## GPT Realtime 목표 구조

`Unity microphone → WebRTC → GPT Realtime → audio/transcript events → Rocketbox lip-sync + affect state`

- 권장 모델: `gpt-realtime-2.1`
- Unity 빌드에는 표준 OpenAI API 키를 포함하지 않습니다.
- 개발자 소유 백엔드가 `/v1/realtime/client_secrets`로 임시 토큰을 발급합니다.
- 사례 프롬프트는 내담자 역할과 정보 공개 단계를 제어하고, 평가는 별도의 구조화된 루브릭 단계에서 수행합니다.
- 연결 실패 시 현재 로컬 3턴 시뮬레이션으로 자동 대체합니다.

### 로컬 실행

.NET 10 SDK가 필요합니다. PowerShell에서 표준 키를 현재 프로세스 환경에만 설정하고 토큰 브로커를 실행합니다. 키를 Unity 프로젝트나 설정 파일에 저장하지 마세요.

```powershell
$env:OPENAI_API_KEY = "사용자 키"
dotnet run --project .\Server\AffectCounsel.TokenBroker
```

브로커가 실행된 뒤 다음과 같이 Realtime 모드를 시작합니다.

```powershell
.\Builds\AffectCounselDemo\AffectCounsel.exe --realtime
```

`--realtime`을 생략하거나 브로커 연결이 실패하면 기존 로컬 사례 엔진을 사용합니다. 현재 구현은 먼저 텍스트 Realtime 대화를 연결하며, 마이크 음성 스트리밍과 GPT 음성 재생은 다음 단계입니다.

## 실시간 얼굴 동작 계수

AU 모드는 Python 브리지가 웹캠을 직접 읽고 UDP loopback으로 Unity에 파생값만 전달합니다. 원본 프레임은 파일이나 세션 로그에 저장하지 않습니다.

```powershell
uv run .\Tools\AuBridge\face_au_bridge.py
.\Builds\AffectCounselDemo\AffectCounsel.exe --au
```

화면에는 보정 진행률 또는 AU04, AU12, AU45의 현재 proxy 값이 표시됩니다. 세션 JSONL에는 `auSource`, `auTracking`, `auCalibrated`와 AU01·02·04·06·07·12·14·15·17·23·25·26·45가 기록됩니다. 이 값은 MediaPipe blendshape를 FACS 이름에 근사 매핑한 관찰 신호이며, 인증된 FACS 코딩이나 감정·상담역량 판정값이 아닙니다. 연구 분석에서는 원자료 표본에 대한 사람 코더 검증과 OpenFace 등 별도 도구를 이용한 재분석이 필요합니다.

## 구성

```text
웹캠 → Python/MediaPipe 브리지 → UDP loopback → Unity AU 모니터
상담자 입력 → 로컬 사례 엔진 또는 GPT Realtime → 내담자 대사·애니메이션
대화 이벤트 + 파생 신호 → 로컬 JSONL 세션 기록
```

Unity 캐시, Windows 빌드, 로컬 로그와 세션 데이터는 Git 저장소에 포함하지 않습니다.

## 제3자 자산

Microsoft Rocketbox 자산은 `Assets/ThirdParty/MicrosoftRocketbox/LICENSE.md`의 라이선스를 따릅니다. Noto Sans KR 폰트는 해당 자산의 라이선스 조건을 확인한 뒤 배포해야 합니다.
