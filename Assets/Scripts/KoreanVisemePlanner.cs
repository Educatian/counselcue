using System;
using System.Collections.Generic;

namespace AdieLab.AffectCounsel
{
    public static class KoreanVisemePlanner
    {
        private static readonly string[] Vowels =
        {
            "AA_VI_10_aa", "AA_VI_11_E", "AA_VI_12_I", "AA_VI_13_O", "AA_VI_14_U"
        };

        public static string[] Build(string text)
        {
            List<string> visemes = new List<string>();
            foreach (char character in text ?? string.Empty)
            {
                if (char.IsWhiteSpace(character) || char.IsPunctuation(character))
                {
                    visemes.Add("AA_VI_00_Sil");
                    continue;
                }

                if (character >= 0xAC00 && character <= 0xD7A3)
                {
                    int syllable = character - 0xAC00;
                    int initial = syllable / 588;
                    int medial = (syllable % 588) / 28;
                    visemes.Add(ConsonantViseme(initial));
                    visemes.Add(VowelViseme(medial));
                    continue;
                }

                visemes.Add("AA_VI_08_nn");
            }

            if (visemes.Count == 0) visemes.Add("AA_VI_00_Sil");
            return visemes.ToArray();
        }

        private static string ConsonantViseme(int initial)
        {
            if (initial == 1 || initial == 7 || initial == 17) return "AA_VI_05_KK";
            if (initial == 3 || initial == 4) return "AA_VI_08_nn";
            if (initial == 5) return "AA_VI_09_RR";
            if (initial == 6 || initial == 8 || initial == 16) return "AA_VI_01_PP";
            if (initial == 9 || initial == 10 || initial == 11) return "AA_VI_07_SS";
            if (initial == 12 || initial == 14 || initial == 15) return "AA_VI_06_CH";
            return "AA_VI_04_DD";
        }

        private static string VowelViseme(int medial)
        {
            if (medial == 2 || medial == 3 || medial == 6 || medial == 7 || medial == 12 || medial == 17) return Vowels[1];
            if (medial == 4 || medial == 5 || medial == 8 || medial == 9 || medial == 13) return Vowels[2];
            if (medial == 10 || medial == 11 || medial == 14 || medial == 15 || medial == 16) return Vowels[3];
            if (medial == 18 || medial == 19 || medial == 20) return Vowels[4];
            return Vowels[0];
        }
    }
}
