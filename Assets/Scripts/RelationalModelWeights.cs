using System;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Per-code effects of a counselor turn on the client's relational state. Versioned and
    /// loadable from Resources/CounselCue/relational-weights.json so the model can be refit
    /// (eval/calibrate-relational.mjs) from expert ratings or coded transcripts without code
    /// changes. Every turn record stores the version that produced it.
    /// </summary>
    [Serializable]
    public sealed class RelationalModelWeights
    {
        public const string ResourcePath = "CounselCue/relational-weights";

        public string version = "avp-prior-1";
        public string source =
            "Prior informed by Chen et al. (2026), Adaptive Virtual Patient: exploration moves " +
            "drive openness about 3x more than any single empathy component. Not yet fitted to " +
            "Korean data; refit with eval/calibrate-relational.mjs.";

        /// <summary>How much of a safety change is mirrored (inversely) in guardedness.</summary>
        public float guardednessCoupling = 0.65f;

        /// <summary>How strongly the delivery (face) modifier carries into disclosure.</summary>
        public float deliveryDisclosureCoupling = 0.8f;

        /// <summary>
        /// 0 = off. When positive, gains in disclosure are scaled by safety
        /// (factor 1 - gate + 2·gate·safety), so openness follows felt safety.
        /// </summary>
        public float safetyGate = 0f;

        public CodeWeight[] codes = DefaultCodes();

        [Serializable]
        public sealed class CodeWeight
        {
            public string code;
            /// <summary>Safety change at the code's typical quality.</summary>
            public float safety;
            /// <summary>Extra safety per quality point above/below typical.</summary>
            public float qualitySlope;
            /// <summary>Willingness-to-disclose change.</summary>
            public float disclosure;
            /// <summary>Number of rated observations behind this row (0 = prior only).</summary>
            public int observations;
        }

        private static RelationalModelWeights active;

        public static RelationalModelWeights Active
        {
            get
            {
                if (active == null) active = LoadOrDefault();
                return active;
            }
        }

        /// <summary>Replace the active weights (tests, calibration previews). Null reloads.</summary>
        public static void Use(RelationalModelWeights weights) => active = weights;

        public static RelationalModelWeights LoadOrDefault()
        {
            RelationalModelWeights defaults = new RelationalModelWeights();
            try
            {
                TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
                if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return defaults;
                RelationalModelWeights loaded = JsonUtility.FromJson<RelationalModelWeights>(asset.text);
                if (loaded == null || loaded.codes == null || loaded.codes.Length == 0) return defaults;
                // Codes missing from a fitted file keep their prior values.
                foreach (CodeWeight prior in defaults.codes)
                {
                    if (loaded.Find(prior.code) == null) loaded.codes = Append(loaded.codes, prior);
                }
                return loaded;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"CounselCue relational weights override ignored: {exception.Message}");
                return defaults;
            }
        }

        public CodeWeight Find(string code)
        {
            if (codes == null) return null;
            for (int i = 0; i < codes.Length; i++)
            {
                if (codes[i] != null && codes[i].code == code) return codes[i];
            }
            return null;
        }

        public float SafetyDelta(string code, int quality)
        {
            CodeWeight weight = Find(code) ?? Find(CounselingCodebook.Neutral);
            if (weight == null) return 0f;
            return weight.safety + weight.qualitySlope * (quality - CounselingCodebook.TypicalQuality(code));
        }

        public float DisclosureDelta(string code, float safety)
        {
            CodeWeight weight = Find(code) ?? Find(CounselingCodebook.Neutral);
            if (weight == null) return 0f;
            float delta = weight.disclosure;
            if (delta > 0f && safetyGate > 0f) delta *= 1f - safetyGate + 2f * safetyGate * safety;
            return delta;
        }

        /// <summary>
        /// The prior. Empathy (reflection, validation) mainly builds safety; exploration (open
        /// questions) mainly opens disclosure, at 3x a single empathy component. Reflection plus
        /// exploration combines both. Advice and premature reassurance close the client down.
        /// </summary>
        public static CodeWeight[] DefaultCodes() => new[]
        {
            W(CounselingCodebook.ReflectionExploration, 0.12f, 0.04f, 0.12f),
            W(CounselingCodebook.Reflection, 0.10f, 0.05f, 0.03f),
            W(CounselingCodebook.Validation, 0.10f, 0.05f, 0.03f),
            W(CounselingCodebook.OpenQuestion, 0.05f, 0.02f, 0.09f),
            W(CounselingCodebook.ClosedQuestion, 0.00f, 0.00f, 0.00f),
            W(CounselingCodebook.WhyQuestion, -0.02f, 0.02f, -0.02f),
            W(CounselingCodebook.Advice, -0.12f, 0.06f, -0.09f),
            W(CounselingCodebook.PrematureReassurance, -0.08f, 0.04f, -0.06f),
            W(CounselingCodebook.Neutral, 0.01f, 0.02f, 0.00f),
            W(CounselingCodebook.Silence, -0.03f, 0.00f, -0.03f)
        };

        private static CodeWeight W(string code, float safety, float slope, float disclosure) =>
            new CodeWeight { code = code, safety = safety, qualitySlope = slope, disclosure = disclosure, observations = 0 };

        private static CodeWeight[] Append(CodeWeight[] source, CodeWeight item)
        {
            CodeWeight[] result = new CodeWeight[source.Length + 1];
            Array.Copy(source, result, source.Length);
            result[source.Length] = item;
            return result;
        }
    }
}
