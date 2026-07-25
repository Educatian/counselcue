using UnityEngine;

namespace AdieLab.AffectCounsel
{
    [CreateAssetMenu(fileName = "ClientProfile", menuName = "CounselCue/Client Profile")]
    public sealed class ClientProfileDefinition : ScriptableObject
    {
        [SerializeField] private string profileId = "adult-workplace";
        [SerializeField] private string displayName = "김지혜";
        [SerializeField] private string ageLabel = "32세";
        [SerializeField] private string counselingDomain = "직업·성인상담";
        [SerializeField, TextArea] private string culturalContext;
        [SerializeField, TextArea] private string nonverbalStyle;
        [SerializeField, Range(0.1f, 0.9f)] private float baselineGazeComfort = 0.58f;
        [SerializeField, Range(0.1f, 0.9f)] private float disclosurePace = 0.48f;

        public string ProfileId => profileId;
        public string DisplayName => displayName;
        public string AgeLabel => ageLabel;
        public string CounselingDomain => counselingDomain;
        public string CulturalContext => culturalContext;
        public string NonverbalStyle => nonverbalStyle;
        public float BaselineGazeComfort => baselineGazeComfort;
        public float DisclosurePace => disclosurePace;

        public void Configure(
            string id,
            string name,
            string age,
            string domain,
            string context,
            string style,
            float gazeComfort,
            float pace)
        {
            profileId = id;
            displayName = name;
            ageLabel = age;
            counselingDomain = domain;
            culturalContext = context;
            nonverbalStyle = style;
            baselineGazeComfort = Mathf.Clamp(gazeComfort, 0.1f, 0.9f);
            disclosurePace = Mathf.Clamp(pace, 0.1f, 0.9f);
        }
    }
}
