namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Phase of the counseling process a practice session is set in
    /// (after 상담면접의 기초 and Hill's Helping Skills).
    /// </summary>
    public enum CounselingPhase { Intake, GoalSetting, MiddleWork, Termination }

    /// <summary>
    /// Phase-specific overlay for a case: briefing text, the client's opening line,
    /// learning objectives, offline reply ladders, and the starting relational state.
    /// </summary>
    public sealed class CounselingPhaseVariant
    {
        /// <summary>Session label shown on the briefing card, e.g. "3회기".</summary>
        public string SessionLabelKo;
        /// <summary>English session label, e.g. "Session 3".</summary>
        public string SessionLabelEn;
        /// <summary>1–2 sentences on where therapy stands now.</summary>
        public string SituationKo;
        /// <summary>English version of <see cref="SituationKo"/>.</summary>
        public string SituationEn;
        /// <summary>What the client says first (Korean, multi-cue).</summary>
        public string OpeningLine;
        /// <summary>Exactly 3 learning objectives for this phase and case.</summary>
        public string[] ObjectivesKo;
        /// <summary>Exactly 3 objectives, same meaning as <see cref="ObjectivesKo"/>.</summary>
        public string[] ObjectivesEn;
        /// <summary>6 offline client lines used when the counselor responds well, progressively deeper.</summary>
        public string[] SupportiveReplies;
        /// <summary>6 offline client lines used when the counselor responds poorly.</summary>
        public string[] GuardedReplies;
        /// <summary>Starting relational state, each 0..1.</summary>
        public float Safety, Guardedness, Disclosure;
    }

    /// <summary>
    /// Static content for practising later counseling phases with the five sprint cases.
    /// The intake phase is covered by each case asset, so <see cref="TryGet"/> returns false for it.
    /// </summary>
    public static class CounselingPhaseLibrary
    {
        /// <summary>All phases in process order.</summary>
        public static readonly CounselingPhase[] All =
        {
            CounselingPhase.Intake, CounselingPhase.GoalSetting, CounselingPhase.MiddleWork, CounselingPhase.Termination
        };

        /// <summary>Stable key shared with the edge worker (phases.js).</summary>
        public static string Key(CounselingPhase phase)
        {
            switch (phase)
            {
                case CounselingPhase.GoalSetting: return "goal_setting";
                case CounselingPhase.MiddleWork: return "middle";
                case CounselingPhase.Termination: return "termination";
                default: return "intake";
            }
        }

        /// <summary>Parses a key produced by <see cref="Key"/>; unknown values map to Intake.</summary>
        public static CounselingPhase FromKey(string key)
        {
            switch (key)
            {
                case "goal_setting": return CounselingPhase.GoalSetting;
                case "middle": return CounselingPhase.MiddleWork;
                case "termination": return CounselingPhase.Termination;
                default: return CounselingPhase.Intake;
            }
        }

        /// <summary>Short Korean label for UI.</summary>
        public static string LabelKo(CounselingPhase phase)
        {
            switch (phase)
            {
                case CounselingPhase.GoalSetting: return "목표 설정";
                case CounselingPhase.MiddleWork: return "중반부 작업";
                case CounselingPhase.Termination: return "종결";
                default: return "접수·초기";
            }
        }

        /// <summary>Short English label for UI.</summary>
        public static string LabelEn(CounselingPhase phase)
        {
            switch (phase)
            {
                case CounselingPhase.GoalSetting: return "Goal setting";
                case CounselingPhase.MiddleWork: return "Middle phase";
                case CounselingPhase.Termination: return "Termination";
                default: return "Intake";
            }
        }

        /// <summary>One line on what trainees practise in the phase (Korean).</summary>
        public static string DescriptionKo(CounselingPhase phase)
        {
            switch (phase)
            {
                case CounselingPhase.GoalSetting: return "막연한 바람을 구체적인 목표로 바꾸고 강점·자원·동기를 함께 평가합니다.";
                case CounselingPhase.MiddleWork: return "저항과 양가감정을 논쟁 없이 다루고, 강한 감정에 머물며 담아 주는 연습을 합니다.";
                case CounselingPhase.Termination: return "변화를 함께 정리하고 종결의 복합적인 감정을 다루며 이후의 어려움에 대비합니다.";
                default: return "관계를 형성하고 비밀보장을 안내하며 호소 문제를 탐색합니다.";
            }
        }

        /// <summary>One line on what trainees practise in the phase (English).</summary>
        public static string DescriptionEn(CounselingPhase phase)
        {
            switch (phase)
            {
                case CounselingPhase.GoalSetting: return "Turn vague wishes into concrete goals and assess strengths, resources and motivation.";
                case CounselingPhase.MiddleWork: return "Work with resistance and ambivalence without arguing; stay with and contain strong feeling.";
                case CounselingPhase.Termination: return "Review change together, work with mixed feelings about ending, and prepare for setbacks.";
                default: return "Build rapport, explain confidentiality, and explore the presenting concern.";
            }
        }

        /// <summary>
        /// Returns a fresh variant for the case and phase. False for Intake (the case asset covers it)
        /// and for unknown case ids.
        /// </summary>
        public static bool TryGet(string caseId, CounselingPhase phase, out CounselingPhaseVariant variant)
        {
            variant = phase == CounselingPhase.Intake || caseId == null ? null : Build(caseId + "|" + Key(phase));
            return variant != null;
        }

        private static CounselingPhaseVariant Build(string id)
        {
            switch (id)
            {
                // ───────────────────────── 김지혜 (32) · 직장 불안 ─────────────────────────

                case "workplace-anxiety-01|goal_setting":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "2회기", SessionLabelEn = "Session 2",
                        SituationKo = "첫 회기에서 팀장에게 회의 중 지적받은 뒤 커진 출근 불안을 이야기했다. 오늘은 상담에서 무엇을 얻고 싶은지 함께 정해야 한다.",
                        SituationEn = "In session 1 she described the dread of going to work that grew after her team leader criticized her in a meeting. Today you need to agree on what she wants from counseling.",
                        OpeningLine = "지난번에 이야기하고 나서 며칠은 좀 가벼웠어요. 그런데 이번 주에도 보고서 메일을 보내기 전에 네 번이나 다시 확인했어요.\n목표를 정하자고 하셨는데… 그냥 좀 편해지고 싶다는 것밖에 모르겠어요.",
                        ObjectivesKo = new[]
                        {
                            "'편해지고 싶다'는 막연한 바람을 관찰 가능한 구체적 목표로 바꾼다.",
                            "재확인 행동, 팀장과의 관계, 퇴사 고민 중 무엇을 먼저 다룰지 내담자와 함께 정한다.",
                            "내담자의 강점과 지지 자원(지금까지 버텨 온 방식, 믿을 만한 동료)을 탐색한다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Turn the vague wish to 'feel at ease' into concrete, observable goals.",
                            "Decide with the client which to address first: re-checking, the team-leader relationship, or thoughts of resigning.",
                            "Explore her strengths and supports (how she has coped so far, a trusted colleague)."
                        },
                        SupportiveReplies = new[]
                        {
                            "편해진다는 게… 아침에 회사 앞에서 한참 서 있지 않고 그냥 들어가는 거요. 그 정도면 좋겠어요.",
                            "메일을 계속 확인하는 건 또 실수하면 그 회의 때처럼 될까 봐서예요. 두 번만 보고 보내도 괜찮으면 좋겠는데, 아직은 불안해요.",
                            "제일 먼저 바꾸고 싶은 건 팀장님 앞에서 얼어붙는 거예요. 퇴사는… 그건 좀 나중에 생각해도 될 것 같아요.",
                            "생각해 보니 입사 동기 한 명이 그날 회의 끝나고 말없이 커피를 사다 줬어요. 아무 말 안 했는데 그게 힘이 됐어요.",
                            "저 원래 일 못하는 사람은 아니었어요. 작년엔 큰 프로젝트도 잘 끝냈고요. 그걸 잊고 있었던 것 같아요.",
                            "이렇게 적어 보니까 막막한 게 조금 줄어요. 다음 주엔 메일은 두 번만 확인하고, 회의에서 한 번은 제 의견을 말해 볼게요."
                        },
                        GuardedReplies = new[]
                        {
                            "목표라고 하시니까 뭔가 제대로 대답해야 할 것 같아서 부담돼요.",
                            "그냥 제가 덜 예민해지면 되는 거 아닐까요.",
                            "우선순위요? 다 똑같이 힘든데요.",
                            "도와줄 사람은… 딱히 없는 것 같아요.",
                            "제가 잘하는 거요? 요즘은 잘 모르겠어요.",
                            "그냥 선생님이 정해 주시면 안 될까요?"
                        },
                        Safety = .55f, Guardedness = .40f, Disclosure = .45f
                    };

                case "workplace-anxiety-01|middle":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "7회기", SessionLabelEn = "Session 7",
                        SituationKo = "지난 회기에 '메일은 두 번까지만 확인하기'와 '회의에서 한 번 의견 말하기'를 작은 과제로 정했다. 오늘은 과제를 하나도 하지 못한 채 미안해하며 들어왔다.",
                        SituationEn = "Last session she agreed to two small steps: check emails at most twice, and voice one opinion in a meeting. Today she arrives apologizing, having done neither.",
                        OpeningLine = "죄송해요, 지난주에 정한 거 하나도 못 했어요. 회의 때 말하려고 입을 떼다가 팀장님이랑 눈이 마주쳐서 그냥 가만히 있었어요.\n선생님이 실망하실 것 같아서 사실 오늘 오기 싫었어요.",
                        ObjectivesKo = new[]
                        {
                            "과제를 못 한 것을 평가하거나 설득하지 않고, 머뭇거림 밑의 두려움을 반영한다.",
                            "'선생님이 실망할 것 같다'는 말을 즉시성으로 다루어 상담 관계 안에서 올라오는 불안을 탐색한다.",
                            "자책이 올라올 때 서둘러 달래지 않고 함께 머물며(버티기), 다음 단계는 내담자가 고르게 한다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Reflect the fear beneath the unfinished task without evaluating or persuading.",
                            "Use immediacy with 'you'll be disappointed in me' to explore the anxiety arising in the counseling relationship itself.",
                            "Stay with her self-blame without rushing to soothe it (버티기), and let her choose the next step."
                        },
                        SupportiveReplies = new[]
                        {
                            "혼날 줄 알았는데… 그렇게 말씀해 주시니까 좀 숨이 쉬어져요.",
                            "사실 선생님한테도 잘하는 모습만 보여 드리고 싶었어요. 여기서도 평가받는 느낌이 들 때가 있어요.",
                            "팀장님 눈을 본 순간 그때 회의실 공기가 그대로 떠올랐어요. 머리보다 몸이 먼저 굳더라고요.",
                            "못 한 게 아니라 무서웠던 거라고 생각하니까, 제가 조금 덜 한심하게 느껴져요.",
                            "어릴 때부터 실수하면 안 된다고 생각했어요. 그래서 하나 틀리면 제 전부가 틀린 것 같아요.",
                            "과제를 좀 더 작게 바꿔도 될까요? 회의 끝나고 메모로 의견을 보내는 것부터 해 보고 싶어요."
                        },
                        GuardedReplies = new[]
                        {
                            "그냥 제가 의지가 약한 거예요.",
                            "다음엔 꼭 할게요. 이번엔 그냥 넘어가 주세요.",
                            "왜 못 했는지는 저도 모르겠어요.",
                            "이런 거 해도 결국 달라지는 건 없는 것 같아요.",
                            "선생님이 저였으면 그냥 하셨겠죠.",
                            "오늘은 과제 얘기 말고 다른 얘기 하면 안 될까요?"
                        },
                        Safety = .50f, Guardedness = .60f, Disclosure = .40f
                    };

                case "workplace-anxiety-01|termination":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "11회기 (마지막)", SessionLabelEn = "Session 11 (final)",
                        SituationKo = "출근 불안이 줄었고, 팀장에게 피드백은 따로 해 달라고 요청하는 경험도 했다. 오늘은 마지막 회기다.",
                        SituationEn = "Her dread of going to work has eased, and she has asked her team leader to give feedback privately. Today is the final session.",
                        OpeningLine = "요즘은 회사 앞에서 멈춰 서는 날이 거의 없어요. 지난주엔 팀장님께 피드백은 따로 말씀해 달라고 부탁도 해 봤고요.\n그런데 오늘이 마지막이라고 생각하니까, 혼자서 다시 예전처럼 될까 봐 좀 무서워요.",
                        ObjectivesKo = new[]
                        {
                            "처음과 지금을 비교하며 변화와 그 변화를 만든 내담자의 노력을 함께 정리한다.",
                            "종결에 대한 아쉬움·두려움·고마움을 서둘러 안심시키지 않고 반영한다.",
                            "불안이 다시 올라올 상황을 예상하고, 그때 쓸 수 있는 방법과 사람을 확인한다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Compare then and now, consolidating the changes and the client's own effort behind them.",
                            "Reflect sadness, fear and gratitude about ending without rushing to reassure.",
                            "Anticipate situations where anxiety may return and confirm the strategies and people she can use."
                        },
                        SupportiveReplies = new[]
                        {
                            "처음 왔을 때는 제가 약해서 이렇다고만 생각했어요. 지금은 그냥 많이 겁이 났던 거라고 말할 수 있어요.",
                            "제일 도움이 된 건 선생님이 답을 바로 안 주신 거였어요. 처음엔 답답했는데, 제가 고를 수 있다는 느낌이 생겼어요.",
                            "언니한테도 얘기했어요. 왜 이제 말했냐고 하는데, 그 말이 싫지 않았어요.",
                            "인사 평가 시즌이 오면 또 숨이 막힐 것 같긴 해요. 그땐 메일 두 번 규칙이랑 언니한테 전화하는 것부터 떠올릴게요.",
                            "솔직히 몇 번만 더 만나면 안 되나 싶기도 해요. 근데 그게 불안해서인지 필요해서인지 잘 모르겠어요.",
                            "끝나는 게 서운하지만, 혼자서도 해 볼 수 있을 것 같아요. 정말 감사했어요."
                        },
                        GuardedReplies = new[]
                        {
                            "좋아진 건지 그냥 익숙해진 건지 모르겠어요.",
                            "제가 한 건 별로 없어요. 운이 좋았던 거죠.",
                            "끝나는 건 뭐, 괜찮아요. 원래 정해진 거니까요.",
                            "다시 나빠지면 그땐 그냥 회사를 그만둬야겠죠.",
                            "정리하자고 하시니까 시험 보는 것 같아요.",
                            "이제 제가 알아서 해야죠."
                        },
                        Safety = .75f, Guardedness = .22f, Disclosure = .65f
                    };

                // ─────────────────── 박서윤 (16) · 다문화 청소년 학업 압박 ───────────────────

                case "adolescent-pressure-01|goal_setting":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "2회기", SessionLabelEn = "Session 2",
                        SituationKo = "첫 회기에서 비밀보장의 범위를 확인한 뒤 성적 하락과 점심을 혼자 먹는 일을 조금 이야기했다. 오늘은 상담에서 바라는 것을 함께 정해야 한다.",
                        SituationEn = "In session 1 she checked the limits of confidentiality, then talked a little about falling grades and eating lunch alone. Today you need to agree on what she wants from counseling.",
                        OpeningLine = "목표 같은 거 생각해 오라고 해서 생각해 봤는데요, 그냥 엄마가 잔소리 안 했으면 좋겠어요. 요즘 집에 가면 제 방에만 있거든요.\n근데 그건 제가 바꿀 수 있는 게 아니잖아요.",
                        ObjectivesKo = new[]
                        {
                            "'엄마가 잔소리 안 했으면'처럼 타인이 바뀌길 바라는 목표를 서윤이 해 볼 수 있는 목표로 함께 바꾼다.",
                            "시험, 집, 학교 관계 중 무엇을 먼저 다룰지 서윤이 고르도록 돕는다.",
                            "서윤의 강점과 편이 되어 주는 사람을 탐색하되, 문화·종교를 문제의 원인으로 가정하지 않는다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Turn an other-focused goal ('Mom should stop nagging') into something Seo-yoon herself can try.",
                            "Help her choose what to work on first: tests, home, or relationships at school.",
                            "Explore her strengths and the people on her side without assuming culture or religion is the cause."
                        },
                        SupportiveReplies = new[]
                        {
                            "잔소리가 없어지면… 그냥 집에서 좀 숨 쉴 수 있을 것 같아요. 제 방 밖에서도요.",
                            "진짜 바라는 건 엄마가 제가 노력하는 걸 한 번이라도 알아주는 거예요. 게으른 게 아니라요.",
                            "제일 먼저는 시험이요. 시험지만 보면 머리가 하얘지는 거, 그거라도 좀 덜했으면 좋겠어요.",
                            "중학교 땐 수학 꽤 잘했어요. 그땐 문제 푸는 게 재밌었는데, 지금은 그 느낌이 기억도 안 나요.",
                            "다른 학교 간 중학교 친구 한 명이랑은 아직 톡해요. 걔는 제 스카프 얘기 안 물어보고 그냥 저로 대해요.",
                            "일주일에 한 번이라도 누구랑 점심 먹어 보는 거… 그건 목표로 해 볼 수 있을 것 같아요."
                        },
                        GuardedReplies = new[]
                        {
                            "목표요? 딱히 없는데요.",
                            "엄마가 바뀌어야 되는 건데 제가 뭘 해요.",
                            "그냥 성적 올리면 다 해결되는 거 아니에요?",
                            "잘하는 거 없어요.",
                            "친구 얘기는 패스할래요.",
                            "이거 적는 거 엄마한테 보여 주는 거 아니죠?"
                        },
                        Safety = .50f, Guardedness = .45f, Disclosure = .40f
                    };

                case "adolescent-pressure-01|middle":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "6회기", SessionLabelEn = "Session 6",
                        SituationKo = "그동안 시험 불안과 점심시간 이야기를 나누며 신뢰가 쌓여 왔다. 어제 담임 교사가 성적 문제로 어머니에게 연락했고, 서윤은 상담 내용이 새어 나간 건 아닌지 화가 난 채 들어왔다.",
                        SituationEn = "Trust had been building around test anxiety and lunchtimes. Yesterday her homeroom teacher called her mother about grades, and she arrives angry, suspecting the counselor leaked what she said.",
                        OpeningLine = "담임쌤이 어제 엄마한테 전화했대요. 엄마가 저한테 '상담까지 다닌다며?' 이러는데 진짜 어이없었어요.\n쌤이 말한 거 아니에요? 솔직히 여기 계속 와야 되는지 모르겠어요.",
                        ObjectivesKo = new[]
                        {
                            "비밀보장에 대한 의심과 짜증을 방어하거나 논쟁하지 않고 그대로 반영한다.",
                            "상담자와의 관계에서 일어난 일을 즉시성으로 다루고, 무엇을 공유했고 하지 않았는지 투명하게 설명한다.",
                            "'계속 와야 하나'라는 양가감정을 설득 없이 탐색하고 선택권을 서윤에게 돌려준다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Reflect her suspicion and irritation about confidentiality without defending or arguing.",
                            "Use immediacy about what happened between you, and say transparently what was and was not shared.",
                            "Explore her ambivalence about continuing without persuading, and return the choice to her."
                        },
                        SupportiveReplies = new[]
                        {
                            "쌤이 말 안 했다는 건 알겠어요. 근데 그 순간엔 여기도 똑같구나 싶었어요.",
                            "화난 것도 있는데, 사실 엄마가 알게 된 게 더 무서웠어요. 또 실망한 표정 지을까 봐요.",
                            "그 전화 받고 엄마가 한참 아무 말도 안 했어요. 차라리 소리 지르는 게 나았을 것 같아요.",
                            "여기는 학교에서 유일하게 제가 이것저것 설명 안 해도 되는 데라서요. 그래서 더 배신당한 느낌이었나 봐요.",
                            "어젯밤에 좀 울었어요. 아무한테도 말 안 했는데, 지금 처음 말하는 거예요.",
                            "계속 올게요. 대신 엄마한테 뭐 말해야 되면 저한테 먼저 말해 준다고 약속해 주세요."
                        },
                        GuardedReplies = new[]
                        {
                            "됐어요, 어차피 어른들은 다 연결돼 있잖아요.",
                            "화 안 났어요. 그냥 짜증 나는 거예요.",
                            "설명 안 해 주셔도 돼요. 이미 들었어요.",
                            "여기 와도 달라지는 거 없잖아요.",
                            "엄마 얘기는 하기 싫어요.",
                            "오늘은 그냥 빨리 끝내면 안 돼요?"
                        },
                        Safety = .45f, Guardedness = .70f, Disclosure = .35f
                    };

                case "adolescent-pressure-01|termination":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "10회기 (마지막)", SessionLabelEn = "Session 10 (final)",
                        SituationKo = "시험 불안이 줄고 점심을 함께 먹는 친구가 생겼으며, 성적표를 숨긴 일을 아버지에게 스스로 이야기했다. 오늘은 방학 전 마지막 회기다.",
                        SituationEn = "Her test panic has eased, she now eats lunch with a classmate, and she told her father herself about the hidden report card. Today is the last session before the school break.",
                        OpeningLine = "쌤, 요즘은 같은 반 애랑 점심 같이 먹어요. 기말 때도 시험지 받고 심장이 뛰긴 했는데, 숨 쉬고 다시 풀었어요.\n근데 방학 끝나면 여기 안 오는 거잖아요. 뭐, 별로 아쉬운 건 아닌데요.",
                        ObjectivesKo = new[]
                        {
                            "시험, 점심시간, 가족 관계에서 달라진 점을 서윤의 언어로 함께 정리한다.",
                            "'별로 아쉽지 않다'는 말 속의 서운함을 부드럽게 반영하되 억지로 인정하게 하지 않는다.",
                            "2학기에 힘들어질 때 기댈 사람과 상담실을 다시 찾는 방법을 구체적으로 확인한다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Review in her own words what changed around tests, lunchtimes and family.",
                            "Gently reflect the sadness under 'it's not like I'll miss it' without forcing her to admit it.",
                            "Make concrete whom she can lean on next semester and how to come back to counseling."
                        },
                        SupportiveReplies = new[]
                        {
                            "…사실 좀 아쉬워요. 여기가 학교에서 제일 편한 데였거든요.",
                            "처음엔 쌤도 엄마한테 다 말할 줄 알았어요. 근데 안 그래서, 어른 중에도 믿을 수 있는 사람이 있구나 했어요.",
                            "성적표 숨긴 거 아빠한테 제가 먼저 말했어요. 아빠가 좀 조용하더니 말해 줘서 고맙다고 했어요.",
                            "제일 도움 된 건 시험지 받으면 숨 세 번 쉬는 거요. 별거 아닌데 진짜 돼요.",
                            "2학기에 반 바뀌면 또 혼자 먹을까 봐 걱정돼요. 스카프 물어보는 애들도 또 있겠죠.",
                            "힘들면 다시 와도 되는 거죠? 그거 알면 좀 덜 불안할 것 같아요."
                        },
                        GuardedReplies = new[]
                        {
                            "그냥 그렇게 된 거예요. 제가 한 거 별로 없어요.",
                            "아쉬운 거 없다니까요.",
                            "좋아진 건지 모르겠어요. 방학이라 그런 걸 수도 있고요.",
                            "2학기 얘기는 지금 하기 싫어요.",
                            "다시 올 일은 없을걸요.",
                            "네, 뭐. 감사했어요."
                        },
                        Safety = .72f, Guardedness = .28f, Disclosure = .60f
                    };

                // ─────────────────────── 최민준 (39) · 경력 전환과 번아웃 ───────────────────────

                case "career-transition-01|goal_setting":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "3회기", SessionLabelEn = "Session 3",
                        SituationKo = "두 회기 동안 퇴사하고 싶은 마음과 가족 부양 책임 사이의 갈등을 이야기했다. 오늘 그는 '연말까지 결론 내기'를 목표로 내놓는다.",
                        SituationEn = "Over two sessions he described the pull between leaving and providing for his family. Today he proposes one goal: reach a decision by year-end.",
                        OpeningLine = "지난주에 생각해 봤는데요, 제 목표는 간단합니다. 연말까지 그만둘지 남을지 결론을 내는 거예요.\n그런데 막상 둘 중 하나를 고르려고 하면 아이들 얼굴이 떠오르고 가슴이 답답해집니다.",
                        ObjectivesKo = new[]
                        {
                            "'그만둘지 남을지 결론'이라는 결과 중심 목표 아래의 바람(통제감, 의미, 가족)을 탐색한다.",
                            "양자택일 목표를 과정 목표(가치 명료화, 작은 실험, 배우자와의 대화)로 나누고 우선순위를 정한다.",
                            "만드는 일의 즐거움과 책임감 등 내담자의 강점과 동기를 자원으로 확인한다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Explore the wishes (control, meaning, family) beneath the outcome-only goal of 'quit or stay'.",
                            "Break the either/or goal into process goals (clarifying values, small experiments, talking with his wife) and prioritize.",
                            "Identify strengths and motivation, such as his joy in making things and his sense of responsibility."
                        },
                        SupportiveReplies = new[]
                        {
                            "결론을 내면 적어도 이 붕 떠 있는 느낌은 끝날 것 같아서요. 생각해 보면 결론보다 그게 더 급한 것 같네요.",
                            "제가 원하는 건 회사를 떠나는 것 자체보다, 제 하루를 제가 정하는 느낌인 것 같습니다.",
                            "우선순위를 정하자면… 아내에게 솔직히 말하는 게 먼저일지도 모르겠습니다. 그게 제일 피하던 거라서요.",
                            "주말에 아이들이랑 레고를 만들 때는 시간 가는 줄 모릅니다. 그런 감각이 아직 남아 있긴 하네요.",
                            "책임감이 저를 붙잡는 줄만 알았는데, 그게 여기까지 버틴 힘이기도 했네요.",
                            "연말 결론 대신, 한 달에 한 번 작은 실험을 해 보는 걸 목표로 적어 보고 싶습니다."
                        },
                        GuardedReplies = new[]
                        {
                            "목표는 결론이라고 말씀드렸잖아요.",
                            "감정 얘기보다는 현실적인 판단이 필요합니다.",
                            "우선순위야 당연히 가족이죠.",
                            "강점이라고 할 만한 건 딱히 없습니다. 그냥 버틴 거죠.",
                            "아내한테 말하는 건 결론이 난 다음에 할 일입니다.",
                            "상담으로 이런 게 정리가 되긴 하는 건가요?"
                        },
                        Safety = .58f, Guardedness = .38f, Disclosure = .48f
                    };

                case "career-transition-01|middle":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "7회기", SessionLabelEn = "Session 7",
                        SituationKo = "가치와 양가감정을 탐색했고 배우자와의 대화도 이야기했지만 아직 실행하지 못했다. 오늘 그는 장단점 표를 들고 와 상담이 효과가 있는지 의문을 제기한다.",
                        SituationEn = "Values and ambivalence have been explored, and talking with his wife was discussed but not yet done. Today he brings a pros-and-cons spreadsheet and questions whether counseling helps.",
                        OpeningLine = "이번 주에 이직의 장단점을 엑셀로 정리해 봤습니다. 점수로 따지면 남는 쪽이 조금 높더군요.\n솔직히 말씀드리면, 몇 주째 마음 얘기만 하는 게 무슨 도움이 되는지 모르겠습니다.",
                        ObjectivesKo = new[]
                        {
                            "상담 효과에 대한 의문과 답답함을 방어하거나 설득하지 않고 반영한다.",
                            "장단점 표라는 지적 정리 뒤에 있는, 점수로 담기지 않는 감정을 부드럽게 탐색한다.",
                            "지금 이 자리에서의 불만을 즉시성으로 다루고 상담 방향을 함께 조정한다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Reflect his doubt and frustration about counseling without defending or persuading.",
                            "Gently explore the feelings behind the spreadsheet that a score cannot hold.",
                            "Address his here-and-now dissatisfaction with immediacy and adjust the direction together."
                        },
                        SupportiveReplies = new[]
                        {
                            "기분 나쁘실 줄 알았는데, 그렇게 받아 주시니 말하기가 좀 편해집니다.",
                            "표를 만들면 정리될 줄 알았는데, 남는 쪽 점수가 높게 나오니까 오히려 더 가라앉더군요.",
                            "생각해 보니 표에는 '아침마다 사라지는 느낌'을 넣을 칸이 없었습니다.",
                            "답답했던 건 상담이라기보다, 제가 계속 결정을 미루고 있다는 사실인 것 같습니다.",
                            "아내에게 말하겠다고 한 것도 아직 못 했습니다. 실망시킬까 봐… 사실 그게 제일 무섭습니다.",
                            "다음엔 표 말고, 아내에게 건넬 수 있는 첫 문장을 같이 생각해 보고 싶습니다."
                        },
                        GuardedReplies = new[]
                        {
                            "제 말은 결국 방법을 좀 알려 달라는 겁니다.",
                            "감정은 이미 충분히 이야기한 것 같은데요.",
                            "표로 보면 명확하잖아요. 뭐가 더 필요합니까.",
                            "선생님은 결정을 안 하셔도 되니까 편하시겠죠.",
                            "아내 얘기는 오늘 안 하겠습니다.",
                            "이런 식이면 몇 번 더 올지 생각해 봐야겠습니다."
                        },
                        Safety = .48f, Guardedness = .62f, Disclosure = .38f
                    };

                case "career-transition-01|termination":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "12회기 (마지막)", SessionLabelEn = "Session 12 (final)",
                        SituationKo = "당장 퇴사하는 대신 아내에게 솔직히 털어놓았고, 주말 제작 모임과 사내 다른 팀 알아보기를 작은 실험으로 시작했다. 오늘은 마지막 회기다.",
                        SituationEn = "Instead of quitting, he opened up to his wife and began small experiments: a weekend making group and exploring another team at work. Today is the final session.",
                        OpeningLine = "아내에게 털어놓고 나서 제일 크게 달라진 건, 아침에 혼자 버티는 느낌이 줄었다는 겁니다. 회사는 아직 그대로 다니고 있고요.\n그런데 이 시간이 없어지면 제가 또 예전처럼 저를 지워 버릴까 봐 걱정됩니다.",
                        ObjectivesKo = new[]
                        {
                            "퇴사 여부가 아닌 과정의 변화(배우자와의 대화, 작은 실험, 통제감)를 내담자의 언어로 정리한다.",
                            "종결에 대한 걱정과 아쉬움, 정리를 대신 받고 싶은 마음을 반영하며 변화를 내담자의 것으로 돌려준다.",
                            "다시 '지워지는' 신호가 올 때 알아차릴 단서와 스스로 쓸 방법을 함께 준비한다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Consolidate process changes (talking with his wife, small experiments, sense of control) in his own words rather than the quit decision.",
                            "Reflect worry, regret and the wish to be handed a summary, and return ownership of change to him.",
                            "Prepare early-warning signs of feeling 'erased' again and the steps he can take himself."
                        },
                        SupportiveReplies = new[]
                        {
                            "처음엔 여기서 답을 받아 갈 줄 알았습니다. 결국 답은 없었는데, 이상하게 덜 막막합니다.",
                            "도움이 된 건 두 마음을 다 인정해도 된다는 거였습니다. 떠나고 싶은 마음이 무책임한 건 아니더군요.",
                            "주말 모임에서 작은 선반 하나를 만들었는데, 그다음 날 아침은 사라지는 느낌이 없었습니다.",
                            "연말에 원치 않는 발령이 나면 다시 흔들릴 것 같습니다. 그땐 표보다 아내와 먼저 이야기하겠습니다.",
                            "솔직히 몇 달에 한 번이라도 점검하듯 오면 안 되나 생각했습니다. 기댈 데가 없어지는 게 아쉬워서요.",
                            "여기서 제 목소리를 좀 되찾은 것 같습니다. 고맙다는 말을 제대로 하고 싶었습니다."
                        },
                        GuardedReplies = new[]
                        {
                            "결국 달라진 건 없죠. 회사도 그대로고요.",
                            "정리해 주시면 받아 적겠습니다.",
                            "아쉬울 게 뭐 있겠습니까. 할 만큼 했죠.",
                            "다시 흔들리면 그때는 그냥 결단하겠습니다.",
                            "마지막이니 간단히 하시죠.",
                            "도움이 됐는지는 좀 더 지나 봐야 알 것 같습니다."
                        },
                        Safety = .76f, Guardedness = .25f, Disclosure = .66f
                    };

                // ─────────────────────── 이정호 (68) · 노년기 사별과 고립 ───────────────────────

                case "older-bereavement-01|goal_setting":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "2회기", SessionLabelEn = "Session 2",
                        SituationKo = "첫 회기에서 아내를 떠나보낸 뒤의 조용한 집과 흐트러진 식사·수면을 이야기했다. 오늘은 상담에서 바라는 것을 함께 정해야 한다.",
                        SituationEn = "In session 1 he spoke of the silent house since his wife's death and his disrupted meals and sleep. Today you need to agree on what he wants from counseling.",
                        OpeningLine = "목표를 생각해 보라고 하셨는데, 이 나이에 목표랄 게 있나 싶습니다. 그냥 하루가 좀 덜 길었으면 합니다.\n지난주에 딸아이가 전화를 했는데, 괜찮다고만 하고 끊었습니다.",
                        ObjectivesKo = new[]
                        {
                            "'하루가 덜 길었으면'이라는 바람을 식사·수면·대화 등 일상의 구체적 목표로 함께 풀어낸다.",
                            "나이에 대한 체념을 서둘러 반박하지 않고, 내담자가 원하는 삶의 모습을 존중하며 탐색한다.",
                            "자녀·이웃·지역 자원 등 지지 체계와 내담자가 지금까지 버텨 온 힘을 확인한다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Unpack the wish for 'days that feel less long' into concrete everyday goals (meals, sleep, conversation).",
                            "Explore the life he wants without hurrying to contradict his resignation about age.",
                            "Identify supports (children, neighbours, community) and the strength that has carried him so far."
                        },
                        SupportiveReplies = new[]
                        {
                            "하루가 긴 건… 특히 오후 네 시쯤입니다. 집사람이랑 공원 한 바퀴 돌던 시간이라서요.",
                            "딸아이한테 괜찮다고 한 건 걱정 끼치기 싫어서였습니다. 사실 목소리를 들으니 반가웠어요.",
                            "먼저 바라는 게 있다면 끼니는 좀 챙기고 싶습니다. 집사람이 그건 늘 잔소리했거든요.",
                            "아파트 경로당에 아는 분이 한 분 있긴 합니다. 한번 나오라고 몇 번 그러더군요.",
                            "평생 일하면서 힘든 일도 넘겨 왔습니다. 이것도 어떻게든 넘길 수는 있겠지요.",
                            "일주일에 한 번은 딸아이한테 제가 먼저 전화해 보는 걸로 정해 보면 어떨까 싶습니다."
                        },
                        GuardedReplies = new[]
                        {
                            "목표 같은 건 젊은 사람들 얘기지요.",
                            "그냥 시간이 지나면 나아지겠지요.",
                            "딸아이는 바쁩니다. 괜히 부담 줄 필요 없습니다.",
                            "밥은 대충 먹으면 됩니다.",
                            "제 나이에 새로 뭘 시작하는 건 좀 그렇습니다.",
                            "선생님이 좋다고 하시는 대로 하겠습니다."
                        },
                        Safety = .52f, Guardedness = .42f, Disclosure = .42f
                    };

                case "older-bereavement-01|middle":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "8회기", SessionLabelEn = "Session 8",
                        SituationKo = "식사와 딸과의 통화가 조금씩 자리를 잡아 왔다. 이번 주 아내의 옷장을 정리하다 손글씨 쪽지를 발견했고, 오늘은 슬픔이 크게 밀려온 상태로 왔다.",
                        SituationEn = "Meals and calls with his daughter had begun to settle. This week, clearing his wife's wardrobe, he found a note in her handwriting, and today grief floods in as he speaks.",
                        OpeningLine = "이번 주에 아이들 말대로 집사람 옷장을 정리하기 시작했습니다. 겨울 코트 주머니에서 장 볼 목록이 적힌 쪽지가 나왔는데, 그 글씨를 보는데 그만…\n죄송합니다. 이 나이에 남 앞에서 이러면 안 되는데요.",
                        ObjectivesKo = new[]
                        {
                            "밀려오는 슬픔을 멈추게 하거나 고치려 하지 않고, 침묵과 함께 곁에 머문다(버티기·담아두기).",
                            "'이러면 안 된다'는 수치심을 존중하며 반영하고, 여기서는 울어도 괜찮다는 것을 전한다.",
                            "감정이 가라앉은 뒤에야 오늘 귀가 후의 일상과 연락할 사람을 부드럽게 확인한다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Stay with the surge of grief and the silence without stopping or fixing it (버티기, 담아두기).",
                            "Respectfully reflect his shame ('I shouldn't do this') and convey that tears are allowed here.",
                            "Only once the feeling settles, gently check on the rest of his day and whom he can contact."
                        },
                        SupportiveReplies = new[]
                        {
                            "…고맙습니다. 그냥 두셔서요. 좀 숨이 돌아옵니다.",
                            "'두부, 대파, 당신 좋아하는 귤.' 그렇게 적혀 있었습니다. 마지막까지 제 것부터 챙겼더군요.",
                            "사십 년을 같이 살았는데 고맙다는 말을 제대로 못 했습니다. 그게 제일 걸립니다.",
                            "아이들 앞에서는 한 번도 울지 않았습니다. 아비가 무너지면 안 된다고 생각했어요.",
                            "옷장은 아직 반도 못 했습니다. 서두르지 않아도 된다고 하시니 그 말이 위로가 됩니다.",
                            "그 쪽지는 버리지 않고 수첩에 끼워 두려고 합니다. 오늘은 집에 가서 딸아이한테 전화 한 통 해야겠습니다."
                        },
                        GuardedReplies = new[]
                        {
                            "괜찮습니다. 금방 추스르겠습니다.",
                            "다른 얘기를 하시지요.",
                            "다 지난 일입니다. 울어 봐야 뭐 하겠습니까.",
                            "이런 모습 보이려고 온 게 아닌데요.",
                            "정리는 해야지요. 언제까지 둘 수는 없으니까요.",
                            "오늘은 이만 일어나 보겠습니다."
                        },
                        Safety = .52f, Guardedness = .58f, Disclosure = .44f
                    };

                case "older-bereavement-01|termination":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "11회기 (마지막)", SessionLabelEn = "Session 11 (final)",
                        SituationKo = "식사가 규칙적이 되었고, 딸에게 먼저 전화하며 경로당 모임에도 나가기 시작했다. 오늘은 마지막 회기이며, 다음 달이 아내의 첫 기일이다.",
                        SituationEn = "His meals are regular, he calls his daughter first, and he has started going to the senior centre. Today is the final session, and his wife's first memorial anniversary is next month.",
                        OpeningLine = "요즘은 아침을 챙겨 먹고, 화요일마다 경로당에 나갑니다. 딸아이한테도 제가 먼저 전화를 하고요.\n그런데 오늘로 끝이라고 생각하니, 또 누구를 떠나보내는 것 같아 마음이 좀 이상합니다.",
                        ObjectivesKo = new[]
                        {
                            "식사·연락·모임 등 일상의 변화를 확인하고, 그 변화를 이끈 내담자의 선택을 인정한다.",
                            "종결이 또 다른 이별처럼 느껴지는 마음을 서둘러 달래지 않고 충분히 반영한다.",
                            "다가오는 첫 기일처럼 슬픔이 다시 커질 때를 예상하고, 연락할 사람과 할 수 있는 일을 정리한다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Review changes in meals, contact and activities, and acknowledge the choices he made.",
                            "Fully reflect that ending feels like another farewell, without hurrying to console.",
                            "Anticipate times grief may deepen, such as the first anniversary, and plan whom to call and what he can do."
                        },
                        SupportiveReplies = new[]
                        {
                            "처음 왔을 때는 이런 이야기를 남한테 한다는 게 부끄러웠습니다. 지금은 해도 괜찮다는 걸 압니다.",
                            "도움이 된 건 선생님이 재촉하지 않으신 거였습니다. 그 사람 이야기를 천천히 할 수 있었지요.",
                            "다음 달이 집사람 첫 기일입니다. 그날은 아이들과 같이 산소에 가기로 했습니다.",
                            "그날 지나고 또 한동안 가라앉을 수도 있겠지요. 그때는 혼자 버티지 말고 딸아이한테 말하겠습니다.",
                            "차는 요즘 한 잔만 준비합니다. 가끔 두 잔 꺼낼 때도 있지만, 이제는 그냥 웃고 맙니다.",
                            "여기서 들어 주신 덕분에 다시 사람들 사이로 나갈 수 있었습니다. 고맙습니다."
                        },
                        GuardedReplies = new[]
                        {
                            "좋아졌다기보다 그냥 익숙해진 거지요.",
                            "끝나는 건 괜찮습니다. 선생님도 바쁘실 텐데요.",
                            "기일은 뭐, 그냥 지나가겠지요.",
                            "경로당은 그냥 시간 때우러 가는 겁니다.",
                            "아이들한테 기대는 건 아직 좀 그렇습니다.",
                            "이제 제가 알아서 하겠습니다."
                        },
                        Safety = .78f, Guardedness = .18f, Disclosure = .68f
                    };

                // ─────────────────── 왕하오 (24) · 유학생 소속감과 적응 ───────────────────

                case "international-belonging-01|goal_setting":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "2회기", SessionLabelEn = "Session 2",
                        SituationKo = "첫 회기에서 한국어 회의에서 느끼는 배제감과 예민한 사람으로 보일까 하는 걱정을 이야기했다. 오늘 그는 목표를 전부 자신의 한국어 실력에 두고 있다.",
                        SituationEn = "In session 1 he described feeling excluded in Korean-language meetings and fearing he seems oversensitive. Today he places the whole goal on his own Korean ability.",
                        OpeningLine = "지난번에 목표를 생각해 보라고 하셨죠. 음… 제 목표는 한국어를 더 잘하는 거예요. 그러면 다 해결될 것 같아요.\n그런데 이번 주 랩 미팅에서도 준비한 말을 결국 못 했어요.",
                        ObjectivesKo = new[]
                        {
                            "'한국어를 더 잘하면 된다'는 목표를 존중하면서 그 아래의 바람(끝까지 들리기, 인정, 소속감)을 탐색한다.",
                            "언어 실력, 회의 참여, 관계 등 여러 목표 가운데 무엇을 먼저 할지 내담자가 고르도록 돕는다.",
                            "문화로 모든 것을 설명하지 않고 내담자의 강점과 지지 자원(동료, 지도교수, 유학생 공동체)을 확인한다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Respect the goal of 'better Korean' while exploring the wishes beneath it (being heard out, recognition, belonging).",
                            "Help him choose which goal comes first: language, meeting participation, or relationships.",
                            "Without explaining everything by culture, identify his strengths and supports (labmates, advisor, international-student community)."
                        },
                        SupportiveReplies = new[]
                        {
                            "한국어를 잘하면… 사람들이 제 생각을 끝까지 들어 줄 것 같아요. 사실 그게 원하는 거예요.",
                            "준비한 말을 못 한 건, 음… 첫 문장을 틀리면 다들 또 조용해질까 봐서요.",
                            "제일 먼저 원하는 건 회의에서 한 번이라도 제 이름으로 의견을 말하는 거예요. 다른 사람이 대신 말하는 거 말고요.",
                            "중국에서 학부 때는 발표를 꽤 잘했어요. 말하는 걸 원래 싫어하는 사람은 아니에요.",
                            "같은 랩에 베트남에서 온 친구가 한 명 있어요. 그 친구도 비슷하다고 해서, 둘이 있으면 좀 편해요.",
                            "지도교수님께 회의 자료를 미리 받을 수 있는지 물어보는 거, 그건 목표로 해 볼 수 있을 것 같아요."
                        },
                        GuardedReplies = new[]
                        {
                            "그냥 한국어 공부를 더 열심히 하면 될 것 같아요.",
                            "다른 목표는… 잘 모르겠어요.",
                            "제가 너무 예민한 걸 수도 있어요.",
                            "잘하는 거는, 음… 특별히 없어요.",
                            "교수님께 그런 부탁을 하는 건 좀 실례일 것 같아요.",
                            "선생님이 보시기에는 뭐가 제일 중요해요?"
                        },
                        Safety = .56f, Guardedness = .40f, Disclosure = .44f
                    };

                case "international-belonging-01|middle":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "6회기", SessionLabelEn = "Session 6",
                        SituationKo = "회의에서의 경험을 실제로 있었던 일로 다루며 작은 말하기 시도를 이어 왔다. 이번 주 선배의 친절을 경험한 뒤, 그는 자신이 과장했다고 느끼며 상담을 그만둘까 고민한다.",
                        SituationEn = "Earlier sessions treated his meeting experiences as real, and he began small attempts to speak. After a senior labmate's kindness this week, he feels he exaggerated and is thinking of stopping counseling.",
                        OpeningLine = "이번 주에 선배가 저한테 먼저 밥 먹자고 했어요. 그래서 생각했는데, 제가 지금까지 좀 과장해서 말한 것 같아요.\n괜히 선생님 시간을 뺏는 것 같아서, 음… 상담은 이제 그만해도 될 것 같기도 해요.",
                        ObjectivesKo = new[]
                        {
                            "'과장했다'는 말을 반박하거나 되돌리려 하지 않고, 좋은 경험과 힘든 경험이 함께 있음을 반영한다.",
                            "'선생님 시간을 뺏는다'는 미안함을 즉시성으로 다루며 상담 관계 안의 마음을 탐색한다.",
                            "그만두고 싶다는 뜻을 존중하되 성급히 동의하거나 붙잡지 않고, 그 뒤의 양가감정을 함께 살핀다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Without refuting 'I exaggerated', reflect that the kind moment and the hard ones can both be true.",
                            "Use immediacy with his guilt about 'taking your time' to explore what is happening in the relationship.",
                            "Respect his wish to stop without quickly agreeing or holding on, and explore the ambivalence behind it."
                        },
                        SupportiveReplies = new[]
                        {
                            "선배랑 밥 먹은 건 진짜 좋았어요. 그런데 그러고 나니까 전에 한 말들이 좀 부끄러워졌어요.",
                            "제가 불편하다고 말하면, 한국 사람들을 나쁘게 말하는 것 같아서 마음이 안 좋아요.",
                            "사실 그다음 날 회의에서는 또 제 말 뒤에 조용해졌어요. 그건 과장이 아니었어요.",
                            "여기서 힘든 얘기를 하면 제가 약한 사람이 되는 것 같았어요. 그래서 그만두고 싶었나 봐요.",
                            "좋은 사람도 있고 불편한 순간도 있고… 둘 다 맞다고 생각하니까 좀 편해져요.",
                            "그만두는 건 조금 더 생각해 볼게요. 다음 주에는 제가 낸 의견을 끝까지 제가 설명해 보고 싶어요."
                        },
                        GuardedReplies = new[]
                        {
                            "아니에요, 정말 괜찮아졌어요.",
                            "다 제가 적응을 잘 못해서 그런 거예요.",
                            "다른 유학생들은 더 힘든데요, 뭐.",
                            "선생님은 바쁘시잖아요. 저보다 더 필요한 사람이 있을 거예요.",
                            "그 얘기는 이제 안 해도 될 것 같아요.",
                            "네, 그럼 오늘이 마지막이어도 괜찮아요."
                        },
                        Safety = .53f, Guardedness = .56f, Disclosure = .42f
                    };

                case "international-belonging-01|termination":
                    return new CounselingPhaseVariant
                    {
                        SessionLabelKo = "10회기 (마지막)", SessionLabelEn = "Session 10 (final)",
                        SituationKo = "회의에서 의견을 끝까지 설명하고 '제가 틀렸을 수도 있는데'라는 서두를 줄였으며, 유학생 모임에도 나가기 시작했다. 학기 말인 오늘은 마지막 회기다.",
                        SituationEn = "He now explains his ideas fully in meetings, has dropped the 'I might be wrong' preface, and joined an international-student group. Today, at semester's end, is the final session.",
                        OpeningLine = "요즘은 회의에서 말하기 전에 '제가 틀렸을 수도 있는데'라고 안 해요. 지난주엔 제 의견을 끝까지 설명했고, 교수님이 좋다고 하셨어요.\n그런데 다음 학기에 새 사람들이 오면, 제가 다시 조용해질까 봐 조금 걱정돼요.",
                        ObjectivesKo = new[]
                        {
                            "회의 참여, 자기 비하적 서두, 관계에서의 변화를 내담자의 말로 확인하고 그 과정을 함께 정리한다.",
                            "종결에 대한 고마움과 아쉬움, 혼자 해낼 수 있을지에 대한 걱정을 서두르지 않고 반영한다.",
                            "새 학기처럼 다시 배제감을 느낄 수 있는 상황을 예상하고, 그때 쓸 방법과 사람을 구체화한다."
                        },
                        ObjectivesEn = new[]
                        {
                            "Confirm in his words the changes in meeting participation, self-deprecating prefaces and relationships, and consolidate how they happened.",
                            "Reflect gratitude, sadness and worry about coping alone without rushing.",
                            "Anticipate situations like a new semester where exclusion may recur, and make his strategies and supports concrete."
                        },
                        SupportiveReplies = new[]
                        {
                            "처음에는 제 경험이 진짜인지도 확신이 없었어요. 지금은 그런 일이 있었다고 말할 수 있어요.",
                            "제일 도움이 된 건 제가 단어를 찾을 때 선생님이 기다려 준 거예요. 여기서는 천천히 말해도 됐어요.",
                            "지난달에 유학생 모임에도 나가 봤어요. 비슷한 얘기를 하는 사람이 많아서 놀랐어요.",
                            "새 사람들이 오면 첫 회의에서 제가 먼저 자기소개를 하려고요. 조용해지기 전에요.",
                            "솔직히 좀 아쉬워요. 한국에서 제 얘기를 이렇게 다 한 사람은 선생님이 처음이에요.",
                            "중국어로도 한국어로도 감사하다는 말이 부족한 것 같아요. 정말 고마웠어요."
                        },
                        GuardedReplies = new[]
                        {
                            "좋아진 건 그냥 제 한국어가 늘어서인 것 같아요.",
                            "선생님이 다 해 주신 거예요.",
                            "다음 학기는… 그때 가 봐야 알 것 같아요.",
                            "아쉬운 건 괜찮아요. 원래 끝나는 거니까요.",
                            "다시 힘들어지면 그냥 참으면 돼요.",
                            "네, 이제 혼자 해 볼게요."
                        },
                        Safety = .74f, Guardedness = .26f, Disclosure = .62f
                    };

                default:
                    return null;
            }
        }
    }
}
