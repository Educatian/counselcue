# Professional Affective Performance Design

## Design claim

The simulator does not infer what the counselor feels. It trains how a counselor's observable delivery cues combine with a counseling micro-skill and may shape the client's relational trajectory.

The proposed contribution is therefore:

> A temporally aligned multimodal feedback architecture for identifying possible mismatches between counseling micro-skills and their embodied delivery, and for modeling their downstream relationship to client safety, guardedness, and willingness to disclose.

This is a design hypothesis, not yet an empirical novelty claim. A systematic comparison with virtual-patient and automated counseling-training systems is required before publication.

## Construct → evidence → mechanic → signal

| Construct | Evidence anchor | Implemented mechanic | Telemetry signal |
|---|---|---|---|
| Perceived empathic delivery | Client, observer, and therapist perceptions of empathy are associated with psychotherapy outcome, while empathy is heterogeneous across contexts (Elliott et al., 2018). | A verbal empathic move is evaluated together with calibrated facial-delivery cues; the system reports alignment, possible mismatch, or insufficient nonverbal evidence. | `counselingMove`, `deliveryAlignment`, `deliveryEvidenceAvailable`, `au04`, `au12` |
| Dyadic relational regulation | Movement coordination in psychotherapy dyads is associated with relationship quality and outcomes, with head and body movement carrying different associations (Ramseyer & Tschacher, 2011, 2014). | Each counselor turn updates three client-facing latent states instead of labeling the counselor's emotion. Those states determine whether the next client response opens or closes. | `relationalSafety`, `guardedness`, `willingnessToDisclose`, turn-to-turn deltas |
| Korean-context helping skills | Korean counselors described culturally responsive modifications involving psychoeducation, indirect communication, validation, careful timing of insight/action skills, and nonverbal warmth; the study also warns against generalizing one sample to all Korean clients (Joo et al., 2017). | Cultural interpretation is an explicit pilot profile that changes advice-before-disclosure and distress-context smile weights. It is configurable and must be calibrated with Korean counselors and clients. | `culturalProfileId`, profile weights, rule fired, expert-rating comparison |

## Core training loop

1. The trainee chooses and delivers a counseling response.
2. The language evaluator identifies the counseling move: reflection, validation, exploration, open question, advice, silence, or neutral continuation.
3. When AU tracking is calibrated, the delivery evaluator examines only observable proxy cues currently supported by the system. It does not assign an emotion label.
4. The cross-modal evaluator returns one of four states: `aligned`, `possible-mismatch`, `relational-order-mismatch`, or `nonverbal-evidence-unavailable`.
5. The client model updates relational safety, guardedness, and willingness to disclose.
6. The next client response and avatar affect reflect the trajectory. The trainee receives one concise, actionable explanation.

## Current pilot rules

These values are **starting values**, not clinical cutoffs.

| Rule | Starting value | Interpretation boundary | Micro test plan |
|---|---:|---|---|
| Brow tension during empathic or exploratory move | calibrated AU04 proxy ≥ 0.18 | Possible delivery tension; never “counselor anxiety” | Compare system flags with blinded ratings from at least two counseling experts; move threshold toward the value maximizing agreement without over-flagging neutral delivery. |
| Smile activation during distress-focused empathic or exploratory move | calibrated AU12 proxy ≥ 0.28 | Context check for possible affective mismatch; culturally ambiguous | Present matched/mismatched clips to Korean counselor–client raters; remove or personalize the rule if ratings are inconsistent. |
| Advice before disclosure | guardedness ≥ 0.50 | Possible relational-order mismatch | Compare advice-first and exploration-first scenario variants; adjust penalty only if client-perceived safety and disclosure ratings distinguish them. |
| Aligned empathic delivery | calibrated cues below the two pilot thresholds | Small positive relational modifier, not a competency score | Confirm that experts rate feedback as plausible and non-punitive; keep the modifier smaller than the verbal-skill contribution. |

## Culture as a design variable

The Korean profile must not encode stereotypes such as “direct eye contact is always good/bad.” It stores hypotheses that can vary by client preference, age/status relationship, counseling orientation, and session phase.

Planned profile variables:

- preferred silence range and whether a pause is reflective or distancing;
- direct-gaze comfort range rather than maximum eye contact;
- honorific level and perceived psychological distance;
- advice expectation and readiness for action;
- nodding, backchannel, and smile interpretation by conversational context;
- authority expectation versus client-centered exploration.

Only advice timing and distress-context smile weighting are active in the first implementation. Gaze, speech rate, pause, nodding, and dyadic synchrony remain explicitly unavailable until their sensors and validation studies exist.

## Five-component training check

- **Clarity:** feedback names the verbal move, evidence availability, mismatch type, and affected client trajectory.
- **Motivation:** delivery changes the next client's openness, not merely a detached score.
- **Response:** a different response or delivery pattern can change the trajectory on the next turn.
- **Satisfaction:** the learner sees concise coaching text and a visible change in safety/guardedness/disclosure.
- **Fit:** the system uses a quiet professional HUD and avoids gamified emotion labels or diagnostic language.

## Abuse and validity risks

- AU proxies can be affected by anatomy, glasses, lighting, camera position, disability, and expressiveness.
- A culturally configured rule can still stereotype individuals; profiles must allow opt-out and personalization.
- The latent client states are simulation variables, not observed mental states.
- Feedback can teach performative masking if treated as a universal score. It must remain reflective, contextual, and expert-validated.
- No employment, credentialing, diagnosis, or high-stakes assessment should use these pilot outputs.

## References

- Elliott, R., Bohart, A. C., Watson, J. C., & Murphy, D. (2018). Therapist empathy and client outcome: An updated meta-analysis. *Psychotherapy, 55*(4), 399–410. https://doi.org/10.1037/pst0000175
- Joo, E., et al. (2017). Using helping skills with Korean clients: The perspectives of Korean counselors. *Psychotherapy Research*. https://doi.org/10.1080/10503307.2017.1397795
- Ramseyer, F., & Tschacher, W. (2011). Nonverbal synchrony in psychotherapy: Coordinated body movement reflects relationship quality and outcome. *Journal of Consulting and Clinical Psychology, 79*(3), 284–295. https://doi.org/10.1037/a0023419
- Ramseyer, F., & Tschacher, W. (2014). Nonverbal synchrony of head- and body-movement in psychotherapy: Different signals have different associations with outcome. *Frontiers in Psychology, 5*, 979. https://doi.org/10.3389/fpsyg.2014.00979
