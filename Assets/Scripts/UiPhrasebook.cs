using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Display-only Korean→English phrases for runtime feedback and debrief text. Research
    /// logs keep the canonical Korean labels; only what is drawn on screen is translated.
    /// Counselor and client utterances are never translated.
    /// </summary>
    public static class UiPhrasebook
    {
        private static readonly KeyValuePair<string, string>[] Phrases = Sorted(new Dictionary<string, string>
        {
            // Coaching feedback (RelationalDeliveryModel)
            { "관계가 아직 경계된 상태입니다. 해결책보다 감정 반영과 탐색을 먼저 시도해 보세요.", "The relationship is still guarded. Try reflecting feelings and exploring before offering solutions." },
            { "안심시키기 전에 내담자가 이해받았다고 느끼도록 감정을 먼저 반영해 보세요.", "Before reassuring, reflect the feeling so the client feels understood." },
            { "비언어 근거가 없어 언어 기술만 반영했습니다.", "No nonverbal evidence was available, so only the verbal skill was used." },
            { "공감 문장은 적절했지만 이마 긴장 단서가 함께 관찰됐습니다. 표정에 힘을 빼고 한 박자 쉬어 보세요.", "The empathic wording fit, but brow tension was observed at the same time. Relax your face and pause for a beat." },
            { "고통을 다루는 순간 미소 단서가 함께 관찰됐습니다. 맥락에 맞는 따뜻한 중립 표정을 점검해 보세요.", "A smile cue appeared while addressing distress. Check for a warm, neutral expression that fits the moment." },
            { "언어 기술과 현재 관찰된 얼굴 전달 단서가 조화를 이룹니다.", "Your verbal skill and the observed facial delivery cues fit together." },
            { "현재 관찰된 전달 단서와 뚜렷한 충돌이 없습니다.", "No clear conflict with the observed delivery cues." },
            // Skills and alignment labels
            { "감정 반영 + 탐색", "Reflection + exploration" },
            { "공감적 반응", "Empathic response" },
            { "감정 반영", "Reflection" },
            { "닫힌 질문", "Closed question" },
            { "AI 코딩", "AI coding" },
            { "규칙 코딩", "Rule coding" },
            { "개방형 질문", "Open question" },
            { "성급한 조언", "Premature advice" },
            { "성급한 안심", "Premature reassurance" },
            { "'왜' 질문", "'Why' question" },
            { "중립 반응", "Neutral response" },
            { "전달 불일치 가능성", "Possible delivery mismatch" },
            { "관계 순서 불일치", "Relational order mismatch" },
            { "비언어 근거 없음", "No nonverbal evidence" },
            { "전달 정합", "Delivery aligned" },
            { "로컬 사례", "Local case" },
            { "AI 페르소나 + ElevenLabs", "AI persona + ElevenLabs" },
            // Focus prompts
            { " · 집중목표: ", " · Focus: " },
            { "감정 단어와 그 의미를 한 문장에 담아 보세요.", "Put a feeling word and its meaning in one sentence." },
            { "예·아니오로 끝나지 않는 질문 뒤 응답 공간을 남기세요.", "Ask a question that cannot end in yes or no, then leave space." },
            { "문장 내용과 얼굴의 긴장·미소가 같은 메시지인지 확인하세요.", "Check that your words and your facial tension or smile send the same message." },
            // Session messages
            { "AI 내담자 응답 생성 중…", "Generating the AI client's reply…" },
            { "응답을 분석하는 중…", "Analyzing your response…" },
            // Live voice (Gemini Live)
            { "텍스트로 응답하고, 내담자는 AI 음성으로 답합니다.", "You type; the client answers with an AI voice." },
            { "마이크 음성이 Google Gemini로 실시간 전송됩니다 · 원음은 저장하지 않습니다.", "Your microphone audio streams to Google Gemini in real time · CounselCue stores no audio." },
            { "실시간 음성은 웹 버전(Chrome·Edge)에서 사용할 수 있습니다.", "Live voice is available in the web version (Chrome or Edge)." },
            { "마이크 권한이 없어 텍스트 대화로 계속합니다.", "No microphone permission, so the session continues in text." },
            { "이 브라우저는 실시간 음성을 지원하지 않아 텍스트 대화로 계속합니다.", "This browser does not support live voice, so the session continues in text." },
            { "실시간 음성 연결이 끊겨 텍스트 대화로 전환했습니다.", "The live voice connection dropped, so the session switched to text." },
            { "Gemini Live 음성", "Gemini Live voice" },
            { "듣는 중", "Listening" },
            { "이전 응답 요청이 취소되었습니다. 내용을 확인한 뒤 다시 보내세요.", "The previous request was canceled. Check your response and send it again." },
            { "선택 장면 재연습 · 원래 응답: ", "Scene replay · original response: " },
            { " · 다른 전달을 시도해 보세요.", " · Try a different delivery." },
            // Debrief and reflection
            { "시간이 종료되었습니다", "Time is up" },
            { "장면 재연습 결과", "Scene replay result" },
            { "세션 성찰 및 재연습", "Reflect and retry" },
            { "연습이 종료되었습니다.\n장면을 선택하고 먼저 자신의 판단을 남겨주세요.", "Practice has ended.\nSelect a scene and record your own judgment first." },
            { "완료된 상담자 응답이 없습니다. 브리핑으로 돌아가 새 연습을 시작하세요.", "There are no completed counselor responses. Return to the briefing to start a new practice." },
            { "자기평가할 장면 없음", "No scenes to self-assess" },
            { "장면을 선택하면 상담자 응답과 시스템 근거가 표시됩니다.", "Select a scene to review the counselor response and system evidence." },
            { "먼저 이 장면에 대한 자신의 판단을 선택하세요.", "Choose your own judgment for this scene first." },
            { "먼저 자신의 판단을 선택하세요.", "Choose your own judgment first." },
            { "시스템 근거  자기평가 후 공개됩니다.", "System evidence  revealed after your self-assessment." },
            { "시스템 근거  ", "System evidence  " },
            { "코딩 근거  ", "Coding rationale  " },
            { "장면 기록", "Scene record" },
            { "나의 판단 · ", "My judgment · " },
            { "잘된 장면", "Effective scene" },
            { "다시 연습 필요", "Needs another try" },
            { "장면을 선택하고 먼저 자신의 판단을 남긴 뒤, 시스템 근거와 비교해 보세요.", "Select a scene, record your own judgment first, then compare it with the system evidence." },
            { "관계 궤적  안전 ", "Relational trajectory  Safety " },
            { " · 경계 ", " · Guarded " },
            { " · 공개 ", " · Disclosure " },
            { "언어기술 평균 ", "Verbal skill average " },
            { " · 원래 ", " · original " },
            { " → 재시도 ", " → retry " },
            // Stages and modes (CounselingSessionFlow)
            { "관계 형성", "Rapport" },
            { "초기 탐색", "Initial exploration" },
            { "감정 심화", "Emotional deepening" },
            { "핵심 탐색", "Core exploration" },
            { "연습 모드", "Practice" },
            { "평가 모드", "Assessment" },
            { "집중연습", "Focused practice" },
            { "장면 재연습", "Scene replay" }
        });

        private static readonly Regex Counts = new Regex("(Delivery aligned|불일치 가능성) (\\d+)회");

        public static string Translate(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            string result = source;
            for (int i = 0; i < Phrases.Length; i++) result = result.Replace(Phrases[i].Key, Phrases[i].Value);
            result = Counts.Replace(result, match =>
                match.Groups[1].Value == "Delivery aligned" ? $"Delivery aligned {match.Groups[2].Value}×" : $"possible mismatch {match.Groups[2].Value}×");
            result = Regex.Replace(result, "(\\d+)/(\\d+)턴", "$1/$2 turns");
            result = Regex.Replace(result, "(\\d+)턴", "Turn $1");
            result = Regex.Replace(result, "(^|\\s)정리(\\s|$)", "$1Consolidation$2");
            return Regex.Replace(result, "(^|\\s)종결(\\s|$)", "$1Closing$2");
        }

        /// <summary>
        /// Translates a debrief scene card line by line, leaving the counselor and client
        /// utterances untouched and translating only their prefixes.
        /// </summary>
        public static string TranslateScene(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            string[] lines = source.Split('\n');
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("상담자  ")) line = "Counselor  " + line.Substring("상담자  ".Length);
                else if (line.StartsWith("내담자  ")) line = "Client  " + line.Substring("내담자  ".Length);
                else if (line.StartsWith("코딩 근거  "))
                {
                    // The rationale is the coder's own Korean sentence; translate only the labels.
                    line = "Coding rationale  " + line.Substring("코딩 근거  ".Length)
                        .Replace("(AI 코딩)", "(AI coding)").Replace("(규칙 코딩)", "(rule coding)");
                }
                else line = Translate(line);
                if (i > 0) builder.Append('\n');
                builder.Append(line);
            }
            return builder.ToString();
        }

        private static KeyValuePair<string, string>[] Sorted(Dictionary<string, string> phrases)
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>(phrases);
            // Longest first so a sentence is replaced before any label it contains.
            list.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
            return list.ToArray();
        }
    }
}
