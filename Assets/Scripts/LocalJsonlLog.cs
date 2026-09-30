using System;
using System.IO;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Appends research records to local JSONL files. Logging must never interrupt
    /// the practice loop: a full disk, a blocked browser store, or a locked file
    /// is reported once as a warning and the session continues.
    /// </summary>
    public static class LocalJsonlLog
    {
        private static bool warned;

        public static bool Append(string fileName, object record)
        {
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
    }
}
