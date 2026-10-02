using System;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    public enum Expressivity { Restrained, Natural, Vivid }

    /// <summary>Controls sent to the server's expression policy (Server/.../src/affect.js).</summary>
    [Serializable]
    public sealed class ExpressionControls
    {
        public string expressivity = "natural";
        public bool vocalEvents = true;
        public string lockAffect = "";
        public float maxIntensity = 1f;
    }

    /// <summary>
    /// The plan the server's expression policy returns with each client reply. The voice, the
    /// face and the research record all follow it (see Docs/EXPRESSION_PIPELINE.md).
    /// </summary>
    [Serializable]
    public sealed class AffectPlan
    {
        public string policy = "";
        public string affect = "anxious";
        public float intensity = 0.5f;
        public float valence;
        public float arousal;
        public string expressivity = "natural";
        public bool locked;
        public string style = "";
        public string spoken = "";
        public string delivery = "";
        public string[] events = Array.Empty<string>();
    }

    /// <summary>
    /// How strongly the AI client shows emotion. Instructors pick it on the briefing card;
    /// researchers can fix it, lock an affect, cap intensity or turn off vocal events with URL
    /// parameters (?expressivity=restrained&amp;lockAffect=guarded&amp;maxIntensity=0.6&amp;vocalEvents=0),
    /// which also hide the briefing control so participants cannot change it.
    /// </summary>
    public static class ExpressionSettings
    {
        private const string PrefKey = "counselcue.expressivity";
        private static bool loaded;
        private static Expressivity level = Expressivity.Natural;
        private static bool vocalEvents = true;
        private static string lockAffect = string.Empty;
        private static float maxIntensity = 1f;

        public static event Action Changed;

        /// <summary>True when a URL parameter fixed the settings for a study.</summary>
        public static bool LockedByStudy { get; private set; }

        public static Expressivity Level
        {
            get { Load(); return level; }
            set
            {
                Load();
                if (LockedByStudy || level == value) return;
                level = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                Changed?.Invoke();
            }
        }

        public static ExpressionControls ToControls()
        {
            Load();
            return new ExpressionControls
            {
                expressivity = Key(level),
                vocalEvents = vocalEvents,
                lockAffect = lockAffect,
                maxIntensity = maxIntensity,
            };
        }

        public static string Key(Expressivity value) => value switch
        {
            Expressivity.Restrained => "restrained",
            Expressivity.Vivid => "vivid",
            _ => "natural",
        };

        public static string LabelKo(Expressivity value) => value switch
        {
            Expressivity.Restrained => "절제",
            Expressivity.Vivid => "풍부",
            _ => "자연스럽게",
        };

        public static string LabelEn(Expressivity value) => value switch
        {
            Expressivity.Restrained => "Restrained",
            Expressivity.Vivid => "Vivid",
            _ => "Natural",
        };

        public static string NoteKo(Expressivity value) => value switch
        {
            Expressivity.Restrained => "감정을 눌러 담아 말합니다. 속도와 쉼으로만 드러납니다.",
            Expressivity.Vivid => "감정이 목소리와 얼굴에 분명히 드러납니다. 한숨·숨 고르기가 들릴 수 있습니다.",
            _ => "실제 내담자처럼 감정이 자연스럽게 묻어납니다.",
        };

        public static string NoteEn(Expressivity value) => value switch
        {
            Expressivity.Restrained => "Feelings are held back; they show only in pace and pauses.",
            Expressivity.Vivid => "Feelings show clearly in voice and face, with the odd sigh or breath.",
            _ => "Feelings come through naturally, as with a real client.",
        };

        public static Expressivity Parse(string key) => key switch
        {
            "restrained" => Expressivity.Restrained,
            "vivid" => Expressivity.Vivid,
            _ => Expressivity.Natural,
        };

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            level = (Expressivity)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, (int)Expressivity.Natural), 0, 2);
            ApplyQuery(Application.absoluteURL);
        }

        /// <summary>Applies study parameters from a page URL. Public for tests.</summary>
        public static void ApplyQuery(string url)
        {
            int start = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (start < 0) return;
            string query = url.Substring(start + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);
            foreach (string pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string name = pair.Substring(0, eq);
                string value = Uri.UnescapeDataString(pair.Substring(eq + 1)).Trim().ToLowerInvariant();
                switch (name)
                {
                    case "expressivity":
                        level = Parse(value);
                        LockedByStudy = true;
                        break;
                    case "lockAffect":
                        if (value == "guarded" || value == "anxious" || value == "relieved" || value == "thoughtful") { lockAffect = value; LockedByStudy = true; }
                        break;
                    case "maxIntensity":
                        if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float cap))
                        { maxIntensity = Mathf.Clamp01(cap); LockedByStudy = true; }
                        break;
                    case "vocalEvents":
                        vocalEvents = value != "0" && value != "false" && value != "off";
                        LockedByStudy = true;
                        break;
                }
            }
        }
    }
}
