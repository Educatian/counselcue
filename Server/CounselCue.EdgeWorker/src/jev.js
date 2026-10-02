// Real-time analysis with Jev (TypeSafe's System-1 decision model) through OpenRouter's
// Decisions API, on the same OPENROUTER_API_KEY. Jev returns typed answers with calibrated
// probabilities in roughly 0.1–0.5 s, so the session can show a provisional skill code and the
// client's affect while the slower LLM coder (/code) writes the rationale and alternative.
// See Docs/REALTIME_ANALYSIS.md.

export const JEV_MODEL = "typesafe/jev-1.13";
export const JEV_ENDPOINT = "https://openrouter.ai/api/alpha/decisions";
export const ANALYSIS_VERSION = "jev-analysis-1";

// Codebook ko-codebook-1, phrased as Jev criteria (one line each, with Korean cues).
export const SKILL_CRITERIA = {
  premature_reassurance:
    "Reassures about the outcome or minimizes before the client feels understood (괜찮아질 거예요, 걱정 마세요, 누구나 그래요, 별거 아니에요).",
  advice:
    "Tells the client what to do outside the session or proposes a solution (~해 보세요, ~하는 게 좋아요, ~하셔야 해요). Not an invitation to keep talking in session.",
  why_question: "Asks the client to justify a feeling or act with 왜 (왜 그렇게 생각하세요?).",
  reflection_exploration:
    "In the same turn, reflects the client's feeling or meaning AND invites further exploration with an open question or invitation.",
  reflection:
    "Names or restates the client's feeling or meaning, usually tentatively (~하신 것 같아요, ~셨군요, ~게 느껴지시는군요), without a further question.",
  validation:
    "Affirms the experience as understandable without predicting the outcome (그럴 만해요, 충분히 그렇게 느끼실 수 있어요).",
  open_question: "Invites elaboration without reflecting (무엇/어떤/어떻게/조금 더 말씀해 주시겠어요).",
  closed_question: "Yes/no or narrow fact question (~하셨어요?, 몇 번이요?).",
  neutral:
    "Greetings, self-introductions, thanks, minimal encouragers (네, 음), structuring, information, or off-target talk. 안녕하세요 and 반갑습니다 are always neutral.",
  silence: "The utterance is empty.",
};

export const QUALITY_LEVELS = [
  "0: likely to harm the alliance (dismissive, judging, interrogating or rushing)",
  "1: partial, awkward or slightly off-target",
  "2: appropriate but generic",
  "3: accurate to what the client just said, specific, tentative and client-centred",
];

export const AFFECT_CRITERIA = {
  guarded: "Holding back, short, wary or defensive.",
  anxious: "Worried, tense, afraid or self-doubting.",
  relieved: "Tension easing, warmer, feeling understood.",
  thoughtful: "Reflective, searching for words, considering.",
};

export const INTENSITY_LEVELS = ["barely shows", "slightly", "clearly", "strongly"];

/** Jev questions for one counselor turn; the client-affect questions only when a reply is given. */
export function analysisQuestions(withClientReply) {
  const questions = {
    skill: {
      type: "choice",
      instructions:
        "Which counseling skill does the counselor_utterance use? If several fit, choose the one listed first. Judge only the counselor_utterance; client_line is context.",
      criteria: SKILL_CRITERIA,
    },
    quality: {
      type: "score",
      instructions:
        "How well does the counselor_utterance respond to the client_line at this session_phase? Judge reflection accuracy against what the client actually said.",
      criteria: QUALITY_LEVELS,
    },
    attends_to_feeling: {
      type: "noul",
      instructions: "Does the counselor_utterance acknowledge a feeling the client expressed in client_line?",
    },
  };
  if (withClientReply) {
    questions.client_affect = {
      type: "choice",
      instructions: "Which feeling does the client_reply show most?",
      criteria: AFFECT_CRITERIA,
    };
    questions.client_intensity = {
      type: "score",
      instructions: "How strongly does that feeling show in the client_reply?",
      criteria: INTENSITY_LEVELS,
    };
  }
  return questions;
}

const clamp01 = (v) => Math.max(0, Math.min(1, Number.isFinite(Number(v)) ? Number(v) : 0));
const round2 = (v) => Math.round(v * 100) / 100;

/** Normalizes a Decisions API response into the analysis the client and record use. */
export function analysisResult(body, latencyMs) {
  const a = (body && body.answers) || {};
  const skill = a.skill || {};
  if (!Object.hasOwn(SKILL_CRITERIA, skill.choice)) throw Error("jev: no skill choice");
  const quality = a.quality || {};
  const qualityScore = Math.max(0, Math.min(3, Number(quality.score) || 0));
  const out = {
    engine: "jev",
    version: ANALYSIS_VERSION,
    model: typeof body.model === "string" ? body.model.slice(0, 60) : "",
    code: skill.choice,
    confidence: round2(clamp01(skill.confidence ?? skill.probabilities?.[skill.choice])),
    probabilities: Object.fromEntries(
      Object.keys(SKILL_CRITERIA).map((k) => [k, round2(clamp01(skill.probabilities?.[k]))]),
    ),
    quality: Math.round(qualityScore),
    qualityScore: round2(qualityScore),
    qualityConfidence: round2(clamp01(quality.confidence)),
    attendsToFeeling: round2(clamp01(a.attends_to_feeling?.noul)),
    latencyMs: Math.max(0, Math.round(latencyMs || 0)),
  };
  if (a.client_affect && Object.hasOwn(AFFECT_CRITERIA, a.client_affect.choice)) {
    out.clientAffect = a.client_affect.choice;
    out.clientAffectConfidence = round2(clamp01(a.client_affect.confidence));
    out.clientIntensity = round2(Math.max(0, Math.min(3, Number(a.client_intensity?.score) || 0)) / 3);
  }
  return out;
}
