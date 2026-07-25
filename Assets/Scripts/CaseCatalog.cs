using System;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    [CreateAssetMenu(fileName = "CaseCatalog", menuName = "CounselCue/Case Catalog")]
    public sealed class CaseCatalog : ScriptableObject
    {
        [SerializeField] private CounselingCaseDefinition[] cases = Array.Empty<CounselingCaseDefinition>();
        [SerializeField, Min(0)] private int defaultCaseIndex;

        public CounselingCaseDefinition[] Cases => cases;
        public int Count => cases == null ? 0 : cases.Length;
        public CounselingCaseDefinition DefaultCase => GetCase(defaultCaseIndex);

        public CounselingCaseDefinition GetCase(int index)
        {
            if (cases == null || cases.Length == 0) return null;
            return cases[Mathf.Clamp(index, 0, cases.Length - 1)];
        }

        public void Configure(CounselingCaseDefinition[] configuredCases, int configuredDefaultIndex = 0)
        {
            cases = configuredCases ?? Array.Empty<CounselingCaseDefinition>();
            defaultCaseIndex = cases.Length == 0 ? 0 : Mathf.Clamp(configuredDefaultIndex, 0, cases.Length - 1);
        }
    }
}
