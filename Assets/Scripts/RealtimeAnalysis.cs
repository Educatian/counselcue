using System;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    public enum AnalysisMode { Off, Shadow, JevFirst }

    /// <summary>
    /// One turn's real-time analysis from Jev (worker /analyze → OpenRouter Decisions API).
    /// Fields mirror Server/CounselCue.EdgeWorker/src/jev.js analysisResult.
    /// </summary>
    [Serializable]
    public sealed class JevAnalysis
    {
        public string engine = "";
        public string version = "";
        public string model = "";
        public string code = "";
        public float confidence;
        public int quality;
        public float qualityScore;
        public float attendsToFeeling;
        public int latencyMs;
        public string clientAffect = "";
        public float clientIntensity;

        [NonSerialized] public bool succeeded;
        [NonSerialized] public string error = "";

        public static JevAnalysis Failure(string reason) => new JevAnalysis { succeeded = false, error = reason ?? "" };
    }

    /// <summary>
    /// How Jev's real-time analysis is used (see Docs/REALTIME_ANALYSIS.md).
    /// Shadow (default): Jev gives an instant provisional code; the LLM coder stays authoritative
    /// and both are recorded for agreement research. JevFirst: Jev's code drives the turn when
    /// its calibrated confidence is high enough. Off: no Jev calls. Researchers set it with the
    /// page URL (?analysis=off|shadow|jev&amp;jevMin=0.75).
    /// </summary>
    public static class AnalysisSettings
    {
        private static bool loaded;
        private static AnalysisMode mode = AnalysisMode.Shadow;
        private static float minConfidence = 0.75f;

        public static AnalysisMode Mode { get { Load(); return mode; } set { Load(); mode = value; } }
        public static float MinConfidence { get { Load(); return minConfidence; } }

        public static string Key(AnalysisMode value) => value switch
        {
            AnalysisMode.Off => "off",
            AnalysisMode.JevFirst => "jev",
            _ => "shadow",
        };

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            ApplyQuery(Application.absoluteURL);
        }

        public static void ApplyQuery(string url)
        {
            int start = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (start < 0) return;
            foreach (string pair in url.Substring(start + 1).Split('&', '#'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string name = pair.Substring(0, eq);
                string value = Uri.UnescapeDataString(pair.Substring(eq + 1)).Trim().ToLowerInvariant();
                if (name == "analysis")
                    mode = value == "off" ? AnalysisMode.Off : value == "jev" ? AnalysisMode.JevFirst : AnalysisMode.Shadow;
                else if (name == "jevMin" && float.TryParse(value, System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out float min))
                    minConfidence = Mathf.Clamp01(min);
            }
        }
    }
}
