# Client facial behaviour

`ClientFacialExpressionDriver` animates every client face through FACS action units, mapped onto the rig's blendshapes by `FacialRigSemanticAdapter`. The model has five layers.

## 1. Jaw
Unity's humanoid rig maps the Character Creator jaw (`CC_Base_JawRoot`) as a muscle. The seated clips have no jaw curve, so Mecanim (and `HumanPoseHandler.SetHumanPose` in the gesture layer) write the muscle's mid value. That left every client's jaw about 10° open.

The driver runs at execution order 900, after the Animator, the gaze IK and the gesture pose. It does three things:
- restores the jaw to its bind pose relative to the head, taken from the skin's bind matrices;
- leaves a 0.6° rest, so the teeth are just apart while the lips stay sealed;
- opens the jaw up to 9° with speech openness. Bilabials close fully.

`FaceDiagnostics` (written during review captures to `Screenshots/review/face-diag.txt`) reports the jaw angle and the active shapes.

## 2. Affect display
All displays are low-intensity FACS prototypes, about A–B intensity. Clients in clinical interviews show subtle, partial displays rather than full emotional prototypes.

| Affect | Main AUs | Basis |
|---|---|---|
| Anxious | AU1 + AU4 (worry brow), small AU5, AU20 lip stretch, AU24 lip press, AU17 | Harrigan & O'Connell, 1996 |
| Guarded | AU4 + AU7, AU23/24 lip tightening, AU17, unilateral AU14 | Ekman & Friesen, 1978; Girard et al., 2014 |
| Thoughtful | AU4 (concentration), AU14, AU17, gaze aversion handled by the gaze layer | Ekman, 1979 |
| Relieved | AU6 + AU12 (Duchenne), relaxed brows | Ekman, Davidson & Friesen, 1990 |
| Sadness tone (per case, e.g. bereavement) | AU1 + AU4 + AU15 + AU17, heavier lids, fewer smiles | Girard et al., 2014 |

Modulation:
- Relational safety relaxes the tension components; guardedness tightens them.
- Soft lip corners (AU12 at 3 plus up to 6 with safety) keep a polite, engaged face, so it doesn't read as cold.
- Intensity drifts in 4–9 s episodes (gain 0.7–1.15), and left/right asymmetry shifts slowly.
- Expressions rise faster than they fade (Krumhuber, Kappas & Manstead, 2013; Cohn & Schmidt, 2004).

## 3. Blinks
| Feature | Behaviour | Basis |
|---|---|---|
| Rate | About 15 per minute listening and 24 speaking; ×1.35 when anxious | Bentivoglio et al., 1997 (17 at rest, 26 in conversation) |
| Intervals | Gamma-distributed, k = 2 | |
| Form | 15% partial blinks, 8% double blinks; fast close, slower reopening | |
| Gaze-evoked | A blink is likely when the gaze moves to aversion or recall | Evinger et al., 1994 |
| Speech | Blinks cluster at pauses, plus a turn-final blink | |
| Listener feedback | A longer blink and a brow lift when the counselor's turn ends (`CounselorBodyController.Submitted`) | Hömke, Holler & Levinson, 2017 |

## 4. Conversational signals
While speaking, the client gives brief eyebrow flashes (AU1 + AU2) at turn and phrase onsets, with a 1.4 s refractory period (Ekman, 1979; Cavé et al., 1996). At about 14% of pauses there is a brief word-search frown (AU4).

## 5. Eyelids and gaze
- The upper lids follow vertical gaze (`Eye_*_Look_Down/Up`, plus partial lid closure looking down) (Becker & Fuchs, 1988).
- A relaxed resting lid covers the top of the iris, avoiding the 'stare' of scanned faces. Actors whose neutral eye opening is wide get extra closure (`FacialTone.RestingLidFor`).

## Rendering
`ClientRenderingController` warms the ActorCore skin material:
- a slight warm tint;
- occlusion strength 0.5;
- less gloss;
- a faint warm emission from the albedo, as a stand-in for subsurface scattering.

The room adds a warm, shadowless fill at the counselor's eye level (`CounselorEyeLevelFill`), which also puts catchlights in the clients' eyes.

## References
- Becker, W., & Fuchs, A. F. (1988). Lid-eye coordination during vertical gaze changes in man and monkey. *Journal of Neurophysiology, 60*(4).
- Bentivoglio, A. R., et al. (1997). Analysis of blink rate patterns in normal subjects. *Movement Disorders, 12*(6).
- Cavé, C., et al. (1996). About the relationship between eyebrow movements and F0 variations. *ICSLP*.
- Cohn, J. F., & Schmidt, K. L. (2004). The timing of facial motion in posed and spontaneous smiles. *International Journal of Wavelets, Multiresolution and Information Processing, 2*(2).
- Ekman, P. (1979). About brows: Emotional and conversational signals. In *Human Ethology*.
- Ekman, P., Davidson, R. J., & Friesen, W. V. (1990). The Duchenne smile. *Journal of Personality and Social Psychology, 58*(2).
- Ekman, P., & Friesen, W. V. (1978). *Facial Action Coding System*.
- Evinger, C., et al. (1994). Not looking while leaping: the linkage of blinking and saccadic gaze shifts. *Experimental Brain Research, 100*.
- Girard, J. M., et al. (2014). Nonverbal social withdrawal in depression: Evidence from manual and automatic analyses. *Image and Vision Computing, 32*(10).
- Harrigan, J. A., & O'Connell, D. M. (1996). How do you look when feeling anxious? Facial displays of anxiety. *Personality and Individual Differences, 21*(2).
- Hömke, P., Holler, J., & Levinson, S. C. (2017). Eye blinking as addressee feedback in face-to-face conversation. *Research on Language and Social Interaction, 50*(1).
- Krumhuber, E. G., Kappas, A., & Manstead, A. S. R. (2013). Effects of dynamic aspects of facial expressions: A review. *Emotion Review, 5*(1).
