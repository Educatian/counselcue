using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Appends research records to local JSONL files, only after the learner has opted in
    /// on the briefing card. Logging must never interrupt the practice loop: a full disk, a
    /// blocked browser store, or a locked file is reported once and the session continues.
    /// </summary>
    public static class LocalJsonlLog
    {
        public const string ConsentKey = "counselcue.research-logging-consent";
        public const string LearnerIdKey = "counselcue.learner-id";
        public const string LearnerCodeKey = "counselcue.learner-code";
        public const string ExportFormat = "counselcue-export";

        /// <summary>Every file this project writes, so deletion can remove all of them.</summary>
        public static readonly string[] FileNames =
        {
            "counseling-sessions.jsonl",
            "counseling-session-summaries.jsonl",
            "counseling-self-assessments.jsonl"
        };

        private static bool warned;

        public static bool ConsentGranted
        {
            get => PlayerPrefs.GetInt(ConsentKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(ConsentKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool Append(string fileName, object record)
        {
            if (!ConsentGranted) return false;
            try
            {
                string path = Path.Combine(Application.persistentDataPath, fileName);
                File.AppendAllText(path, JsonUtility.ToJson(record) + Environment.NewLine);
                return true;
            }
            catch (Exception exception)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning($"CounselCue local logging is unavailable; the session continues without it. {exception.GetType().Name}: {exception.Message}");
                }
                return false;
            }
        }

        /// <summary>Number of local record files currently present.</summary>
        public static int CountFiles()
        {
            int count = 0;
            foreach (string fileName in FileNames)
            {
                try { if (File.Exists(Path.Combine(Application.persistentDataPath, fileName))) count++; }
                catch (Exception) { }
            }
            return count;
        }

        /// <summary>Random, device-local pseudonym (e.g. L-7F3A9C); never linked to an identity.</summary>
        public static string LearnerId
        {
            get
            {
                string id = PlayerPrefs.GetString(LearnerIdKey, string.Empty);
                if (!string.IsNullOrEmpty(id)) return id;
                id = "L-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
                PlayerPrefs.SetString(LearnerIdKey, id);
                PlayerPrefs.Save();
                return id;
            }
        }

        /// <summary>Optional code the instructor assigns (e.g. a class roster number).</summary>
        public static string LearnerCode
        {
            get => PlayerPrefs.GetString(LearnerCodeKey, string.Empty);
            set
            {
                PlayerPrefs.SetString(LearnerCodeKey, Sanitize(value, 40));
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// One JSON file with every local record, for the instructor dashboard
        /// (Dashboard/, deployed at /dashboard/). Returns null when there is nothing to export.
        /// Records are copied line by line as written; nothing is re-serialized.
        /// </summary>
        public static string BuildExportBundle()
        {
            StringBuilder sessions = new StringBuilder(), summaries = new StringBuilder(), assessments = new StringBuilder();
            int count = 0;
            count += AppendLines(FileNames[0], sessions);
            count += AppendLines(FileNames[1], summaries);
            count += AppendLines(FileNames[2], assessments);
            if (count == 0) return null;
            return ResearchExportBundle.Compose(sessions.ToString(), summaries.ToString(), assessments.ToString(),
                DateTime.UtcNow.ToString("O"), Application.version, LearnerId, LearnerCode);
        }

        public static string ExportFileName()
        {
            string who = string.IsNullOrWhiteSpace(LearnerCode) ? LearnerId : LearnerCode;
            StringBuilder safe = new StringBuilder();
            foreach (char c in who) safe.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return $"counselcue-{safe}-{DateTime.Now:yyyyMMdd-HHmm}.json";
        }

        private static int AppendLines(string fileName, StringBuilder target)
        {
            int count = 0;
            try
            {
                string path = Path.Combine(Application.persistentDataPath, fileName);
                if (!File.Exists(path)) return 0;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length < 2 || line[0] != '{' || line[line.Length - 1] != '}') continue;
                    if (target.Length > 0) target.Append(',');
                    target.Append(line);
                    count++;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"CounselCue could not read {fileName} for export: {exception.Message}");
            }
            return count;
        }

        private static string Sanitize(string value, int max)
        {
            string text = (value ?? string.Empty).Trim();
            return text.Length <= max ? text : text.Substring(0, max);
        }

        /// <summary>Deletes every local record file. Returns how many were removed, or -1 on failure.</summary>
        public static int DeleteAll()
        {
            int deleted = 0;
            bool failed = false;
            foreach (string fileName in FileNames)
            {
                try
                {
                    string path = Path.Combine(Application.persistentDataPath, fileName);
                    if (!File.Exists(path)) continue;
                    File.Delete(path);
                    deleted++;
                }
                catch (Exception exception)
                {
                    failed = true;
                    Debug.LogWarning($"CounselCue could not delete {fileName}: {exception.Message}");
                }
            }
            // On WebGL the files live in an IndexedDB-backed file system; saving PlayerPrefs
            // flushes it so the deletion survives a page reload.
            PlayerPrefs.Save();
            return failed ? -1 : deleted;
        }
    }
}
