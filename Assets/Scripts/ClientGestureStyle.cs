using UnityEngine;

namespace AdieLab.AffectCounsel
{
    /// <summary>Seated rest postures a client returns to between gestures.</summary>
    public enum RestPose
    {
        HandsOnThighs,
        HandsClasped,
        HandsOnKnees,
        ForearmsCrossedLow,
        ArmsFolded,
        HandOnArmrest,
        CheekOnHand,     // 턱 괴기: elbow on the armrest, cheek resting on the hand
        ChinOnFist,      // thinker: other arm across the stomach supports the elbow
        LeanInClasped    // leaning in, elbows toward the knees, hands clasped
    }

    /// <summary>
    /// Per-client gesture tendencies. Values come from the nonverbal literature summarised in
    /// Docs/GESTURES.md: anxious clients show more body-focused self-adaptors and fewer
    /// speech illustrators (Waxer 1977; Ekman &amp; Friesen 1969), depressed/grieving clients gesture
    /// less, slower and with smaller amplitude (psychomotor retardation), guarded clients close
    /// their posture and lean back, and safety brings openness and forward lean (Mehrabian).
    /// </summary>
    public sealed class ClientGestureStyle
    {
        public string Id = "default";
        /// <summary>Speech illustrators (beats, open palms) per minute of speaking at neutral affect.</summary>
        public float IllustratorsPerMinute = 7f;
        /// <summary>Body-focused adaptors (rubbing, neck/face touch) per minute at neutral affect.</summary>
        public float AdaptorsPerMinute = 1.6f;
        /// <summary>Gesture amplitude multiplier (1 = typical adult, &lt;1 restrained).</summary>
        public float Amplitude = 0.85f;
        /// <summary>Movement-duration multiplier (&gt;1 slower).</summary>
        public float Tempo = 1f;
        /// <summary>Baseline trunk flexion in degrees (positive = slumped/leaning forward).</summary>
        public float Slump;
        /// <summary>Head pitch bias in degrees (positive = head lowered).</summary>
        public float HeadDown;
        public bool CoversMouthWhenSmiling;
        public bool TucksHair;
        public float NodTendency = 0.6f;

        public RestWeight[] OpenRests = { new RestWeight(RestPose.HandsOnThighs, 1f) };
        public RestWeight[] ClosedRests = { new RestWeight(RestPose.HandsClasped, 1f) };
        public RestWeight[] ThoughtfulRests = { new RestWeight(RestPose.ChinOnFist, 1f) };
        public AdaptorKind[] Adaptors = { AdaptorKind.HandRub, AdaptorKind.NeckTouch, AdaptorKind.ThighRub };

        public struct RestWeight
        {
            public RestPose Pose;
            public float Weight;
            public RestWeight(RestPose pose, float weight) { Pose = pose; Weight = weight; }
        }

        public RestPose Pick(RestWeight[] options)
        {
            float total = 0f;
            foreach (RestWeight option in options) total += option.Weight;
            float roll = Random.value * total;
            foreach (RestWeight option in options)
            {
                roll -= option.Weight;
                if (roll <= 0f) return option.Pose;
            }
            return options[options.Length - 1].Pose;
        }

        public static ClientGestureStyle For(string profileId)
        {
            switch (profileId)
            {
                case "workplace-anxiety-01":
                    // 32-year-old office worker: polite, restrained; hands folded, rubbing and
                    // neck touch under stress; covers her mouth when she laughs.
                    return new ClientGestureStyle
                    {
                        Id = profileId, IllustratorsPerMinute = 6f, AdaptorsPerMinute = 2.2f, Amplitude = 0.75f, Tempo = 0.95f,
                        CoversMouthWhenSmiling = true, NodTendency = 0.7f,
                        OpenRests = new[] { new RestWeight(RestPose.HandsOnThighs, 0.5f), new RestWeight(RestPose.HandsClasped, 0.3f), new RestWeight(RestPose.HandOnArmrest, 0.2f) },
                        ClosedRests = new[] { new RestWeight(RestPose.HandsClasped, 0.7f), new RestWeight(RestPose.ForearmsCrossedLow, 0.3f) },
                        ThoughtfulRests = new[] { new RestWeight(RestPose.ChinOnFist, 0.5f), new RestWeight(RestPose.HandsClasped, 0.5f) },
                        Adaptors = new[] { AdaptorKind.HandRub, AdaptorKind.Wring, AdaptorKind.NeckTouch, AdaptorKind.ThighRub }
                    };
                case "adolescent-pressure-01":
                    // 16-year-old: withdrawn, arms folded, cheek propped on a hand, hair touching;
                    // few illustrators until safety builds.
                    return new ClientGestureStyle
                    {
                        Id = profileId, IllustratorsPerMinute = 4f, AdaptorsPerMinute = 1.8f, Amplitude = 0.7f, Tempo = 1f,
                        Slump = 5f, HeadDown = 4f, TucksHair = true, CoversMouthWhenSmiling = true, NodTendency = 0.35f,
                        OpenRests = new[] { new RestWeight(RestPose.HandsOnThighs, 0.4f), new RestWeight(RestPose.HandsClasped, 0.3f), new RestWeight(RestPose.CheekOnHand, 0.3f) },
                        ClosedRests = new[] { new RestWeight(RestPose.ArmsFolded, 0.5f), new RestWeight(RestPose.ForearmsCrossedLow, 0.3f), new RestWeight(RestPose.CheekOnHand, 0.2f) },
                        ThoughtfulRests = new[] { new RestWeight(RestPose.CheekOnHand, 0.7f), new RestWeight(RestPose.ChinOnFist, 0.3f) },
                        Adaptors = new[] { AdaptorKind.HairTuck, AdaptorKind.ThighRub, AdaptorKind.HandRub }
                    };
                case "career-transition-01":
                    // 39-year-old: tired, ambivalent; rubs his forehead, leans in with clasped hands
                    // when he opens up, two-handed "on one hand / on the other" gestures.
                    return new ClientGestureStyle
                    {
                        Id = profileId, IllustratorsPerMinute = 9f, AdaptorsPerMinute = 1.5f, Amplitude = 1f, Tempo = 1.05f,
                        Slump = 2f, NodTendency = 0.5f,
                        OpenRests = new[] { new RestWeight(RestPose.LeanInClasped, 0.35f), new RestWeight(RestPose.HandsOnThighs, 0.35f), new RestWeight(RestPose.HandOnArmrest, 0.3f) },
                        ClosedRests = new[] { new RestWeight(RestPose.ForearmsCrossedLow, 0.5f), new RestWeight(RestPose.HandsClasped, 0.5f) },
                        ThoughtfulRests = new[] { new RestWeight(RestPose.ChinOnFist, 0.5f), new RestWeight(RestPose.CheekOnHand, 0.5f) },
                        Adaptors = new[] { AdaptorKind.ForeheadRub, AdaptorKind.HandRub, AdaptorKind.NeckTouch }
                    };
                case "older-bereavement-01":
                    // 68-year-old widower: slow, sparse, small movements; hands on knees, a hand to
                    // the chest when grief surfaces; irregular small shifts rather than rhythmic rubbing.
                    return new ClientGestureStyle
                    {
                        Id = profileId, IllustratorsPerMinute = 3f, AdaptorsPerMinute = 1.2f, Amplitude = 0.6f, Tempo = 1.45f,
                        Slump = 7f, HeadDown = 5f, NodTendency = 0.5f,
                        OpenRests = new[] { new RestWeight(RestPose.HandsOnKnees, 0.5f), new RestWeight(RestPose.HandsOnThighs, 0.3f), new RestWeight(RestPose.HandOnArmrest, 0.2f) },
                        ClosedRests = new[] { new RestWeight(RestPose.HandsClasped, 0.6f), new RestWeight(RestPose.HandsOnKnees, 0.4f) },
                        ThoughtfulRests = new[] { new RestWeight(RestPose.HandsClasped, 0.5f), new RestWeight(RestPose.ChinOnFist, 0.5f) },
                        Adaptors = new[] { AdaptorKind.HandToChest, AdaptorKind.FaceTouch, AdaptorKind.SmallShift }
                    };
                case "international-belonging-01":
                    // 24-year-old graduate student: courteous, frequent small nods, contained
                    // gestures, touches his neck when embarrassed, points to himself.
                    return new ClientGestureStyle
                    {
                        Id = profileId, IllustratorsPerMinute = 6f, AdaptorsPerMinute = 1.6f, Amplitude = 0.75f, Tempo = 1f,
                        NodTendency = 0.9f,
                        OpenRests = new[] { new RestWeight(RestPose.HandsOnThighs, 0.45f), new RestWeight(RestPose.HandsClasped, 0.35f), new RestWeight(RestPose.HandOnArmrest, 0.2f) },
                        ClosedRests = new[] { new RestWeight(RestPose.HandsClasped, 0.6f), new RestWeight(RestPose.ForearmsCrossedLow, 0.4f) },
                        ThoughtfulRests = new[] { new RestWeight(RestPose.ChinOnFist, 0.6f), new RestWeight(RestPose.HandsClasped, 0.4f) },
                        Adaptors = new[] { AdaptorKind.NeckTouch, AdaptorKind.HandRub, AdaptorKind.ThighRub }
                    };
                default:
                    return new ClientGestureStyle { Id = profileId ?? "default" };
            }
        }
    }

    public enum AdaptorKind
    {
        HandRub,
        Wring,
        ThighRub,
        NeckTouch,
        FaceTouch,
        ForeheadRub,
        HandToChest,
        HairTuck,
        SmallShift
    }
}
