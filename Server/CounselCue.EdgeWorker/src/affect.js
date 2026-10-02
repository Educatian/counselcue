// Controllable expression pipeline (see Docs/EXPRESSION_PIPELINE.md).
//
//   appraisal (persona LLM: emotion, intensity, delivery note, tagged speech)
//     -> policy (this file: researcher controls, relational state, case voice, caps)
//     -> AffectPlan, rendered identically by the voice (Gemini 3.8 Flash TTS style + tags),
//        the Unity face/gesture layer, and logged with the policy version for research.
//
// The policy is deterministic: the same appraisal, state and controls always give the same
// plan, so a study can hold expressivity constant or lock an affect across participants.

export const EXPRESSION_POLICY = "expr-1";
export const AFFECTS = ["guarded", "anxious", "relieved", "thoughtful"];

export const EXPRESSIVITY = {
  restrained: { gain: 0.65, maxEvents: 0, label: "Keep emotion understated and controlled; feeling shows mostly in pace and pauses." },
  natural: { gain: 1.0, maxEvents: 1, label: "Let the feeling come through naturally, as a real client would, without performing it." },
  vivid: { gain: 1.3, maxEvents: 2, label: "Let the feeling come through clearly in the voice, still believable and never theatrical." },
};

// Inline vocal events Gemini TTS renders; a counseling client never needs more than these.
export const VOCAL_EVENTS = ["sigh", "breath", "short pause", "long pause", "chuckle"];
const EVENT_RE = /<(sigh|breath|short pause|long pause|chuckle)>/g;

// Who is speaking, for the TTS director note (kept in English, which TTS style prompts follow best).
export const CASE_VOICE = {
  "workplace-anxiety-01": "a 32-year-old Korean woman, polite and restrained",
  "adolescent-pressure-01": "a 16-year-old Korean high-school girl, quiet and cautious with adults",
  "career-transition-01": "a 39-year-old Korean man, measured and earnest",
  "older-bereavement-01": "a 68-year-old Korean man, slow and soft-spoken",
  "international-belonging-01": "a 24-year-old Chinese graduate student speaking careful Korean as a second language",
};

const AFFECT_STYLE = {
  guarded: { tone: "guarded and hesitant, holding back", pace: "with short, careful phrases", valence: -0.3, arousal: 0.45 },
  anxious: { tone: "anxious and tense, a little breathless", pace: "slightly fast with uneven pauses", valence: -0.5, arousal: 0.7 },
  relieved: { tone: "relieved and warmer, the tension easing", pace: "at an easy, unhurried pace", valence: 0.4, arousal: 0.35 },
  thoughtful: { tone: "reflective and searching for words", pace: "slowly, with thinking pauses", valence: 0.0, arousal: 0.3 },
};

// Sadness colours the bereavement case whatever the momentary affect.
const CASE_UNDERTONE = {
  "older-bereavement-01": "with an undertone of grief",
};

const clamp01 = (v) => Math.max(0, Math.min(1, Number.isFinite(Number(v)) ? Number(v) : 0));

/** Researcher/instructor controls, bounded. Request values override env defaults. */
export function expressionControls(raw, env = {}) {
  let defaults = env.EXPRESSION_DEFAULTS;
  if (typeof defaults === "string") {
    try {
      defaults = JSON.parse(defaults);
    } catch {
      defaults = null;
    }
  }
  const base = defaults && typeof defaults === "object" ? defaults : {};
  const r = raw && typeof raw === "object" ? raw : {};
  const pick = (key) => (r[key] !== undefined ? r[key] : base[key]);
  const expressivity = Object.hasOwn(EXPRESSIVITY, pick("expressivity")) ? pick("expressivity") : "natural";
  const lock = AFFECTS.includes(pick("lockAffect")) ? pick("lockAffect") : "";
  const maxIntensity = pick("maxIntensity") === undefined ? 1 : clamp01(pick("maxIntensity"));
  const vocalEvents = pick("vocalEvents") === undefined ? true : pick("vocalEvents") === true;
  return { expressivity, lockAffect: lock, maxIntensity, vocalEvents };
}

/** Short, plain director note from the model: letters, spaces and light punctuation only. */
export function cleanDelivery(note) {
  return String(note || "")
    .replace(/[^A-Za-z ,.'-]/g, " ")
    .replace(/\s+/g, " ")
    .trim()
    .slice(0, 100);
}

const normalize = (s) => String(s || "").replace(EVENT_RE, " ").replace(/\s+/g, " ").trim();

/**
 * The text the voice speaks: the subtitle text with at most `maxEvents` allowed vocal tags.
 * If the tagged version does not match the reply word for word, the plain reply is spoken.
 */
export function spokenText(reply, spoken, maxEvents) {
  const plain = String(reply || "").trim();
  if (!spoken || maxEvents <= 0) return plain;
  let text = String(spoken).replace(/<(?!\/?(sigh|breath|short pause|long pause|chuckle)>)[^>]*>/g, " ");
  if (normalize(text) !== normalize(plain)) return plain;
  let count = 0;
  text = text.replace(EVENT_RE, (m) => (++count <= maxEvents ? m : " "));
  return text.replace(/\s+/g, " ").trim();
}

function intensityWord(i) {
  return i < 0.3 ? "faintly" : i < 0.55 ? "slightly" : i < 0.8 ? "noticeably" : "strongly";
}

/**
 * Build the AffectPlan from the persona's appraisal, the relationship state and the controls.
 * appraisal: { emotion, intensity, delivery, spoken, reply }
 * state: { safety, guardedness, disclosure } (0..1)
 */
export function affectPlan(caseId, appraisal, state, controls, options = {}) {
  const c = controls || expressionControls();
  const a = appraisal || {};
  const s = state || {};
  const affect = c.lockAffect || (AFFECTS.includes(a.emotion) ? a.emotion : "anxious");
  // Appraised intensity, nudged by the relationship: guardedness tightens negative affect,
  // safety lets relief show. Then the expressivity gain and the researcher's cap.
  // options.prepared: the intensity already came out of this policy (e.g. /voice replaying
  // the /turn plan), so it is only re-capped, not nudged and amplified a second time.
  let raw = a.intensity === undefined ? 0.5 : clamp01(a.intensity);
  if (!options.prepared && (affect === "guarded" || affect === "anxious")) raw = clamp01(raw + 0.15 * clamp01(s.guardedness) - 0.1 * clamp01(s.safety));
  if (!options.prepared && affect === "relieved") raw = clamp01(raw + 0.1 * clamp01(s.safety));
  const gain = options.prepared ? 1 : EXPRESSIVITY[c.expressivity].gain;
  const intensity = Math.round(Math.min(c.maxIntensity, clamp01(raw * gain)) * 100) / 100;

  const look = AFFECT_STYLE[affect];
  const maxEvents = c.vocalEvents ? EXPRESSIVITY[c.expressivity].maxEvents : 0;
  const spoken = spokenText(a.reply, a.spoken, maxEvents);
  const note = cleanDelivery(a.delivery);
  const who = CASE_VOICE[caseId] || "a Korean adult";
  const undertone = CASE_UNDERTONE[caseId] ? ", " + CASE_UNDERTONE[caseId] : "";
  const style =
    `Speak as ${who} in a counseling session: ${intensityWord(intensity)} ${look.tone}${undertone}, ${look.pace}. ` +
    (note ? `Delivery: ${note}. ` : "") +
    "Natural conversational Korean, never acted or exaggerated.";
  return {
    policy: EXPRESSION_POLICY,
    affect,
    intensity,
    valence: Math.round(look.valence * intensity * 100) / 100,
    arousal: Math.round(look.arousal * (0.5 + intensity / 2) * 100) / 100,
    expressivity: c.expressivity,
    locked: !!c.lockAffect,
    style,
    delivery: note,
    spoken,
    events: (spoken.match(EVENT_RE) || []).map((t) => t.slice(1, -1)),
  };
}

/** Expression guidance for the real-time (Gemini Live) persona instruction. */
export function liveExpressionBlock(caseId, controls) {
  const c = controls || expressionControls();
  const lock = c.lockAffect ? ` Keep your underlying feeling ${c.lockAffect} for the whole session; only its intensity may change.` : "";
  return `EXPRESSION
Your voice is ${CASE_VOICE[caseId] || "a Korean adult"}${CASE_UNDERTONE[caseId] ? ", " + CASE_UNDERTONE[caseId] : ""}. ${EXPRESSIVITY[c.expressivity].label}${lock}
Let feeling show through pace, pauses, breath and softness rather than by naming it.${c.vocalEvents ? "" : " Do not sigh or laugh audibly."}
At the start of every spoken turn, call set_client_affect once with your current feeling (one of ${AFFECTS.join(", ")}) and its intensity from 0 to 1${c.maxIntensity < 1 ? ` (never above ${c.maxIntensity})` : ""}. It is silent bookkeeping for the simulation: never mention it and never wait for its result.`;
}

/** Function declaration the Live session uses to report affect to the face layer. */
export const LIVE_AFFECT_TOOL = {
  functionDeclarations: [
    {
      name: "set_client_affect",
      description: "Silently report the client's current feeling so the avatar's face and posture can match the voice.",
      parameters: {
        type: "object",
        properties: {
          affect: { type: "string", enum: AFFECTS },
          intensity: { type: "number", description: "0 (barely) to 1 (strongly)" },
        },
        required: ["affect", "intensity"],
      },
      behavior: "NON_BLOCKING",
    },
  ],
};

/** Wrap headerless 16-bit mono PCM in a WAV container. */
export function pcmToWav(pcm, sampleRate = 24000) {
  const header = new ArrayBuffer(44);
  const v = new DataView(header);
  const str = (o, s) => [...s].forEach((ch, i) => v.setUint8(o + i, ch.charCodeAt(0)));
  str(0, "RIFF");
  v.setUint32(4, 36 + pcm.length, true);
  str(8, "WAVE");
  str(12, "fmt ");
  v.setUint32(16, 16, true);
  v.setUint16(20, 1, true);
  v.setUint16(22, 1, true);
  v.setUint32(24, sampleRate, true);
  v.setUint32(28, sampleRate * 2, true);
  v.setUint16(32, 2, true);
  v.setUint16(34, 16, true);
  str(36, "data");
  v.setUint32(40, pcm.length, true);
  const out = new Uint8Array(44 + pcm.length);
  out.set(new Uint8Array(header), 0);
  out.set(pcm, 44);
  return out;
}
