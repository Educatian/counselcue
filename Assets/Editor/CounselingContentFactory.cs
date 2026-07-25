using System;
using UnityEditor;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    public static class CounselingContentFactory
    {
        public const string CatalogPath = "Assets/Data/CaseCatalog.asset";

        private static readonly string[] AvatarPaths =
        {
            "Assets/ThirdParty/MicrosoftRocketbox/Avatars/Adults/Female_Adult_05/Export/Female_Adult_05_facial.fbx",
            "Assets/ThirdParty/MicrosoftRocketbox/Avatars/Children/Female_Child_02/Export/Female_Child_02_facial.fbx",
            "Assets/ThirdParty/MicrosoftRocketbox/Avatars/Adults/Male_Adult_07/Export/Male_Adult_07_facial.fbx",
            "Assets/ThirdParty/MicrosoftRocketbox/Avatars/Adults/Male_Adult_14/Export/Male_Adult_14_facial.fbx",
            "Assets/ThirdParty/MicrosoftRocketbox/Avatars/Adults/Male_Adult_09/Export/Male_Adult_09_facial.fbx"
        };

        [MenuItem("Tools/CounselCue/Create Sprint Case Catalog")]
        public static CaseCatalog CreateOrUpdate()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Profiles");
            EnsureFolder("Assets/Data/Presentations");
            EnsureFolder("Assets/Data/Cases");

            CaseSpec[] specs = BuildSpecs();
            CounselingCaseDefinition[] cases = new CounselingCaseDefinition[specs.Length];
            for (int i = 0; i < specs.Length; i++) cases[i] = CreateCase(specs[i], i);

            CaseCatalog catalog = LoadOrCreate<CaseCatalog>(CatalogPath);
            catalog.Configure(cases, 0);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static CounselingCaseDefinition CreateCase(CaseSpec spec, int avatarIndex)
        {
            ClientProfileDefinition profile = LoadOrCreate<ClientProfileDefinition>($"Assets/Data/Profiles/{spec.Id}.asset");
            profile.Configure(spec.Id, spec.Name, spec.Age, spec.Domain, spec.CulturalContext, spec.NonverbalStyle, spec.GazeComfort, spec.DisclosurePace);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPaths[avatarIndex]);
            if (prefab == null) throw new InvalidOperationException($"Rocketbox facial avatar is missing: {AvatarPaths[avatarIndex]}");
            AvatarPresentationDefinition presentation = LoadOrCreate<AvatarPresentationDefinition>($"Assets/Data/Presentations/{spec.Id}.asset");
            presentation.Configure($"rocketbox-{spec.Id}", prefab, new Vector3(0f, 0.08f, 1.02f), new Vector3(0f, 180f, 0f), Vector3.one,
                spec.VoiceStyle, spec.ExpressionIntensity, spec.GazeIntensity);

            CounselingCaseDefinition definition = LoadOrCreate<CounselingCaseDefinition>($"Assets/Data/Cases/{spec.Id}.asset");
            definition.Configure(spec.Id, spec.Title, spec.Name, $"{spec.Age} · {spec.Domain}", spec.Concern, spec.InitialLine,
                900f, 180f, 3, spec.Objectives, BuildLadder(spec.Supportive, spec.Guarded), DefaultFocusSkills());
            definition.ConfigurePresentation(profile, presentation, spec.Domain, spec.Difficulty, spec.Id);
            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(presentation);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static CounselingDisclosureStep[] BuildLadder(string[] supportive, string[] guarded)
        {
            int length = Mathf.Min(supportive.Length, guarded.Length);
            CounselingDisclosureStep[] result = new CounselingDisclosureStep[length];
            for (int i = 0; i < length; i++) result[i] = new CounselingDisclosureStep { supportiveReply = supportive[i], guardedReply = guarded[i] };
            return result;
        }

        private static CounselingFocusSkill[] DefaultFocusSkills() => new[]
        {
            new CounselingFocusSkill { id="emotion-reflection", label="감정 반영", objective="감정과 의미를 구체적으로 반영한다.", coachingPrompt="감정 단어와 그 의미를 한 문장에 담아 보세요." },
            new CounselingFocusSkill { id="open-question", label="개방형 질문", objective="내담자가 경험을 확장하도록 질문한다.", coachingPrompt="예·아니오로 끝나지 않는 질문 뒤 응답 공간을 남기세요." },
            new CounselingFocusSkill { id="delivery-alignment", label="전달 정합", objective="언어와 표정·시선의 전달을 맞춘다.", coachingPrompt="문장 내용과 얼굴의 긴장·미소가 같은 메시지인지 확인하세요." }
        };

        private static CaseSpec[] BuildSpecs() => new[]
        {
            new CaseSpec("workplace-anxiety-01", "직장 불안", "김지혜", "32세", "직업·성인상담", "기초",
                "최근 회사에 가려고 하면 숨이 막히고 자신의 역량을 의심합니다.",
                "요즘 회사에 가려고 하면 숨이 막히는 것 같아요.\n제가 너무 약한 사람인가 싶기도 하고요.",
                "존댓말과 간접 표현을 사용하며, 상담자의 조언보다 먼저 안전한 반응을 확인한다.", "초반에는 시선을 짧게 피하고 손을 모은다. 안전감이 생기면 재접촉과 공개가 늘어난다.", .52f, .48f, "soft-contemporary-korean", .72f, .70f,
                new[]{"관계 안전감을 형성한다.","불안의 상황·의미·영향을 탐색한다.","해결책보다 내담자의 선택을 앞세운다."},
                new[]{"누군가에게 말하니 조금 정리가 되는 느낌이에요.","회사에 들어가는 순간부터 가슴이 답답해져요.","특히 팀장님과 이야기할 때 더 심해져요.","회의에서 실수를 지적받은 뒤 시선이 무서워졌어요.","가족에게는 걱정시킬까 봐 말하지 못했어요.","당장 답보다 안전하게 일할 수 있다는 느낌이 필요해요."},
                new[]{"그냥 제가 알아서 해야 하는 문제 같아요.","그렇게 간단한 문제는 아닌 것 같아요.","무슨 말을 해야 할지 모르겠어요.","그 얘기는 아직 자세히 하고 싶지 않아요.","가족 이야기는 하고 싶지 않아요.","오늘은 여기까지만 이야기하고 싶어요."}),
            new CaseSpec("adolescent-pressure-01", "다문화 청소년 학업 압박", "박서윤", "16세", "청소년·다문화·학교상담", "중급",
                "한국에서 성장한 다문화 가정의 무슬림 청소년으로, 성적 하락과 부모 기대, 종교적 복장에 대한 또래의 시선 사이에서 지치고 학교에서도 혼자 있으려 합니다.",
                "엄마는 제가 그냥 게을러진 거래요. 학교에서는 제 옷을 보고 계속 물어보는 것도 지쳐요.",
                "성인 권위에 대한 경계를 고려하고 존댓말을 강요하지 않는다. 종교·문화 정체성을 문제의 원인으로 단정하지 않고 내담자의 의미를 확인하며 비밀보장의 한계를 투명하게 설명한다.", "직접 눈맞춤이 길면 부담을 느끼고 옆을 보며 생각한다. 재촉하지 않으면 짧게 재접촉한다.", .34f, .34f, "young-soft-korean", .64f, .56f,
                new[]{"상담의 비밀보장과 선택권을 설명한다.","학업 압박과 소속감 경험을 내담자의 언어로 탐색한다.","문화적 가정을 피하고 침묵을 존중한다."},
                new[]{"제 말을 바로 판단하지 않으니까 조금 편해요.","시험지를 받으면 심장이 빨리 뛰어요.","친구들이 제 스카프를 또 물어볼까 봐 점심도 혼자 먹어요.","아빠가 실망할까 봐 성적표를 숨겼어요.","가끔 그냥 사라지고 싶다는 생각까지 들어요.","누구 한 명이라도 제 편이라고 느끼고 싶어요."},
                new[]{"선생님도 결국 부모님한테 말할 거잖아요.","그냥 공부하기 싫은 것뿐이에요.","친구 얘기는 별로 하고 싶지 않아요.","집 얘기는 하지 않을래요.","그런 생각까지는 아니에요.","이제 그만 물어보면 안 돼요?"}),
            new CaseSpec("career-transition-01", "경력 전환과 번아웃", "최민준", "39세", "진로·직업상담", "중급",
                "안정적인 직장을 그만두고 싶은 마음과 가족 부양 책임 사이에서 갈등합니다.",
                "남들이 보기엔 괜찮은 직장인데, 저는 아침마다 제가 없어지는 기분이 듭니다.",
                "조언 중심 기대가 있을 수 있으나 가치와 양가감정을 먼저 탐색한다.", "생각할 때 위쪽을 보고, 불편한 질문에는 몸을 굳힌다. 존중을 느끼면 고개를 끄덕인다.", .58f, .46f, "calm-adult-korean", .68f, .72f,
                new[]{"양가감정을 동시에 반영한다.","가치와 역할 책임을 분리해 탐색한다.","즉각적인 진로 처방을 피한다."},
                new[]{"두 마음이 같이 있다는 표현이 맞는 것 같아요.","일 자체보다 제가 통제할 수 없는 게 힘들어요.","예전에는 만드는 일이 즐거웠어요.","아이들 때문에 모험하면 안 된다고 생각해요.","배우자에게는 아직 솔직히 말하지 못했어요.","작은 실험부터 해볼 수 있다면 덜 막막할 것 같아요."},
                new[]{"그래서 어디로 이직하라는 건가요?","그냥 다들 이 정도는 참고 살죠.","옛날 얘기는 별로 도움이 안 될 것 같아요.","가족 책임은 당연한 거죠.","배우자 얘기는 넘어가죠.","구체적인 답이 없다면 의미가 있나요?"}),
            new CaseSpec("older-bereavement-01", "노년기 사별과 고립", "이정호", "68세", "노인상담", "중급",
                "배우자 사별 후 식사와 수면이 흐트러졌고 자녀에게 짐이 될까 도움을 피합니다.",
                "집에 들어가면 너무 조용합니다. 자식들한테 이런 말까지 할 수는 없고요.",
                "연령 존중과 존댓말을 유지하되 과도한 권위적 태도나 유아화를 피한다.", "긴 침묵과 아래쪽 시선이 자연스러운 회상 과정일 수 있다. 고개 끄덕임은 동의보다 경청 신호일 수 있다.", .44f, .31f, "warm-older-korean", .58f, .60f,
                new[]{"사별의 속도와 침묵을 존중한다.","외로움과 일상 기능을 구분해 탐색한다.","자녀 관계에 대한 가정을 피한다."},
                new[]{"기다려 주시니 그 사람 생각을 조금 해볼 수 있네요.","아침에 눈뜨는 시간이 제일 힘듭니다.","같이 마시던 차를 아직 두 잔 준비할 때가 있어요.","아이들은 바빠 보여서 전화를 망설입니다.","요즘은 끼니를 자주 거릅니다.","누군가와 일주일에 한 번이라도 이야기하면 좋겠습니다."},
                new[]{"나이 들면 다 그런 거죠.","그 사람 얘기는 그만하겠습니다.","별일 아닙니다.","아이들은 바쁩니다. 괜히 귀찮게 하면 안 되죠.","밥은 알아서 먹습니다.","도움을 받을 정도는 아닙니다."}),
            new CaseSpec("international-belonging-01", "유학생 소속감과 적응", "왕하오", "24세", "다문화·대학상담", "중급",
                "한국 대학원 생활 중 언어 부담과 배제감을 느끼지만 민감한 사람으로 보일까 말하지 못합니다.",
                "회의에서 제가 말하면 잠깐 조용해져요. 제가 한국말을 이상하게 해서 그런지 모르겠어요.",
                "문화적 차이를 단정하지 않고 이름 발음, 선호 언어, 설명 방식과 직접 시선의 편안함을 확인한다.", "정확한 한국어를 찾을 때 시선이 옆으로 이동한다. 이를 회피나 거짓으로 해석하지 않는다.", .46f, .38f, "gentle-international-korean", .64f, .62f,
                new[]{"문화적 설명을 내담자에게 확인한다.","언어 유창성과 정서 깊이를 혼동하지 않는다.","차별 가능성과 개인 해석을 모두 열어 둔다."},
                new[]{"틀린 말을 해도 기다려 주셔서 편해요.","회의 전에 할 말을 여러 번 연습해요.","농담을 못 알아들으면 다 같이 웃는데 저만 멈춰 있어요.","한 번은 제 의견을 다른 사람이 다시 말하자 받아들여졌어요.","그 뒤로 말하기 전에 제가 틀렸다고 먼저 말해요.","제 경험이 실제였다고 인정받고 싶어요."},
                new[]{"제가 한국말을 더 잘하면 되겠죠.","문화가 달라서 그런 것뿐이에요.","농담 얘기는 설명하기 어려워요.","차별이라고까지 말하고 싶지는 않아요.","제 자신감 문제일 수도 있어요.","그냥 적응해야 할 것 같아요."})
        };

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int index = path.LastIndexOf('/');
            string parent = path.Substring(0, index);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(index + 1));
        }

        private sealed class CaseSpec
        {
            public readonly string Id, Title, Name, Age, Domain, Difficulty, Concern, InitialLine, CulturalContext, NonverbalStyle, VoiceStyle;
            public readonly float GazeComfort, DisclosurePace, ExpressionIntensity, GazeIntensity;
            public readonly string[] Objectives, Supportive, Guarded;
            public CaseSpec(string id,string title,string name,string age,string domain,string difficulty,string concern,string initialLine,string culturalContext,string nonverbalStyle,float gazeComfort,float disclosurePace,string voiceStyle,float expressionIntensity,float gazeIntensity,string[] objectives,string[] supportive,string[] guarded)
            { Id=id;Title=title;Name=name;Age=age;Domain=domain;Difficulty=difficulty;Concern=concern;InitialLine=initialLine;CulturalContext=culturalContext;NonverbalStyle=nonverbalStyle;GazeComfort=gazeComfort;DisclosurePace=disclosurePace;VoiceStyle=voiceStyle;ExpressionIntensity=expressionIntensity;GazeIntensity=gazeIntensity;Objectives=objectives;Supportive=supportive;Guarded=guarded; }
        }
    }
}
