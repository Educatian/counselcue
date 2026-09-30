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

        // Character Creator / ActorCore blendshape names (60-shape facial profile and CC4
        // extended visemes) mapped onto CounselCue's FACS-style AU and viseme vocabulary.
        private static readonly Dictionary<string, string> CharacterCreatorShapes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Brow_Raise_Inner_L", "AU_01_L_BrowInnerUp" }, { "Brow_Raise_Inner_R", "AU_01_R_BrowInnerUp" },
            { "Brow_Raise_Outer_L", "AU_02_L_BrowOuterUp" }, { "Brow_Raise_Outer_R", "AU_02_R_BrowOuterUp" },
            { "Brow_Drop_L", "AU_04_L_BrowLowerer" }, { "Brow_Drop_R", "AU_04_R_BrowLowerer" },
            { "Brow_Compress_L", "AU_04_L_BrowCompress" }, { "Brow_Compress_R", "AU_04_R_BrowCompress" },
            { "Eye_Wide_L", "AU_05_L_UpperLidRaiser" }, { "Eye_Wide_R", "AU_05_R_UpperLidRaiser" },
            { "Cheek_Raise_L", "AU_06_L_CheekRaiser" }, { "Cheek_Raise_R", "AU_06_R_CheekRaiser" },
            { "Eye_Squint_L", "AU_07_L_LidTightener" }, { "Eye_Squint_R", "AU_07_R_LidTightener" },
            { "Mouth_Smile_L", "AU_12_L_LipCornerPuller" }, { "Mouth_Smile_R", "AU_12_R_LipCornerPuller" },
            { "Mouth_Dimple_L", "AU_14_L_Dimpler" }, { "Mouth_Dimple_R", "AU_14_R_Dimpler" },
            { "Mouth_Frown_L", "AU_15_L_LipCornerDepressor" }, { "Mouth_Frown_R", "AU_15_R_LipCornerDepressor" },
            { "Mouth_Shrug_Lower", "AU_17_ChinRaiser" }, { "Mouth_Chin_Up", "AU_17_ChinRaiser" },
            { "Mouth_Press_L", "AU_24_L_LipPressor" }, { "Mouth_Press_R", "AU_24_R_LipPressor" },
            { "Mouth_Tighten_L", "AU_23_L_LipTightener" }, { "Mouth_Tighten_R", "AU_23_R_LipTightener" },
            { "Mouth_Drop_Lower", "AU_25_LipsPart" }, { "Jaw_Open", "AU_26_JawDrop" },
            { "Eye_Blink_L", "AU_43_L_EyeClosed" }, { "Eye_Blink_R", "AU_43_R_EyeClosed" },
            { "Eye_Blink", "AU_45_Blink" },
            { "V_Open", "AA_VI_10_aa" }, { "V_Wide", "AA_VI_11_E" }, { "V_Lip_Open", "AA_VI_12_I" },
            { "V_Tight_O", "AA_VI_13_O" }, { "V_Tight", "AA_VI_14_U" }, { "V_Explosive", "AA_VI_01_PP" },
            { "V_Dental_Lip", "AA_VI_02_FF" }, { "V_Affricate", "AA_VI_07_SS" }, { "V_Tongue_up", "AA_VI_04_DD" },
            { "V_Tongue_Raise", "AA_VI_05_KK" }, { "V_None", "AA_VI_00_Sil" },
            // CC3 "+" profile spellings (e.g. Mandy-based actors) that carry ARKit shapes as well;
            // the A01..A51 ARKit duplicates stay unmapped so no channel is driven twice.
            { "Brow_Raise_Inner_Left", "AU_01_L_BrowInnerUp" }, { "Brow_Raise_Inner_Right", "AU_01_R_BrowInnerUp" },
            { "Brow_Raise_Outer_Left", "AU_02_L_BrowOuterUp" }, { "Brow_Raise_Outer_Right", "AU_02_R_BrowOuterUp" },
            { "Brow_Drop_Left", "AU_04_L_BrowLowerer" }, { "Brow_Drop_Right", "AU_04_R_BrowLowerer" },
            { "Eyes_Blink", "AU_45_Blink" }, { "Mouth_Open", "AU_26_JawDrop" }, { "Mouth_Lips_Part", "AU_25_LipsPart" },
            { "Mouth_Down_Lower_L", "AU_25_L_LipsPart" }, { "Mouth_Down_Lower_R", "AU_25_R_LipsPart" }
        };

        /// <summary>
        /// The semantic key for a vendor blendshape: Rocketbox AU/viseme names pass through, and
        /// Character Creator / ActorCore names are translated. Unknown shapes return empty.
        /// </summary>
        public static string ToSemantic(string blendShapeName)
        {
            string name = Normalize(blendShapeName);
            if (name.StartsWith("AU_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("AA_VI_", StringComparison.OrdinalIgnoreCase)) return name;
            return CharacterCreatorShapes.TryGetValue(name, out string semantic) ? semantic : string.Empty;
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
