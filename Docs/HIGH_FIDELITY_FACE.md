# High-fidelity face path

CounselCue treats a facial action as a semantic channel (`AU_12`, blink, Korean viseme, and so on), not as a Rocketbox-specific mesh index. This lets the counseling-state model survive a future head replacement.

## Implemented WebGL tier

- combined-versus-lateral blendshape conflict suppression
- critically damped, velocity-limited morph motion
- asymmetric blink timing with a short closed-eye hold
- Korean viseme coarticulation instead of hard phoneme switching
- coupled jaw/lip opening and speech-time lip-pressor suppression
- low-amplitude, slowly changing left/right asymmetry
- rig-independent AU and viseme name adapter

This is the highest-value motion upgrade available without replacing the Rocketbox head. It reduces popping and rubber-like over-deformation, but it does not add geometric detail that is absent from the source mesh.

## Premium desktop tier

An Unreal/MetaHuman-class result requires a new licensed head with:

- high-resolution neutral mesh and scan-quality albedo/normal maps
- separate cornea, sclera, iris, pupil, tear line, eyelashes, teeth and tongue
- left/right facial controls plus pose-space corrective shapes
- per-expression wrinkle normal or displacement maps
- at least ARKit/FACS-equivalent coverage and Korean visemes
- HDRP skin shader with subsurface scattering for the desktop research build

The desktop tier should keep the current semantic API and supply only a new mapping profile. The public WebGL tier remains optimized and uses the same counseling behavior model.

## Acceptance gates

1. No single-frame morph change greater than the configured velocity limit.
2. No combined AU and its left/right variants are driven together.
3. Korean phonemes blend across boundaries without a zero-mouth frame.
4. Eye closure, pupil direction and eyelid follow remain readable at the face-observation zoom.
5. Expert naturalness median reaches 4/5 in the designated close-up clips.
