# CounselCue Asset Upgrade Brief

## Production Goal

Upgrade CounselCue from a readable prototype room into a believable Korean 1:1 counseling training space with HUD assets that support close observation of facial expression, gaze, gesture, voice, and counselor delivery.

## Visual References

- `Assets/Art/References/CounselCue_RealisticRoom_Reference.png`
- `Assets/Art/References/CounselCue_HUD_Reference.png`
- `Assets/Art/References/CounselCue_PropSheet_Reference.png`
- `Assets/Art/References/CounselCue_MaterialSwatches_Reference.png`
- `Assets/Art/References/CounselCue_HUDComponents_Reference.png`

## What Felt Weak Before

- The room still read as a procedural Unity set because many objects were cube/cylinder primitives.
- The counseling distance was improved, but the foreground lacked tactile counselor-view cues such as tea, tissue, table edge, and soft occluding chair fabric.
- Wall decor existed, but it needed richer Korean counseling-office signals: hanji art, ribbed acoustic paneling, books, linen curtains, warm side lighting.
- Plant forms were too symbolic; believable rooms need variation in pot material, leaf scale, and imperfect arrangements.
- The HUD worked but still relied on flat rectangular panels and text labels. Observation controls should feel like a professional training instrument with icon-first controls.
- AU/webcam feedback needed a more visual “signal instrument” language, not just status text.
- Tutorial spotlight should be a visible component in the visual system, not a generic overlay.

## Blender Asset Targets

Create a single importable pack containing these objects:

- `CC_TissueBox_Linen`: linen cube tissue box with raised tissue sheet.
- `CC_CeladonTeaCup`: simple celadon cup and saucer for the side table.
- `CC_RoundOakTable`: low round table with bevels, thick legs, dark foot shadows.
- `CC_FloorLamp_Hanji`: slim brass stand, round base, paper shade.
- `CC_BookStack_Counseling`: five plain books, muted fabric covers, no readable titles.
- `CC_BasketPlant`: woven basket pot, stems, varied leaves.
- `CC_HanjiArtwork_Frame`: dark oak frame, mat board, grayscale mountain plane.
- `CC_AcousticRibPanel`: ribbed felt wall panel module.
- `CC_WovenRug`: thin rectangular rug with raised thread strips.
- `CC_HUDKit`: world-space reference objects for glass panel, paper transcript card, AU bars, alliance meter, zoom rail, mic button, tutorial ring.

## Material Targets

- `CC_LightOak`
- `CC_DarkWalnut`
- `CC_SageFabric`
- `CC_LinenWarmWhite`
- `CC_CreamPlaster`
- `CC_RibbedFelt`
- `CC_JuteRug`
- `CC_CeladonCeramic`
- `CC_HanjiPaper`
- `CC_Brass`
- `CC_HudGlass`
- `CC_CharcoalPanel`

## Unity Integration Notes

- Keep the client face unobstructed at 100% zoom.
- Place tactile props in foreground and side table, not between camera and client face.
- Use HUD controls on edges only; reserve the center for facial AU, gaze, and gesture observation.
- Prefer icon buttons for mic, zoom in/out, reset, language, notes, and settings.
- Do not add readable book titles or faux clinical documents.
- Avoid over-warm monochrome beige; preserve sage, teal, brass, charcoal contrast.

## Blender Execution

Run from this project root after installing a CLI-accessible desktop Blender:

```powershell
blender --background --python Tools/Blender/build_counselcue_assets.py
```

The script writes:

- `Tools/Blender/out/CounselCueRoomAssetPack.blend`
- `Assets/Models/CounselCue/CounselCueRoomAssetPack.fbx`
- `Assets/Models/CounselCue/CounselCueRoomAssetPack.glb`
