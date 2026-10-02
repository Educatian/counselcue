# Client gestures

`ClientGestureController` gives every client procedural, seated body language driven by
affect, the relational state (safety / guardedness / disclosure) and a per-client
`ClientGestureStyle`. It replaces the Rocketbox upper-body talk clips, which were recorded
standing or sitting at a table and left the hands floating over the lap in an armchair.

## How it works

- **Body-relative targets.** Each pose is a hand target on an anchor computed every frame from
  the avatar's own skeleton — thighs, knees, lap, ribs, upper chest, chin, cheek, mouth, ear,
  neck, forehead, the chair's armrests — so the same pose fits adults, the teenager and the
  older client, on Rocketbox or ActorCore rigs.
- **Arm solver.** An analytic two-bone IK on the humanoid arm bones (not Mecanim hand IK, which
  is clamped by muscle limits and cannot cross the midline). Each pose carries an elbow pole so
  elbows stay down and in front for face touches and out from the torso at rest. The wrist is
  limited to 28° off the forearm line, and wrist twist is shared with the forearm.
- **Timing.** Minimum-jerk transitions with a small lift so hands arc rather than drag;
  Kendon's phases (preparation → stroke → hold → retraction); Poisson-timed events so gestures
  never tick like a metronome; low-amplitude drift so a resting hand is never frozen.
- **Layers.** Trunk lean (forward with safety, back with guardedness), side lean, head nods,
  shakes and tilts, shoulder shrugs, finger curl through humanoid muscles.

## Repertoire

| Kind | Gestures |
| --- | --- |
| Rest postures | hands on thighs, hands stacked on the lap (손을 모은 자세), hands on knees, forearms crossed low, arms folded, hand on armrest (only when reachable), cheek resting on the hand (턱 괴기, other forearm under the elbow), chin on fist, leaning in with hands together |
| Self-adaptors (body-focused) | hand rubbing, wringing, thigh rubbing, neck touch, face touch, forehead rub, hand to chest, hair tuck, small irregular shifts |
| Illustrators (speech) | beats (quick down-stroke, slower recovery), open palms, shrug with palms up on uncertainty ("모르겠어요"), pointing to oneself on "저는/제가", covering the mouth when laughing |
| Regulators | listening nods after the counselor's turn, nods on "네/맞아요", head shake on "아니요" |

## Evidence behind the rules

- Ekman & Friesen's categories (emblems, illustrators, adaptors, regulators, affect displays)
  structure the repertoire.
- Anxiety: more self-touching adaptors and nervous hand movements, fewer speech-linked
  illustrators, stiffer upright trunk (Waxer 1977). → `AdaptorsPerMinute × 1.6`,
  `IllustratorsPerMinute × 0.7` when anxious.
- Depression and grief: fewer co-speech gestures, which fall further with symptom severity and
  psychomotor retardation; more irregular, plan-less hand movements and fewer repetitive ones
  in comorbid depression. → the 68-year-old client gestures ~3/min at 0.6 amplitude and 1.45×
  slower, with "small shift" adaptors instead of rhythmic rubbing.
- Immediacy: forward lean and open posture signal engagement; closed posture and backward lean
  accompany guardedness (Mehrabian). → lean = +4° × safety − 4.5° × guardedness.
- Culture: restrained gesture, hands kept together on the lap and covering the mouth when
  laughing are common in Korean formal conversation; frequent small nods as polite
  back-channelling.

## Per-client styles

| Client | Style |
| --- | --- |
| 김지혜, 32, workplace anxiety | restrained, stacked hands, rubbing and neck touch under stress, covers mouth when laughing |
| 박서윤, 16, academic pressure | withdrawn: arms folded, cheek on hand, hair touching, head lowered, few illustrators until safety builds |
| 최민준, 39, career transition | tired and ambivalent: forehead rub, leaning in when opening up, two-handed "on one hand / on the other" |
| 이정호, 68, bereavement | slow, sparse, small: hands on knees, hand to chest, irregular shifts |
| 왕하오, 24, international student | courteous: frequent small nods, contained gestures, neck touch when embarrassed, points to himself |

## Review

`ReviewCaptureRunner` renders a gesture sheet (every rest posture for three clients, every
adaptor and illustrator) and a motion strip when `Temp/counselcue-gesture-sheet.flag` exists.

## Sources

- Ekman, P., & Friesen, W. V. (1969). The repertoire of nonverbal behavior. *Semiotica*, 1(1), 49–98.
- Waxer, P. H. (1977). Nonverbal cues for anxiety: An examination of emotional leakage. *Journal of Abnormal Psychology*, 86(3), 306–314.
- Fidgeting behavior during psychotherapy: hand movement structure contains information about depressive symptoms. *Journal of Contemporary Psychotherapy* (2020). https://link.springer.com/article/10.1007/s10879-020-09465-5
- Automatic quantification of hand gestures in current and remitted major depressive disorder during oral expression (2025). https://pmc.ncbi.nlm.nih.gov/articles/PMC12291071/
- Kendon, A. / McNeill, D. on gesture phases and beats: https://mcneilllab.uchicago.edu/pdfs/gesture.a_psycholinguistic_approach.cambridge.encyclop.pdf
