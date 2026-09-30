using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    public static class SkillLexiconExport
    {
        private const string JsonPath = "Assets/Resources/CounselCue/skill-lexicon.json";

        /// <summary>
        /// Writes the active term lists to an editable JSON file. Once it exists, the evaluator uses it
        /// (list by list) instead of the built-in defaults. Run the Response Evaluator Checks after edits.
        /// </summary>
        [MenuItem("Tools/CounselCue/Export Skill Lexicon JSON")]
        public static void Export()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(JsonPath));
            File.WriteAllText(JsonPath, JsonUtility.ToJson(CounselingResponseEvaluator.Active, true));
            AssetDatabase.ImportAsset(JsonPath);
            CounselingResponseEvaluator.UseLexicon(null);
            Debug.Log($"COUNSELCUE_LEXICON_EXPORTED {JsonPath}");
            EditorUtility.RevealInFinder(JsonPath);
        }
    }
}
