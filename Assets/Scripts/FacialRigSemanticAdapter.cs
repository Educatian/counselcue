using System;
using System.Collections.Generic;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Maps vendor blendshape names to the semantic AU/viseme vocabulary used by CounselCue.
    /// This keeps counseling logic independent from the selected face rig.
    /// </summary>
    public static class FacialRigSemanticAdapter
    {
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            int separator = name.LastIndexOf('.');
            return separator >= 0 ? name.Substring(separator + 1) : name;
        }

        public static string BilateralFamily(string semanticName)
        {
            return Normalize(semanticName).Replace("_L_", "_").Replace("_R_", "_");
        }

        public static bool IsLeft(string semanticName) => Normalize(semanticName).Contains("_L_");
        public static bool IsRight(string semanticName) => Normalize(semanticName).Contains("_R_");

        public static HashSet<string> FindCombinedShapesShadowedByLaterals(IEnumerable<string> semanticNames)
        {
            HashSet<string> names = new HashSet<string>(semanticNames, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> sides = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names)
            {
                string family = BilateralFamily(name);
                if (!sides.ContainsKey(family)) sides.Add(family, 0);
                if (IsLeft(name)) sides[family] |= 1;
                if (IsRight(name)) sides[family] |= 2;
            }

            HashSet<string> shadowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, int> pair in sides)
            {
                if (pair.Value == 3 && names.Contains(pair.Key)) shadowed.Add(pair.Key);
            }
            return shadowed;
        }
    }
}
