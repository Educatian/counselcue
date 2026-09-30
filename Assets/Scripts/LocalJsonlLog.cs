using System;
using System.IO;
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
