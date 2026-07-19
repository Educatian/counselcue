import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = ROOT / "Assets" / "Models" / "CounselCue"
BLEND_OUT_DIR = ROOT / "Tools" / "Blender" / "out"
PREVIEW_DIR = ROOT / "Assets" / "Art" / "References"
OUT_DIR.mkdir(parents=True, exist_ok=True)
BLEND_OUT_DIR.mkdir(parents=True, exist_ok=True)
PREVIEW_DIR.mkdir(parents=True, exist_ok=True)


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def mat(name, color, roughness=0.72, metallic=0.0, alpha=1.0):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], alpha)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if alpha < 1.0:
        material.blend_method = "BLEND"
        bsdf.inputs["Alpha"].default_value = alpha
    return material


MATS = {}


def make_materials():
    MATS.update(
        {
            "light_oak": mat("CC_LightOak", (0.74, 0.50, 0.28), 0.55),
            "dark_walnut": mat("CC_DarkWalnut", (0.22, 0.12, 0.07), 0.58),
            "sage": mat("CC_SageFabric", (0.45, 0.54, 0.43), 0.92),
            "linen": mat("CC_LinenWarmWhite", (0.86, 0.80, 0.68), 0.95),
            "cream": mat("CC_CreamPlaster", (0.83, 0.79, 0.70), 0.86),
            "felt": mat("CC_RibbedFelt", (0.50, 0.53, 0.43), 0.96),
            "jute": mat("CC_JuteRug", (0.62, 0.48, 0.30), 0.98),
            "celadon": mat("CC_CeladonCeramic", (0.58, 0.68, 0.60), 0.38),
            "hanji": mat("CC_HanjiPaper", (0.86, 0.82, 0.72), 0.88),
            "brass": mat("CC_Brass", (0.78, 0.55, 0.25), 0.42, 1.0),
            "hud_glass": mat("CC_HudGlass", (0.04, 0.06, 0.055), 0.34, 0.0, 0.62),
            "charcoal": mat("CC_CharcoalPanel", (0.045, 0.052, 0.050), 0.68),
            "teal": mat("CC_MutedTealAccent", (0.20, 0.48, 0.38), 0.64),
            "gold": mat("CC_BrassHudAccent", (0.92, 0.68, 0.33), 0.45, 0.4),
            "leaf": mat("CC_Leaf", (0.25, 0.42, 0.22), 0.78),
            "ink": mat("CC_InkWash", (0.12, 0.13, 0.12), 0.92),
        }
    )


def cube(name, loc, scale, material):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    bevel = obj.modifiers.new("soft bevel", "BEVEL")
    bevel.width = min(scale) * 0.08
    bevel.segments = 3
    obj.modifiers.new("weighted normals", "WEIGHTED_NORMAL")
    return obj


def cyl(name, loc, radius, depth, material, vertices=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    bevel = obj.modifiers.new("soft bevel", "BEVEL")
    bevel.width = min(radius * 0.08, depth * 0.08)
    bevel.segments = 3
    obj.modifiers.new("weighted normals", "WEIGHTED_NORMAL")
    return obj


def plane(name, loc, scale, material, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_plane_add(size=1, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(material)
    return obj


def add_label(name, text, loc, size=0.12):
    font_curve = bpy.data.curves.new(name, "FONT")
    font_curve.body = text
    font_curve.align_x = "CENTER"
    font_curve.align_y = "CENTER"
    font_curve.size = size
    obj = bpy.data.objects.new(name, font_curve)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.data.materials.append(MATS["hanji"])
    return obj


def tissue_box(offset):
    x, y, z = offset
    cube("CC_TissueBox_Linen", (x, y, z + 0.18), (0.52, 0.42, 0.32), MATS["linen"])
    cube("CC_TissueBox_Slit", (x, y, z + 0.35), (0.30, 0.045, 0.018), MATS["charcoal"])
    tissue = plane("CC_TissueBox_PulledTissue", (x, y, z + 0.58), (0.22, 0.34, 1), MATS["hanji"], (math.radians(72), 0, math.radians(8)))
    tissue.modifiers.new("cloth-like bend", "SIMPLE_DEFORM").deform_method = "BEND"
    return tissue


def tea_set(offset):
    x, y, z = offset
    cyl("CC_CeladonTeaCup_Bowl", (x, y, z + 0.18), 0.18, 0.26, MATS["celadon"], 64)
    cyl("CC_CeladonTeaCup_Interior", (x, y, z + 0.30), 0.145, 0.025, MATS["charcoal"], 64)
    cyl("CC_CeladonTeaCup_Saucer", (x, y, z + 0.04), 0.26, 0.045, MATS["dark_walnut"], 64)


def round_table(offset):
    x, y, z = offset
    cyl("CC_RoundOakTable_Top", (x, y, z + 0.72), 0.56, 0.11, MATS["light_oak"], 72)
    for dx, dy in [(-0.34, -0.28), (0.34, -0.28), (-0.34, 0.28), (0.34, 0.28)]:
        leg = cyl("CC_RoundOakTable_Leg", (x + dx, y + dy, z + 0.36), 0.055, 0.64, MATS["light_oak"], 24)
        leg.rotation_euler[0] = math.radians(4 if dx > 0 else -4)


def floor_lamp(offset):
    x, y, z = offset
    cyl("CC_FloorLamp_Base", (x, y, z + 0.04), 0.24, 0.07, MATS["dark_walnut"], 64)
    cyl("CC_FloorLamp_Stand", (x, y, z + 0.82), 0.025, 1.54, MATS["brass"], 32)
    cyl("CC_FloorLamp_HanjiShade", (x, y, z + 1.62), 0.30, 0.58, MATS["hanji"], 72)
    for i in range(8):
        angle = i * math.pi / 4
        rail = cube(
            "CC_FloorLamp_ShadeRib",
            (x + math.cos(angle) * 0.305, y + math.sin(angle) * 0.305, z + 1.62),
            (0.012, 0.012, 0.56),
            MATS["brass"],
        )
        rail.rotation_euler[2] = angle


def book_stack(offset):
    x, y, z = offset
    colors = [MATS["linen"], MATS["sage"], MATS["cream"], MATS["charcoal"], MATS["dark_walnut"]]
    for i, material in enumerate(colors):
        cube(f"CC_BookStack_Book_{i+1:02}", (x, y, z + 0.055 + i * 0.105), (0.74, 0.48, 0.09), material)


def basket_plant(offset):
    x, y, z = offset
    cyl("CC_BasketPlant_WovenPot", (x, y, z + 0.22), 0.30, 0.42, MATS["jute"], 48)
    cyl("CC_BasketPlant_Soil", (x, y, z + 0.44), 0.27, 0.025, MATS["dark_walnut"], 48)
    for i in range(9):
        angle = i * math.tau / 9
        height = 0.62 + 0.10 * (i % 3)
        stem = cyl("CC_BasketPlant_Stem", (x + math.cos(angle) * 0.07, y + math.sin(angle) * 0.07, z + 0.52 + height / 2), 0.012, height, MATS["dark_walnut"], 12)
        stem.rotation_euler[0] = math.radians(10 * math.sin(angle))
        stem.rotation_euler[1] = math.radians(16 * math.cos(angle))
        for j in range(3):
            leaf = plane(
                "CC_BasketPlant_Leaf",
                (x + math.cos(angle + j * 0.35) * (0.18 + j * 0.07), y + math.sin(angle + j * 0.35) * (0.18 + j * 0.07), z + 0.82 + j * 0.14),
                (0.11, 0.045, 1),
                MATS["leaf"],
                (math.radians(70), 0, angle),
            )
            leaf.modifiers.new("leaf curve", "SIMPLE_DEFORM").deform_method = "BEND"


def artwork(offset):
    x, y, z = offset
    cube("CC_HanjiArtwork_Frame", (x, y, z + 0.62), (0.92, 0.08, 1.18), MATS["dark_walnut"])
    cube("CC_HanjiArtwork_Mat", (x, y - 0.046, z + 0.62), (0.75, 0.03, 0.98), MATS["hanji"])
    mountain = plane("CC_HanjiArtwork_InkMountain", (x, y - 0.065, z + 0.64), (0.30, 0.18, 1), MATS["ink"], (math.radians(90), 0, 0))
    mountain.name = "CC_HanjiArtwork_InkMountain_AbstractPlane"


def acoustic_panel(offset):
    x, y, z = offset
    cube("CC_AcousticRibPanel_Back", (x, y, z + 0.55), (1.15, 0.06, 1.1), MATS["felt"])
    for i in range(10):
        cube("CC_AcousticRibPanel_Rib", (x - 0.50 + i * 0.11, y - 0.055, z + 0.55), (0.045, 0.08, 1.08), MATS["felt"])


def rug(offset):
    x, y, z = offset
    cube("CC_WovenRug_Base", (x, y, z + 0.012), (1.55, 1.05, 0.024), MATS["jute"])
    for i in range(9):
        cube("CC_WovenRug_RaisedThread", (x - 0.68 + i * 0.17, y, z + 0.034), (0.018, 1.00, 0.018), MATS["linen"])


def hud_kit(offset):
    x, y, z = offset
    cube("CC_HUDKit_GlassPanel", (x, y, z + 1.12), (1.42, 0.04, 0.68), MATS["hud_glass"])
    cube("CC_HUDKit_PaperTranscript", (x + 1.75, y, z + 1.12), (0.92, 0.035, 0.98), MATS["hanji"])
    cyl("CC_HUDKit_MicButton", (x - 0.55, y - 0.10, z + 0.48), 0.22, 0.045, MATS["teal"], 64)
    cyl("CC_HUDKit_TutorialRing", (x + 0.10, y - 0.10, z + 0.48), 0.25, 0.035, MATS["gold"], 72)
    rail = cube("CC_HUDKit_ZoomRail", (x + 0.74, y - 0.10, z + 0.55), (0.12, 0.04, 0.78), MATS["charcoal"])
    for i in range(12):
        cube("CC_HUDKit_AUBar", (x - 0.56 + i * 0.075, y - 0.16, z + 1.18 + 0.025 * (i % 5)), (0.034, 0.035, 0.08 + 0.035 * (i % 5)), MATS["teal"])
    for i in range(8):
        cube("CC_HUDKit_AllianceMeterSegment", (x - 0.50 + i * 0.11, y - 0.16, z + 0.86), (0.075, 0.035, 0.10), MATS["gold" if i < 3 else "teal"])
    add_label("CC_HUDKit_Label", "HUD kit proportions", (x + 0.5, y - 0.05, z + 0.10), 0.08)


def add_scene_lighting():
    bpy.ops.object.light_add(type="AREA", location=(1.5, -5.0, 4.0))
    light = bpy.context.object
    light.name = "CC_AssetPack_Softbox"
    light.data.energy = 600
    light.data.size = 5
    bpy.ops.object.camera_add(location=(4.2, -7.0, 3.4), rotation=(math.radians(62), 0, math.radians(32)))
    bpy.context.scene.camera = bpy.context.object


def build_pack():
    clear_scene()
    make_materials()
    tissue_box((-3.4, 0, 0))
    tea_set((-2.35, 0, 0))
    round_table((-1.15, 0, 0))
    floor_lamp((0.25, 0, 0))
    book_stack((1.45, 0, 0))
    basket_plant((2.55, 0, 0))
    artwork((-2.6, 1.6, 0))
    acoustic_panel((-1.15, 1.6, 0))
    rug((0.40, 1.6, 0))
    hud_kit((2.00, 1.6, 0))
    add_scene_lighting()
    bpy.context.scene.render.resolution_x = 1600
    bpy.context.scene.render.resolution_y = 900
    bpy.context.scene.eevee.taa_render_samples = 64
    bpy.context.scene.render.filepath = str(PREVIEW_DIR / "CounselCue_BlenderAssetPackPreview.png")
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT_DIR / "CounselCueRoomAssetPack.blend"))
    bpy.ops.export_scene.fbx(filepath=str(OUT_DIR / "CounselCueRoomAssetPack.fbx"), use_selection=False, apply_unit_scale=True)
    bpy.ops.export_scene.gltf(filepath=str(OUT_DIR / "CounselCueRoomAssetPack.glb"), export_format="GLB")


if __name__ == "__main__":
    build_pack()
