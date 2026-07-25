using UnityEngine;

namespace AdieLab.AffectCounsel
{
    [CreateAssetMenu(fileName = "AvatarPresentation", menuName = "CounselCue/Avatar Presentation")]
    public sealed class AvatarPresentationDefinition : ScriptableObject
    {
        [SerializeField] private string presentationId = "rocketbox-female-adult-05";
        [SerializeField] private GameObject avatarPrefab;
        [SerializeField] private Vector3 localPosition = Vector3.zero;
        [SerializeField] private Vector3 localEulerAngles = new Vector3(0f, 180f, 0f);
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private string voiceStyle = "soft-contemporary-korean";
        [SerializeField, Range(0.2f, 1f)] private float expressionIntensity = 0.72f;
        [SerializeField, Range(0.2f, 1f)] private float gazeIntensity = 0.68f;

        public string PresentationId => presentationId;
        public GameObject AvatarPrefab => avatarPrefab;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        public Vector3 LocalScale => localScale;
        public string VoiceStyle => voiceStyle;
        public float ExpressionIntensity => expressionIntensity;
        public float GazeIntensity => gazeIntensity;

        public void Configure(
            string id,
            GameObject prefab,
            Vector3 position,
            Vector3 eulerAngles,
            Vector3 scale,
            string configuredVoiceStyle,
            float configuredExpressionIntensity,
            float configuredGazeIntensity)
        {
            presentationId = id;
            avatarPrefab = prefab;
            localPosition = position;
            localEulerAngles = eulerAngles;
            localScale = scale;
            voiceStyle = configuredVoiceStyle;
            expressionIntensity = Mathf.Clamp(configuredExpressionIntensity, 0.2f, 1f);
            gazeIntensity = Mathf.Clamp(configuredGazeIntensity, 0.2f, 1f);
        }
    }
}
