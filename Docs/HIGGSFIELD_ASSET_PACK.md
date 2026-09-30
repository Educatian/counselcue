# CounselCue × Higgsfield 에셋 팩

CounselCue의 2D 비주얼을 Higgsfield로 만들기 위한 프롬프트와 파일 규격입니다. 생성한 파일을 **아래 경로와 파일명 그대로** 넣으면 Unity 빌더와 WebGL 템플릿이 자동으로 반영합니다. 파일이 없으면 지금 모습이 그대로 유지되므로, 에셋은 한 번에 하나씩 넣어도 됩니다.

> Higgsfield는 2D 이미지·영상 도구입니다. 상담 중에 관찰하는 3D 내담자(Rocketbox 아바타, FACS/시선 레이어)는 바꾸지 않습니다. 이 팩은 그 주변인 브리핑, 상담실 배경, 로딩·온보딩, 홍보물을 다룹니다.

## 0. 작업 순서

1. 아래 **공통 스타일**을 Higgsfield 프롬프트 앞에 붙여 씁니다. 영어 프롬프트가 결과가 더 안정적입니다.
2. 각 에셋은 4장 이상 생성한 뒤, **검수 기준**을 통과한 1장만 고릅니다.
3. 정해진 경로에 넣습니다.
   - Unity 에셋: `Tools → CounselCue → Build Korean Counseling Room`을 다시 실행합니다.
   - WebGL 에셋: 다시 빌드한 뒤 배포합니다.
4. 사용한 모델·프롬프트·시드·날짜를 `Assets/Art/Higgsfield/PROVENANCE.md`에 기록합니다. 연구 보고와 저작권 확인에 필요합니다.
5. 사용 중인 Higgsfield 요금제에서 상업적 이용·재배포가 허용되는지 확인합니다. 공개 데모와 논문 그림에 쓰이기 때문입니다.

| 에셋 | 경로 | 비율 · 권장 크기 | 권장 모델 |
|---|---|---|---|
| 사례 일러스트 ×5 | `Assets/Art/Higgsfield/Portraits/{caseId}.png` | 1:1 · 1024² | Soul(첫 장) → Nano Banana(변형·일관성) |
| 창밖 풍경 | `Assets/Art/Higgsfield/Room/window_view.jpg` | 9:16 · 1152×2048 | Soul 또는 Seedream |
| 벽 액자 그림 | `Assets/Art/Higgsfield/Room/wall_artwork.png` | 1:1 · 2048² | Seedream 또는 Recraft |
| 로딩 배경 | `Assets/WebGLTemplates/CounselCue/TemplateData/loading-hero.jpg` | 16:9 · 1920×1080 | Soul |
| 튜토리얼 첫 장 | `Assets/WebGLTemplates/CounselCue/TemplateData/tour-welcome.jpg` | 16:9 · 1280×720 | Seedream |
| 링크 미리보기 | `Assets/WebGLTemplates/CounselCue/TemplateData/social-card.jpg` | 1200×630 | GPT Image(글자 포함 시) |
| GitHub 소셜 프리뷰 | `.github/counselcue-social-preview.jpg` + 저장소 설정 업로드 | 1280×640 | GPT Image |
| 홍보 영상 | 저장소 밖(LinkedIn·README 링크) | 1:1, 16:9 | Kling · Higgsfield DOP · Seedance |

`caseId`: `workplace-anxiety-01`, `adolescent-pressure-01`, `career-transition-01`, `older-bereavement-01`, `international-belonging-01`

현재 `loading-hero.jpg`와 `social-card.jpg`에는 기존 스크린샷으로 만든 임시 이미지가 들어 있습니다. 같은 이름으로 덮어쓰면 됩니다.

## 1. 공통 스타일

상담실은 따뜻한 아이보리, 세이지, 월넛 톤이고, 벽에는 한지 찢어 붙이기 풍의 산 그림(`Assets/Art/Textures/HanjiMountainArtwork.png`)이 걸려 있습니다. 모든 2D 에셋을 이 톤에 맞춥니다.

**팔레트**(빌더 머티리얼 기준): 아이보리 `#E8DEC9` · 웜화이트 `#F0E8D9` · 세이지 `#6B8066` · 오크 `#A87A4D` · 월넛 `#42261A` · 액션 틸 `#337A5E` · 골드 `#F8C77A`

**스타일 프리픽스**(모든 이미지 공통)

```text
Soft editorial illustration with subtle Korean hanji paper grain, muted warm palette of ivory, sage green, oak and walnut brown, gentle diffused daylight, calm and respectful mood, clean composition, no text, no logo, no watermark.
```

**네거티브**(지원하는 모델에서 사용)

```text
text, letters, watermark, logo, signature, frame border, extra fingers, distorted hands, exaggerated crying, dramatic distress, clinical hospital setting, medical equipment, caricature, stereotype, cartoon anime style, oversaturated, harsh shadows
```

## 2. 사례 브리핑 일러스트 (5장)

**표시 위치:** 브리핑 카드 오른쪽 170×170 영역입니다. 아래에 "AI 생성 사례 일러스트" 캡션이 자동으로 붙습니다. 이미지가 있는 사례만 설명 텍스트 폭이 줄어듭니다.

**사진이 아니라 일러스트로 만드는 이유**
- 3D 아바타(Rocketbox)와 얼굴이 완전히 같을 수 없습니다. 사실적인 사진을 쓰면 학습자가 "다른 사람"으로 인식합니다.
- 일러스트는 사례 카드로 읽히면서 옷차림·연령·분위기만 맞추면 됩니다.

**작업 방법**
1. 사례마다 첫 장은 Soul로 만듭니다.
2. 변형이 필요하면 첫 장을 레퍼런스로 넣고 Nano Banana로 만듭니다.
3. Soul ID는 실존 인물 사진으로 학습하는 기능이므로 여기서는 쓰지 않습니다.

**검수 기준**
- [ ] 옷차림과 헤어가 3D 아바타와 일치(아래 표)
- [ ] 표정은 중립~약간 긴장. 울거나 과장된 고통 표현 없음
- [ ] 가슴 위 상반신, 얼굴이 중앙 60% 안, 배경은 무지 아이보리
- [ ] 글자·로고·워터마크 없음, 손가락 왜곡 없음
- [ ] 170px로 줄여도 얼굴이 식별됨

| caseId | 내담자 | 3D 아바타 외형(맞출 것) |
|---|---|---|
| workplace-anxiety-01 | 김지혜, 32세 여성 | 어깨 길이 검은 머리, 연분홍 핀스트라이프 블레이저, 하늘색 셔츠 |
| adolescent-pressure-01 | 박서윤, 16세 여성 | 자주(버건디) 히잡, 올리브그린 긴 튜닉, 어두운 바지 |
| career-transition-01 | 최민준, 39세 남성 | 짧은 짙은 갈색 머리, 짙은 회색 재킷, 녹슨 갈색 셔츠 |
| older-bereavement-01 | 이정호, 68세 남성 | 이마가 넓게 벗어진 회색 머리, 연회색 셔츠, 카키 바지 |
| international-belonging-01 | 왕하오, 24세 남성 | 귀를 덮는 검은 머리, 네이비 반팔 티셔츠, 청바지 |

**workplace-anxiety-01 · 김지혜**

```text
[STYLE PREFIX] Chest-up portrait of a Korean woman in her early thirties, shoulder-length straight black hair, light pink pinstripe blazer over a pale blue collared shirt, hands loosely clasped, slightly tense but composed expression, gaze just off to the side, plain warm ivory background, soft window light from the left.
```

**adolescent-pressure-01 · 박서윤**

```text
[STYLE PREFIX] Chest-up portrait of a 16-year-old Korean-born high school girl from a multicultural family, wearing a neatly wrapped burgundy hijab and a long olive-green tunic, age-appropriate and modest, quiet uncertain expression, eyes lowered slightly, holding the strap of a school backpack, plain warm ivory background, soft daylight. Respectful, non-stereotyped depiction.
```

미성년자 사례입니다. 교복·학교 소품 수준의 중립적 연출만 쓰고, 신체 강조·연출된 포즈·화장 강조는 쓰지 않습니다. 히잡은 정체성의 일부로 자연스럽게 그리고, 문제의 상징처럼 보이게 하지 않습니다.

**career-transition-01 · 최민준**

```text
[STYLE PREFIX] Chest-up portrait of a Korean man about 39 years old, short dark brown hair, charcoal grey casual blazer over a rust-brown shirt, tired but polite expression, slight frown of deliberation, looking slightly upward as if weighing a decision, plain warm ivory background, soft window light.
```

**older-bereavement-01 · 이정호**

```text
[STYLE PREFIX] Chest-up portrait of a Korean man in his late sixties, receding grey hair, light grey button-up shirt, dignified and quiet expression, gaze gently lowered in remembrance, hands resting together, plain warm ivory background, soft late-morning light. Dignified, not frail or pitiable.
```

**international-belonging-01 · 왕하오**

```text
[STYLE PREFIX] Chest-up portrait of a 24-year-old Chinese graduate student living in Korea, black hair falling over the ears, navy short-sleeve t-shirt, holding a notebook, thoughtful and slightly hesitant expression, eyes glancing to the side as if searching for the right word, plain warm ivory background, soft daylight. Natural, non-caricatured depiction.
```

## 3. 상담실 환경 아트

설계 원칙: **내담자 얼굴 관찰이 우선입니다.** 배경은 대비가 낮고 시선을 끌지 않아야 합니다.

### 3-1. 창밖 풍경 `Room/window_view.jpg`

**적용 위치:** 상담자 시점 왼쪽 뒤의 세로 창(`BackWindowGlow`)입니다.
- 은은한 자체 발광으로 표시되고, 조명 계산에는 영향을 주지 않습니다.
- 커튼과 스탠드가 일부를 가리므로 핵심 요소는 가운데에 둡니다.

```text
[STYLE PREFIX] View through a window of a quiet low-rise Seoul residential street in late morning, soft overcast daylight, ginkgo and zelkova trees, pale sky, distant rooftops, strong soft-focus depth of field as if seen from inside a room, no people, no cars in focus, no readable signs, low contrast, airy and calm. Vertical 9:16.
```

**검수 기준**
- [ ] 사람·읽을 수 있는 간판 없음
- [ ] 흐린 하늘 위주로, 밝은 부분이 화면을 태우지 않음
- [ ] 계절감이 과하지 않음(연구 세션 시점과 무관하게 자연스럽게)

### 3-2. 벽 액자 그림 `Room/wall_artwork.png`

**적용 위치:** 내담자 머리 뒤 벽의 한지 액자 안(`CC_HanjiArtwork_Canvas`)입니다. 이번 개선으로, 짙은 월넛 판으로만 보이던 액자에 실제 그림이 들어갑니다. 새 파일이 없으면 기존 한지 산 그림을 씁니다.

```text
Abstract torn Korean hanji paper collage of layered distant mountains and a pale sun, muted sage, sand beige, soft terracotta and ivory, visible paper fibers, very low contrast, flat lighting, calm and minimal, no people, no faces, no eyes, no animals, no text. Square 1:1.
```

**검수 기준**
- [ ] **얼굴·눈·동물 형상 없음.** 머리 뒤에 있어서 시선 진단과 표정 관찰을 방해합니다.
- [ ] 가장 어두운 색도 내담자 머리카락보다 밝게 해서 머리 윤곽 대비를 유지
- [ ] 명도 대비가 낮은 파스텔 계열

## 4. 로딩 · 온보딩

### 4-1. 로딩 배경 `TemplateData/loading-hero.jpg`

**적용 위치:** WebGL 로딩 화면 배경입니다. 위에 어두운 녹색 오버레이가 덮이고, 로고·진행바·안내 문구가 가운데에 표시됩니다.

```text
[STYLE PREFIX] Empty contemporary Korean counseling room, two armchairs facing each other at a gentle angle, small round oak side table with a celadon tea cup and tissue box, hanji floor lamp glowing warmly, sheer curtains, wide shot, generous empty space in the center, no people. Horizontal 16:9.
```

### 4-2. 튜토리얼 첫 장 `TemplateData/tour-welcome.jpg`

**적용 위치:** 스팟라이트 튜토리얼 첫 카드 상단(16:9)에만 표시됩니다. 파일이 없으면 이미지 영역이 숨겨집니다.

```text
[STYLE PREFIX] Over-the-shoulder view from behind a counselor seated in an armchair, facing an empty client chair in a warm counseling room, the counselor shown only as a soft silhouette of shoulder and notebook, inviting and calm, no faces visible. Horizontal 16:9.
```

### 4-3. 링크 미리보기 · GitHub 소셜 프리뷰

- `TemplateData/social-card.jpg`(1200×630): 배포 페이지의 `og:image`로 이미 연결돼 있습니다.
- `.github/counselcue-social-preview.jpg`(1280×640): GitHub 저장소 **Settings → Social preview**에도 업로드해야 반영됩니다.

**만드는 방법**
- 배경은 4-1 이미지를 재사용합니다.
- 제목은 GPT Image로 넣거나, 편집 도구에서 직접 올리는 편이 정확합니다.

```text
Wide banner, left two thirds: calm illustrated Korean counseling room in ivory, sage and walnut tones with soft daylight; right third: clean empty space in deep green #173B30 for a title. No people. Title text (if the model supports text): "CounselCue" and subtitle "Relational delivery practice for counselors".
```

## 5. 홍보 · 데모 영상 (LinkedIn · README)

**정직성 원칙**
- 제품 기능을 보여주는 장면은 **실제 WebGL 빌드 화면 녹화**만 씁니다.
- Higgsfield는 도입·마무리 분위기 컷(b-roll)에만 씁니다.
- AI로 만든 "가짜 앱 화면", AI 인물이 말하는 추천 영상, 3D 내담자 얼굴을 AI로 립싱크·재생성한 영상은 만들지 않습니다. 실제 표정·시선 시스템을 잘못 보여주게 됩니다.

**구성:** 약 25초, 1:1(1080×1080, LinkedIn 피드)과 16:9(1920×1080) 두 가지로 내보냅니다.

| # | 길이 | 소스 | 내용 · 프롬프트 |
|---|---|---|---|
| 1 | 3초 | Higgsfield 이미지→영상(Kling 또는 DOP) | 4-1 로딩 배경을 첫 프레임으로 사용. `Slow gentle dolly-in toward the empty client chair, lamp light softly flickering, dust motes in sunlight, no people appear, calm.` |
| 2 | 5초 | 실제 녹화 | 브리핑 → 사례 5개 중 선택 → 코칭 연습 시작 |
| 3 | 8초 | 실제 녹화 | 내담자 발화 → 상담자가 감정 반영 입력 → "감정 반영 + 탐색 · 전달 정합" 피드백 → 내담자 응답·음성 |
| 4 | 5초 | 실제 녹화 | 디브리프: 자기평가를 먼저 고른 뒤 시스템 근거 공개 → 장면 재연습 |
| 5 | 2초 | Higgsfield 이미지→영상 | `Close-up of a celadon tea cup on an oak side table, soft steam rising, warm window light, shallow depth of field, no people.` |
| 6 | 2초 | 정지 엔드카드 | "CounselCue · github.com/Educatian/counselcue", "연구·훈련용 프로토타입 · 진단/평가 도구 아님" |

- 자막은 한국어·영어를 모두 넣습니다. 3번 장면은 입력 문장을 자막으로 크게 보여 줍니다.
- 화면 녹화는 1920×1080 전체화면 WebGL로 합니다. 튜토리얼은 미리 닫고, 디버그 패널은 숨깁니다.
- AI 생성 컷(1·5번)에는 영상 설명란에 "일부 배경 컷은 AI 생성"이라고 표기합니다.
- README에서는 영상을 GitHub 이슈 편집기로 업로드해 받은 링크를 쓰거나, 3번 장면을 GIF로 만들어 넣습니다.

## 6. 반영 확인 체크리스트

- [ ] 사례 5개를 차례로 선택할 때 브리핑 일러스트가 사례에 맞게 바뀌고, 캡션이 한/영 토글에서 모두 보임
- [ ] 일러스트가 없는 사례는 이미지 영역이 숨고 설명 폭이 원래대로 돌아옴
- [ ] 관찰 줌 100%와 최대 줌에서 내담자 머리 윤곽이 액자 그림과 분명히 구분됨(`progress-42` 장면과 비교)
- [ ] 창밖 풍경이 내담자 얼굴 밝기에 영향을 주지 않음(표정·시선 진단 패널 값 비교)
- [ ] WebGL 로딩 화면에서 로고·진행바·안내 문구 대비가 충분함
- [ ] 링크 미리보기 확인(LinkedIn Post Inspector 등)
- [ ] 이미지당 권장 용량: 일러스트 ≤ 400 KB, 창밖·액자 ≤ 800 KB, 로딩 배경 ≤ 300 KB. WebGL 다운로드 크기에 포함됩니다.
- [ ] `PROVENANCE.md` 기록 완료
