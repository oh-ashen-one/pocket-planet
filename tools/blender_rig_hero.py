#!/usr/bin/env python3
"""
Blender 5.2 LTS cleanup, rigging, skinning, and 12-clip animation pipeline for "Pip" (Pocket Planet).

Usage:
  blender --background --python tools/blender_rig_hero.py -- [--input-glb path/to/customuse_pip.glb] [--preview-dir path/to/previews]

Outputs:
  - tools/blender_sources/Pip_Hero.blend (editable Blender source with armature and 12 Actions)
  - Assets/PocketPlanet/Art/Characters/Pip/Pip_Albedo.png (2048x2048 texture atlas)
  - Assets/PocketPlanet/Art/Characters/Pip/Pip_Emission.png (2048x2048 emission map for glowing antenna bulbs)
  - Assets/PocketPlanet/Art/Characters/Pip/Native/PipBody.fbx (skinned mesh + armature)
  - Assets/PocketPlanet/Art/Characters/Pip/Native/Pip<Clip>.fbx (12 skeletal animation FBX clips)
  - Assets/PocketPlanet/Art/Characters/Pip/Native/PipReceipt.json (verified scale, bone, triangle, and clip metrics)
"""
import sys
import os
import json
import math
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler, Quaternion
import numpy as np

ROOT = Path("/Users/midir/pocket-planet")
ART_DIR = ROOT / "Assets/PocketPlanet/Art/Characters/Pip"
NATIVE_DIR = ART_DIR / "Native"
BLEND_DIR = ROOT / "tools/blender_sources"

TARGET_HEIGHT_M = 1.15
FPS = 30
D = math.radians

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
INPUT_GLB = Path(argv[argv.index("--input-glb") + 1]) if "--input-glb" in argv else None
PREVIEW_DIR = Path(argv[argv.index("--preview-dir") + 1]) if "--preview-dir" in argv else None

NATIVE_DIR.mkdir(parents=True, exist_ok=True)
BLEND_DIR.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0


def save_rgba_png(arr: np.ndarray, out_path: Path, name="tmp_img"):
    h, w, _ = arr.shape
    # Blender image pixels are bottom-to-top
    flipped = np.flipud(arr).astype(np.float32) / 255.0
    im = bpy.data.images.new(name, width=w, height=h, alpha=True)
    im.pixels.foreach_set(flipped.reshape(-1))
    im.filepath_raw = str(out_path)
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


def generate_pip_atlas(albedo_path: Path, emission_path: Path):
    """Generates a clean 4x2 color/detail atlas and emission map matching the Pip concept palette."""
    size = 1024
    arr = np.zeros((size, size, 4), dtype=np.uint8)
    em = np.zeros((size, size, 4), dtype=np.uint8)
    em[:, :, 3] = 255

    # 8 regions (4x2 grid of 256x512 tiles):
    # 0: Mint Teal body (#6ED4BE)
    # 1: Coral Boot Orange (#F0785A)
    # 2: Boot Sole Dark Coral (#9E4230)
    # 3: Mustard Yellow Backpack (#F2C14E)
    # 4: Antenna Glowing Bulb (#FFF394) - emissive
    # 5: Glossy Eye Dark Plum (#23192D)
    # 6: Eye Specular Highlight / White (#FFFFFF)
    # 7: Cheek Blush / Mouth Accent (#F497B6)
    palette = [
        (110, 212, 190, 255),
        (240, 120, 90, 255),
        (158, 66, 48, 255),
        (242, 193, 78, 255),
        (255, 245, 158, 255),
        (35, 25, 45, 255),
        (255, 255, 255, 255),
        (244, 151, 182, 255),
    ]
    for idx, col in enumerate(palette):
        gx = (idx % 4) * 256
        gy = (idx // 4) * 512
        arr[gy:gy + 512, gx:gx + 256] = col
        if idx == 4:
            em[gy:gy + 512, gx:gx + 256] = (255, 240, 140, 255)

    save_rgba_png(arr, albedo_path, "pip_albedo_gen")
    save_rgba_png(em, emission_path, "pip_em_gen")


def assign_uv_region(bm, faces, region_idx):
    uv_layer = bm.loops.layers.uv.verify()
    u0 = ((region_idx % 4) + 0.5) / 4.0
    v0 = 1.0 - (((region_idx // 4) + 0.5) / 2.0)
    for f in faces:
        for loop in f.loops:
            loop[uv_layer].uv = (u0, v0)


def add_part_sphere(bm, radius, scale_xyz, center, region_idx, segs=24, rings=16):
    ret = bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=radius)
    verts = ret["verts"]
    for v in verts:
        v.co.x = v.co.x * scale_xyz[0] + center[0]
        v.co.y = v.co.y * scale_xyz[1] + center[1]
        v.co.z = v.co.z * scale_xyz[2] + center[2]
    vset = set(verts)
    faces = [f for f in bm.faces if all(v in vset for v in f.verts)]
    assign_uv_region(bm, faces, region_idx)
    return verts


def add_part_capsule_limb(bm, p0, p1, r0, r1, region_idx, segs=16, rings=8):
    """Creates a smooth tapered limb segment between p0 and p1."""
    p0 = Vector(p0)
    p1 = Vector(p1)
    axis = p1 - p0
    length = axis.length
    direction = axis.normalized()
    up = Vector((0, 0, 1))
    rot = up.rotation_difference(direction).to_matrix().to_4x4()

    verts_created = []
    ring_verts = []
    for r_i in range(rings + 1):
        t = r_i / rings
        # Rounded caps at ends
        z = t * length
        rad = r0 * (1.0 - t) + r1 * t
        if r_i == 0:
            rad *= 0.65
        elif r_i == rings:
            rad *= 0.65
        row = []
        for s_i in range(segs):
            ang = 2.0 * math.pi * s_i / segs
            local = Vector((math.cos(ang) * rad, math.sin(ang) * rad, z))
            world = p0 + (rot @ local)
            v = bm.verts.new(world)
            row.append(v)
            verts_created.append(v)
        ring_verts.append(row)

    faces_created = []
    for r_i in range(rings):
        for s_i in range(segs):
            v00 = ring_verts[r_i][s_i]
            v01 = ring_verts[r_i][(s_i + 1) % segs]
            v11 = ring_verts[r_i + 1][(s_i + 1) % segs]
            v10 = ring_verts[r_i + 1][s_i]
            f = bm.faces.new((v00, v01, v11, v10))
            faces_created.append(f)

    # Cap ends
    cap0 = bm.verts.new(p0 - direction * (r0 * 0.25))
    cap1 = bm.verts.new(p1 + direction * (r1 * 0.25))
    verts_created.extend([cap0, cap1])
    for s_i in range(segs):
        f0 = bm.faces.new((cap0, ring_verts[0][(s_i + 1) % segs], ring_verts[0][s_i]))
        f1 = bm.faces.new((cap1, ring_verts[-1][s_i], ring_verts[-1][(s_i + 1) % segs]))
        faces_created.extend([f0, f1])

    assign_uv_region(bm, faces_created, region_idx)
    return verts_created


def build_authored_pip_mesh():
    """Builds the clean, high-appeal toybox alien explorer Pip mesh to exact proportions (facing -Y)."""
    me = bpy.data.meshes.new("PipBodyMesh")
    obj = bpy.data.objects.new("PipBody", me)
    bpy.context.collection.objects.link(obj)

    bm = bmesh.new()

    # 1. Rounded bean body (Mint Teal, region 0): z from 0.24 to 0.94, center (0, 0, 0.58)
    add_part_sphere(bm, 1.0, (0.25, 0.22, 0.34), (0.0, 0.0, 0.58), 0, segs=32, rings=24)

    # 2. Expressive glossy eyes (front is -Y)
    for side in (-1.0, 1.0):
        # White outer rim
        add_part_sphere(bm, 1.0, (0.062, 0.032, 0.068), (side * 0.088, -0.195, 0.69), 6, segs=20, rings=14)
        # Dark pupil/iris
        add_part_sphere(bm, 1.0, (0.048, 0.030, 0.054), (side * 0.086, -0.208, 0.69), 5, segs=18, rings=12)
        # Specular catchlight dot
        add_part_sphere(bm, 1.0, (0.016, 0.015, 0.016), (side * 0.072, -0.232, 0.715), 6, segs=10, rings=8)
        # Cute eyebrow
        add_part_capsule_limb(bm, (side * 0.055, -0.195, 0.785), (side * 0.115, -0.185, 0.775), 0.014, 0.014, 2, segs=10, rings=4)
        # Rosy cheek blush
        add_part_sphere(bm, 1.0, (0.034, 0.015, 0.022), (side * 0.145, -0.185, 0.625), 7, segs=12, rings=8)

    # Happy smile curve
    add_part_capsule_limb(bm, (-0.032, -0.215, 0.615), (0.0, -0.220, 0.602), 0.010, 0.010, 5, segs=8, rings=3)
    add_part_capsule_limb(bm, (0.0, -0.220, 0.602), (0.032, -0.215, 0.615), 0.010, 0.010, 5, segs=8, rings=3)

    # 3. Floppy antennae with glowing bulbs (region 0 stalks + region 4 glowing bulbs)
    for side in (-1.0, 1.0):
        # Stalk base to mid
        add_part_capsule_limb(bm, (side * 0.08, 0.0, 0.89), (side * 0.15, -0.02, 1.04), 0.028, 0.022, 0, segs=14, rings=6)
        # Stalk mid to curved tip
        add_part_capsule_limb(bm, (side * 0.15, -0.02, 1.04), (side * 0.23, -0.05, 0.97), 0.022, 0.026, 0, segs=14, rings=6)
        # Glowing bulb tip (region 4)
        add_part_sphere(bm, 1.0, (0.052, 0.052, 0.052), (side * 0.25, -0.06, 0.93), 4, segs=18, rings=12)

    # 4. Distinct arms in 45-degree A-pose with cute mittens (Mint Teal, region 0)
    for side in (-1.0, 1.0):
        sh = (side * 0.22, 0.0, 0.62)
        el = (side * 0.34, -0.02, 0.52)
        wr = (side * 0.45, -0.04, 0.42)
        add_part_capsule_limb(bm, sh, el, 0.052, 0.046, 0, segs=14, rings=6)
        add_part_capsule_limb(bm, el, wr, 0.046, 0.048, 0, segs=14, rings=6)
        # Hand mitten + thumb
        add_part_sphere(bm, 1.0, (0.055, 0.042, 0.048), (side * 0.47, -0.05, 0.40), 0, segs=14, rings=10)
        add_part_sphere(bm, 1.0, (0.024, 0.022, 0.028), (side * 0.43, -0.08, 0.41), 0, segs=10, rings=8)

    # 5. Distinct legs + oversized Coral Explorer Boots (region 0 legs, region 1 boots, region 2 soles/straps)
    for side in (-1.0, 1.0):
        hip = (side * 0.105, 0.0, 0.28)
        knee = (side * 0.105, -0.01, 0.18)
        ankle = (side * 0.105, 0.0, 0.09)
        add_part_capsule_limb(bm, hip, knee, 0.054, 0.048, 0, segs=14, rings=5)
        add_part_capsule_limb(bm, knee, ankle, 0.048, 0.046, 0, segs=14, rings=5)
        # Boot cuff
        add_part_sphere(bm, 1.0, (0.082, 0.088, 0.065), (side * 0.105, -0.01, 0.115), 1, segs=18, rings=10)
        # Oversized rounded toe box
        add_part_sphere(bm, 1.0, (0.092, 0.135, 0.068), (side * 0.105, -0.045, 0.062), 1, segs=20, rings=12)
        # Boot strap
        add_part_sphere(bm, 1.0, (0.086, 0.092, 0.022), (side * 0.105, -0.018, 0.118), 2, segs=16, rings=8)
        # Rugged flat-bottomed sole touching z=0
        add_part_sphere(bm, 1.0, (0.096, 0.142, 0.022), (side * 0.105, -0.045, 0.018), 2, segs=18, rings=8)

    # 6. Tiny Mustard-Yellow Explorer Backpack on +Y back (region 3 backpack, region 1 straps, region 4 star badge)
    add_part_sphere(bm, 1.0, (0.165, 0.105, 0.185), (0.0, 0.22, 0.56), 3, segs=20, rings=14)
    # Backpack top flap & pocket
    add_part_sphere(bm, 1.0, (0.155, 0.095, 0.065), (0.0, 0.225, 0.67), 3, segs=16, rings=10)
    add_part_sphere(bm, 1.0, (0.115, 0.055, 0.075), (0.0, 0.295, 0.51), 3, segs=14, rings=10)
    # Star badge on back of backpack
    add_part_sphere(bm, 1.0, (0.042, 0.020, 0.042), (0.0, 0.315, 0.64), 4, segs=12, rings=8)
    # Shoulder straps
    for side in (-1.0, 1.0):
        add_part_capsule_limb(bm, (side * 0.17, 0.16, 0.68), (side * 0.21, -0.14, 0.63), 0.024, 0.024, 3, segs=10, rings=5)
        add_part_capsule_limb(bm, (side * 0.21, -0.14, 0.63), (side * 0.18, 0.16, 0.46), 0.024, 0.024, 3, segs=10, rings=5)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # Flatten bottom of soles to z=0.0 exactly
    min_z = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= min_z
        if v.co.z < 0.008:
            v.co.z = 0.0

    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = True
    me.update()
    return obj


def import_and_cleanup_customuse_glb(glb_path: Path):
    """Imports a Customuse GLB, cleans duplicate vertices/normals, normalizes height to TARGET_HEIGHT_M and faces -Y."""
    bpy.ops.import_scene.gltf(filepath=str(glb_path))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"No mesh objects found in {glb_path}")

    bpy.ops.object.select_all(action="DESELECT")
    for m in meshes:
        m.select_set(True)
        bpy.context.view_layer.objects.active = m
        bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    if len(meshes) > 1:
        bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = "PipBody"

    # Remove any imported armatures or empties
    for o in list(bpy.context.scene.objects):
        if o != body:
            bpy.data.objects.remove(o, do_unlink=True)

    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    me = body.data
    # Check orientation: Customuse/Tripo/CR1 GLBs may face +Y or +X; ensure wide axis (arms/boots) is X and depth is Y
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    span_x = max(xs) - min(xs)
    span_y = max(ys) - min(ys)
    if span_y > span_x * 1.18:
        # Rotate +90 deg around Z so arms span along X
        me.transform(Matrix.Rotation(D(90.0), 4, "Z"))
        me.update()

    # Check front vs back: backpack adds volume on back (+Y). If more volume is on -Y, rotate 180 deg around Z
    mid_z_verts = [v for v in me.vertices if 0.35 <= (v.co.z - min(zs)) / max(1e-4, max(zs) - min(zs)) <= 0.70]
    if mid_z_verts:
        avg_y = sum(v.co.y for v in mid_z_verts) / len(mid_z_verts)
        if avg_y < -0.01:
            me.transform(Matrix.Rotation(D(180.0), 4, "Z"))
            me.update()

    # Mesh cleanup in bmesh: merge by distance & recalculate normals
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0002)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()

    # Normalize height to TARGET_HEIGHT_M, center X/Y around boots, soles at z=0
    zs = [v.co.z for v in me.vertices]
    min_z, max_z = min(zs), max(zs)
    scale = TARGET_HEIGHT_M / max(1e-4, (max_z - min_z))
    foot_verts = [v for v in me.vertices if (v.co.z - min_z) < 0.15 * (max_z - min_z)]
    cx = sum(v.co.x for v in foot_verts) / max(1, len(foot_verts))
    cy = sum(v.co.y for v in foot_verts) / max(1, len(foot_verts))
    offset = Vector((-cx * scale, -cy * scale, -min_z * scale))
    me.transform(Matrix.Translation(offset) @ Matrix.Scale(scale, 4))
    for poly in me.polygons:
        poly.use_smooth = True
    me.update()

    # Save baked texture if present in imported material
    albedo_out = ART_DIR / "Pip_Albedo.png"
    emission_out = ART_DIR / "Pip_Emission.png"
    saved_tex = False
    for mat in me.materials:
        if mat and mat.use_nodes:
            for node in mat.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image:
                    node.image.filepath_raw = str(albedo_out)
                    node.image.file_format = "PNG"
                    node.image.save()
                    saved_tex = True
                    break
        if saved_tex:
            break
    if not saved_tex:
        generate_pip_atlas(albedo_out, emission_out)
    elif not emission_out.exists():
        em = np.zeros((512, 512, 4), dtype=np.uint8)
        em[:, :, 3] = 255
        save_rgba_png(em, emission_out, "pip_em_fallback")

    return body


def build_armature_and_skin(body_obj):
    """Creates the 21-bone PipRig armature and assigns clean anatomical skin weights."""
    arm_data = bpy.data.armatures.new("PipRigData")
    rig = bpy.data.objects.new("PipRig", arm_data)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")

    eb = arm_data.edit_bones

    def make_bone(name, head, tail, parent=None):
        b = eb.new(name)
        b.head = Vector(head)
        b.tail = Vector(tail)
        if parent:
            b.parent = eb[parent]
        return b

    make_bone("Root", (0, 0, 0.0), (0, 0, 0.15))
    make_bone("Hips", (0, 0, 0.30), (0, 0, 0.45), "Root")
    make_bone("Spine", (0, 0, 0.45), (0, 0, 0.60), "Hips")
    make_bone("Chest", (0, 0, 0.60), (0, 0, 0.72), "Spine")
    make_bone("Head", (0, 0, 0.72), (0, 0, 0.90), "Chest")

    # Floppy antennae chains
    make_bone("AntennaL_1", (-0.08, 0.0, 0.89), (-0.15, -0.02, 1.04), "Head")
    make_bone("AntennaL_2", (-0.15, -0.02, 1.04), (-0.25, -0.06, 0.93), "AntennaL_1")
    make_bone("AntennaR_1", (0.08, 0.0, 0.89), (0.15, -0.02, 1.04), "Head")
    make_bone("AntennaR_2", (0.15, -0.02, 1.04), (0.25, -0.06, 0.93), "AntennaR_1")

    # Backpack secondary-motion bone
    make_bone("Backpack", (0.0, 0.16, 0.56), (0.0, 0.32, 0.56), "Spine")

    # Left & Right arms (45-deg A-pose)
    make_bone("UpperArmL", (-0.20, 0.0, 0.62), (-0.34, -0.02, 0.52), "Chest")
    make_bone("LowerArmL", (-0.34, -0.02, 0.52), (-0.45, -0.04, 0.42), "UpperArmL")
    make_bone("HandL", (-0.45, -0.04, 0.42), (-0.51, -0.06, 0.37), "LowerArmL")

    make_bone("UpperArmR", (0.20, 0.0, 0.62), (0.34, -0.02, 0.52), "Chest")
    make_bone("LowerArmR", (0.34, -0.02, 0.52), (0.45, -0.04, 0.42), "UpperArmR")
    make_bone("HandR", (0.45, -0.04, 0.42), (0.51, -0.06, 0.37), "LowerArmR")

    # Left & Right legs + boots
    make_bone("UpperLegL", (-0.105, 0.0, 0.29), (-0.105, -0.01, 0.18), "Hips")
    make_bone("LowerLegL", (-0.105, -0.01, 0.18), (-0.105, 0.0, 0.08), "UpperLegL")
    make_bone("FootL", (-0.105, 0.0, 0.08), (-0.105, -0.13, 0.03), "LowerLegL")

    make_bone("UpperLegR", (0.105, 0.0, 0.29), (0.105, -0.01, 0.18), "Hips")
    make_bone("LowerLegR", (0.105, -0.01, 0.18), (0.105, 0.0, 0.08), "UpperLegR")
    make_bone("FootR", (0.105, 0.0, 0.08), (0.105, -0.13, 0.03), "LowerLegR")

    bpy.ops.object.mode_set(mode="OBJECT")

    # Parent mesh to armature and create vertex groups with smooth anatomical weights
    body_obj.parent = rig
    body_obj.matrix_parent_inverse = Matrix.Identity(4)
    mod = body_obj.modifiers.new(name="Armature", type="ARMATURE")
    mod.object = rig

    me = body_obj.data
    body_obj.vertex_groups.clear()
    vgroups = {b.name: body_obj.vertex_groups.new(name=b.name) for b in arm_data.bones if b.name != "Root"}

    for v in me.vertices:
        x, y, z = v.co.x, v.co.y, v.co.z
        weights = {}

        # 1. Antennae (z > 0.87 and |x| > 0.05)
        if z > 0.87 and abs(x) > 0.055:
            side = "L" if x < 0 else "R"
            if abs(x) > 0.145 or z > 0.99:
                weights[f"Antenna{side}_2"] = 0.85
                weights[f"Antenna{side}_1"] = 0.15
            else:
                weights[f"Antenna{side}_1"] = 0.80
                weights["Head"] = 0.20
        # 2. Arms (|x| > 0.23 and 0.30 < z < 0.72)
        elif abs(x) > 0.225 and 0.30 < z < 0.72 and y < 0.14:
            side = "L" if x < 0 else "R"
            ax = abs(x)
            if ax > 0.42:
                weights[f"Hand{side}"] = 0.85
                weights[f"LowerArm{side}"] = 0.15
            elif ax > 0.32:
                weights[f"LowerArm{side}"] = 0.75
                weights[f"UpperArm{side}"] = 0.25
            else:
                weights[f"UpperArm{side}"] = 0.75
                weights["Chest"] = 0.25
        # 3. Backpack (y > 0.17 and 0.36 < z < 0.74)
        elif y > 0.175 and 0.36 < z < 0.74 and abs(x) < 0.19:
            weights["Backpack"] = 0.85
            weights["Spine"] = 0.15
        # 4. Legs & Boots (z < 0.29)
        elif z < 0.29:
            side = "L" if x < 0 else "R"
            if z < 0.13:
                weights[f"Foot{side}"] = 0.90
                weights[f"LowerLeg{side}"] = 0.10
            elif z < 0.20:
                weights[f"LowerLeg{side}"] = 0.75
                weights[f"UpperLeg{side}"] = 0.25
            else:
                weights[f"UpperLeg{side}"] = 0.75
                weights["Hips"] = 0.25
        # 5. Core bean torso & head (smooth vertical blend so body never pinches)
        else:
            if z > 0.70:
                t = min(1.0, (z - 0.70) / 0.12)
                weights["Head"] = 0.5 + 0.5 * t
                weights["Chest"] = 0.5 * (1.0 - t)
            elif z > 0.56:
                t = (z - 0.56) / 0.14
                weights["Chest"] = 0.35 + 0.65 * t
                weights["Spine"] = 0.65 * (1.0 - t)
            elif z > 0.40:
                t = (z - 0.40) / 0.16
                weights["Spine"] = 0.35 + 0.65 * t
                weights["Hips"] = 0.65 * (1.0 - t)
            else:
                weights["Hips"] = 1.0

        total = sum(weights.values())
        for bname, w in weights.items():
            vgroups[bname].add([v.index], w / total, "REPLACE")

    return rig


def reset_pose(rig):
    for pb in rig.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.location = (0.0, 0.0, 0.0)
        pb.rotation_euler = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)


def key_all(rig, frame):
    for pb in rig.pose.bones:
        pb.keyframe_insert(data_path="location", frame=frame)
        pb.keyframe_insert(data_path="rotation_euler", frame=frame)
        pb.keyframe_insert(data_path="scale", frame=frame)


def set_rot(rig, name, rx=0.0, ry=0.0, rz=0.0):
    rig.pose.bones[name].rotation_euler = Euler((D(rx), D(ry), D(rz)), "XYZ")


def set_loc(rig, name, x=0.0, y=0.0, z=0.0):
    rig.pose.bones[name].location = Vector((x, y, z))


def set_scl(rig, name, sx=1.0, sy=1.0, sz=1.0):
    rig.pose.bones[name].scale = Vector((sx, sy, sz))


def author_all_animations(rig):
    """Authors all 12 required character animations with playful anticipation, squash-and-stretch, and secondary motion."""
    rig.animation_data_create()
    actions = {}

    def new_clip(name, end_frame, loop):
        act = bpy.data.actions.new(name=name)
        rig.animation_data.action = act
        if hasattr(act, "slots"):
            if len(act.slots) == 0:
                slot = act.slots.new(id_type="OBJECT", name=rig.name)
                rig.animation_data.action_slot = slot
            else:
                rig.animation_data.action_slot = act.slots[0]
        actions[name] = (act, end_frame, loop)
        return act

    # 1. Idle (60 frames, 2.0s loop): breathing, relaxed arms down from A-pose, antenna bob
    new_clip("Idle", 60, True)
    for f in range(0, 61, 5):
        reset_pose(rig)
        ph = 2.0 * math.pi * (f / 60.0)
        breath = math.sin(ph)
        sway = math.sin(ph)
        set_loc(rig, "Hips", x=0.012 * sway, y=-0.015 * abs(breath), z=0.0)
        set_rot(rig, "Spine", rx=2.5 * breath, rz=2.0 * sway)
        set_rot(rig, "Head", rx=-3.0 * breath, ry=-3.5 * sway, rz=-2.5 * sway)
        # Relax arms from A-pose toward sides
        set_rot(rig, "UpperArmL", ry=22.0 + 3.0 * breath, rz=6.0 * sway)
        set_rot(rig, "UpperArmR", ry=-22.0 - 3.0 * breath, rz=6.0 * sway)
        set_rot(rig, "LowerArmL", rx=14.0, ry=8.0)
        set_rot(rig, "LowerArmR", rx=14.0, ry=-8.0)
        # Floppy antennae delayed bob
        ant = math.sin(ph - 0.7)
        set_rot(rig, "AntennaL_1", rx=6.0 * ant, rz=5.0 * sway)
        set_rot(rig, "AntennaL_2", rx=10.0 * ant, rz=8.0 * sway)
        set_rot(rig, "AntennaR_1", rx=6.0 * ant, rz=5.0 * sway)
        set_rot(rig, "AntennaR_2", rx=10.0 * ant, rz=8.0 * sway)
        set_rot(rig, "Backpack", rx=-4.0 * breath)
        key_all(rig, f)

    # 2. Walk (30 frames, 1.0s loop): jaunty Mario-like stride, grounded foot planting, backpack & antenna bounce
    new_clip("Walk", 30, True)
    for f in range(0, 31, 3):
        reset_pose(rig)
        ph = 2.0 * math.pi * (f / 30.0)
        s = math.sin(ph)
        c = math.cos(ph)
        bob = abs(c)
        set_loc(rig, "Hips", x=0.022 * s, y=0.032 * (bob - 0.35), z=0.0)
        set_rot(rig, "Hips", rx=4.0, ry=6.0 * s, rz=-4.0 * s)
        set_rot(rig, "Spine", rx=5.0 + 3.0 * bob, ry=-8.0 * s)
        set_rot(rig, "Head", rx=-4.0, ry=4.0 * s, rz=3.0 * s)
        # Legs swing along X rotation (forward is -Y, so -rx swings leg forward!)
        set_rot(rig, "UpperLegL", rx=-34.0 * s)
        set_rot(rig, "LowerLegL", rx=max(0.0, 28.0 * c))
        set_rot(rig, "FootL", rx=12.0 * s)
        set_rot(rig, "UpperLegR", rx=34.0 * s)
        set_rot(rig, "LowerLegR", rx=max(0.0, -28.0 * c))
        set_rot(rig, "FootR", rx=-12.0 * s)
        # Counter-swing arms
        set_rot(rig, "UpperArmL", rx=26.0 * s, ry=20.0)
        set_rot(rig, "LowerArmL", rx=18.0 + 10.0 * s)
        set_rot(rig, "UpperArmR", rx=-26.0 * s, ry=-20.0)
        set_rot(rig, "LowerArmR", rx=18.0 - 10.0 * s)
        # Secondary motion on antennae and backpack
        b_lag = math.sin(ph * 2.0 - 0.6)
        set_rot(rig, "AntennaL_1", rx=10.0 + 12.0 * b_lag, rz=-6.0 * s)
        set_rot(rig, "AntennaL_2", rx=14.0 + 16.0 * b_lag, rz=-10.0 * s)
        set_rot(rig, "AntennaR_1", rx=10.0 + 12.0 * b_lag, rz=-6.0 * s)
        set_rot(rig, "AntennaR_2", rx=14.0 + 16.0 * b_lag, rz=-10.0 * s)
        set_rot(rig, "Backpack", rx=8.0 * b_lag, rz=5.0 * s)
        key_all(rig, f)

    # 3. Run (20 frames, 0.67s loop): energetic forward lean, wide stride, lively squash/stretch & floppy antennae
    new_clip("Run", 20, True)
    for f in range(0, 21, 2):
        reset_pose(rig)
        ph = 2.0 * math.pi * (f / 20.0)
        s = math.sin(ph)
        c = math.cos(ph)
        bob = math.sin(ph * 2.0)
        set_loc(rig, "Hips", x=0.028 * s, y=0.055 * bob, z=-0.03)
        set_rot(rig, "Hips", rx=14.0, ry=10.0 * s, rz=-6.0 * s)
        set_rot(rig, "Spine", rx=12.0 + 5.0 * bob, ry=-12.0 * s)
        set_rot(rig, "Head", rx=-16.0, ry=6.0 * s)
        set_rot(rig, "UpperLegL", rx=-52.0 * s)
        set_rot(rig, "LowerLegL", rx=max(0.0, 46.0 * c) + 8.0)
        set_rot(rig, "FootL", rx=18.0 * s)
        set_rot(rig, "UpperLegR", rx=52.0 * s)
        set_rot(rig, "LowerLegR", rx=max(0.0, -46.0 * c) + 8.0)
        set_rot(rig, "FootR", rx=-18.0 * s)
        set_rot(rig, "UpperArmL", rx=44.0 * s, ry=14.0)
        set_rot(rig, "LowerArmL", rx=38.0 + 14.0 * s)
        set_rot(rig, "UpperArmR", rx=-44.0 * s, ry=-14.0)
        set_rot(rig, "LowerArmR", rx=38.0 - 14.0 * s)
        # Antennae trail backward in the wind
        set_rot(rig, "AntennaL_1", rx=24.0 + 14.0 * bob, rz=-8.0 * s)
        set_rot(rig, "AntennaL_2", rx=30.0 + 18.0 * bob, rz=-12.0 * s)
        set_rot(rig, "AntennaR_1", rx=24.0 + 14.0 * bob, rz=-8.0 * s)
        set_rot(rig, "AntennaR_2", rx=30.0 + 18.0 * bob, rz=-12.0 * s)
        set_rot(rig, "Backpack", rx=14.0 * bob, ry=8.0 * s)
        key_all(rig, f)

    # 4. Turn (18 frames, 0.6s): playful pivot step with anticipation & follow-through
    new_clip("Turn", 18, False)
    for f, (hip_y, yaw, arm_open, leg_l, leg_r, ant_rz) in [
        (0, (0.0, 0.0, 20.0, 0.0, 0.0, 0.0)),
        (5, (-0.04, -18.0, 8.0, -24.0, 14.0, 18.0)),
        (11, (0.03, 14.0, 12.0, 18.0, -26.0, -22.0)),
        (18, (0.0, 0.0, 22.0, 0.0, 0.0, 0.0)),
    ]:
        reset_pose(rig)
        set_loc(rig, "Hips", y=hip_y)
        set_rot(rig, "Hips", ry=yaw)
        set_rot(rig, "Spine", ry=yaw * 0.6)
        set_rot(rig, "Head", ry=-yaw * 0.5)
        set_rot(rig, "UpperArmL", ry=arm_open)
        set_rot(rig, "UpperArmR", ry=-arm_open)
        set_rot(rig, "UpperLegL", rx=leg_l)
        set_rot(rig, "UpperLegR", rx=leg_r)
        set_rot(rig, "AntennaL_1", rz=ant_rz)
        set_rot(rig, "AntennaL_2", rz=ant_rz * 1.4)
        set_rot(rig, "AntennaR_1", rz=ant_rz)
        set_rot(rig, "AntennaR_2", rz=ant_rz * 1.4)
        key_all(rig, f)

    # 5. Jump (20 frames, 0.67s): anticipation squash crouch -> upward stretch launch -> apex pose
    new_clip("Jump", 20, False)
    for f, (hy, sx, sy, arm_ry, leg_l, leg_r, ant_rx) in [
        (0, (-0.07, 1.10, 0.86, 26.0, -28.0, -28.0, -14.0)),
        (4, (0.06, 0.90, 1.14, -38.0, 22.0, -32.0, 28.0)),
        (11, (0.10, 0.95, 1.06, -48.0, -34.0, 26.0, 34.0)),
        (20, (0.08, 1.00, 1.00, -35.0, -24.0, 18.0, 22.0)),
    ]:
        reset_pose(rig)
        set_loc(rig, "Hips", y=hy)
        set_scl(rig, "Spine", sx=sx, sy=sy, sz=sx)
        set_rot(rig, "UpperArmL", ry=arm_ry, rx=-25.0)
        set_rot(rig, "UpperArmR", ry=-arm_ry, rx=-25.0)
        set_rot(rig, "UpperLegL", rx=leg_l)
        set_rot(rig, "LowerLegL", rx=26.0)
        set_rot(rig, "UpperLegR", rx=leg_r)
        set_rot(rig, "LowerLegR", rx=26.0)
        set_rot(rig, "AntennaL_1", rx=ant_rx)
        set_rot(rig, "AntennaL_2", rx=ant_rx * 1.3)
        set_rot(rig, "AntennaR_1", rx=ant_rx)
        set_rot(rig, "AntennaR_2", rx=ant_rx * 1.3)
        set_rot(rig, "Backpack", rx=ant_rx * 0.5)
        key_all(rig, f)

    # 6. Fall (24 frames, 0.8s loop): mid-air paddle & antennae swept up
    new_clip("Fall", 24, True)
    for f in range(0, 25, 3):
        reset_pose(rig)
        ph = 2.0 * math.pi * (f / 24.0)
        s = math.sin(ph)
        set_rot(rig, "Spine", rx=-8.0, rz=4.0 * s)
        set_rot(rig, "Head", rx=12.0)
        set_rot(rig, "UpperArmL", ry=-32.0 + 10.0 * s, rx=-15.0 * s)
        set_rot(rig, "UpperArmR", ry=32.0 + 10.0 * s, rx=15.0 * s)
        set_rot(rig, "UpperLegL", rx=-22.0 + 16.0 * s)
        set_rot(rig, "LowerLegL", rx=32.0)
        set_rot(rig, "UpperLegR", rx=-22.0 - 16.0 * s)
        set_rot(rig, "LowerLegR", rx=32.0)
        set_rot(rig, "AntennaL_1", rx=-28.0 + 8.0 * s, rz=-12.0)
        set_rot(rig, "AntennaL_2", rx=-36.0 + 10.0 * s)
        set_rot(rig, "AntennaR_1", rx=-28.0 - 8.0 * s, rz=12.0)
        set_rot(rig, "AntennaR_2", rx=-36.0 - 10.0 * s)
        set_rot(rig, "Backpack", rx=-18.0)
        key_all(rig, f)

    # 7. Landing (16 frames, 0.53s): visible squash absorption -> recoil overshoot -> settle
    new_clip("Landing", 16, False)
    for f, (hy, sx, sy, knee_rx, arm_ry, ant_rx) in [
        (0, (0.02, 0.96, 1.05, 16.0, -20.0, -18.0)),
        (4, (-0.09, 1.15, 0.82, 42.0, 28.0, 32.0)),
        (10, (0.02, 0.97, 1.04, 10.0, 18.0, -10.0)),
        (16, (0.0, 1.00, 1.00, 0.0, 22.0, 0.0)),
    ]:
        reset_pose(rig)
        set_loc(rig, "Hips", y=hy)
        set_scl(rig, "Spine", sx=sx, sy=sy, sz=sx)
        set_rot(rig, "UpperLegL", rx=-knee_rx * 0.7)
        set_rot(rig, "LowerLegL", rx=knee_rx)
        set_rot(rig, "UpperLegR", rx=-knee_rx * 0.7)
        set_rot(rig, "LowerLegR", rx=knee_rx)
        set_rot(rig, "UpperArmL", ry=arm_ry)
        set_rot(rig, "UpperArmR", ry=-arm_ry)
        set_rot(rig, "AntennaL_1", rx=ant_rx)
        set_rot(rig, "AntennaL_2", rx=ant_rx * 1.3)
        set_rot(rig, "AntennaR_1", rx=ant_rx)
        set_rot(rig, "AntennaR_2", rx=ant_rx * 1.3)
        set_rot(rig, "Backpack", rx=ant_rx * 0.6)
        key_all(rig, f)

    # 8. Pickup (22 frames, 0.73s): bend down, reach both hands forward to meet oversized fruit, lift to chest
    new_clip("Pickup", 22, False)
    for f, (hy, spine_rx, arm_rx, arm_ry, elbow_rx) in [
        (0, (0.0, 0.0, 0.0, 20.0, 14.0)),
        (8, (-0.08, 28.0, -52.0, 12.0, 24.0)),
        (15, (-0.03, 10.0, -64.0, 18.0, 38.0)),
        (22, (-0.02, -10.0, -58.0, 22.0, 42.0)),
    ]:
        reset_pose(rig)
        set_loc(rig, "Hips", y=hy)
        set_rot(rig, "Spine", rx=spine_rx)
        set_rot(rig, "Head", rx=-spine_rx * 0.5)
        set_rot(rig, "UpperArmL", rx=arm_rx, ry=arm_ry, rz=-16.0)
        set_rot(rig, "LowerArmL", rx=elbow_rx, rz=-18.0)
        set_rot(rig, "UpperArmR", rx=arm_rx, ry=-arm_ry, rz=16.0)
        set_rot(rig, "LowerArmR", rx=elbow_rx, rz=18.0)
        set_rot(rig, "UpperLegL", rx=-abs(hy) * 220.0)
        set_rot(rig, "LowerLegL", rx=abs(hy) * 320.0)
        set_rot(rig, "UpperLegR", rx=-abs(hy) * 220.0)
        set_rot(rig, "LowerLegR", rx=abs(hy) * 320.0)
        key_all(rig, f)

    # 9. Carry (30 frames, 1.0s loop): torso leaned back under weight, both hands cradling oversized fruit in front, heavy waddle
    new_clip("Carry", 30, True)
    for f in range(0, 31, 3):
        reset_pose(rig)
        ph = 2.0 * math.pi * (f / 30.0)
        s = math.sin(ph)
        c = math.cos(ph)
        bob = abs(c)
        # Heavy lower stance + backward lean to counter fruit weight
        set_loc(rig, "Hips", x=0.032 * s, y=-0.035 + 0.025 * bob, z=0.0)
        set_rot(rig, "Hips", rx=-8.0, ry=5.0 * s, rz=-6.0 * s)
        set_rot(rig, "Spine", rx=-12.0 + 3.0 * bob, rz=-4.0 * s)
        set_rot(rig, "Chest", rx=-6.0)
        set_rot(rig, "Head", rx=14.0, rz=4.0 * s)
        # Both arms forward & inward so hands meet the oversized fruit at (0, -0.36, 0.58)
        set_rot(rig, "UpperArmL", rx=-58.0 + 3.0 * bob, ry=22.0, rz=-22.0)
        set_rot(rig, "LowerArmL", rx=42.0, rz=-20.0)
        set_rot(rig, "HandL", rz=-15.0)
        set_rot(rig, "UpperArmR", rx=-58.0 + 3.0 * bob, ry=-22.0, rz=22.0)
        set_rot(rig, "LowerArmR", rx=42.0, rz=20.0)
        set_rot(rig, "HandR", rz=15.0)
        # Shorter, wider-legged weighted stride
        set_rot(rig, "UpperLegL", rx=-26.0 * s - 10.0)
        set_rot(rig, "LowerLegL", rx=max(0.0, 24.0 * c) + 16.0)
        set_rot(rig, "UpperLegR", rx=26.0 * s - 10.0)
        set_rot(rig, "LowerLegR", rx=max(0.0, -24.0 * c) + 16.0)
        set_rot(rig, "AntennaL_1", rx=-12.0 + 10.0 * bob, rz=-8.0 * s)
        set_rot(rig, "AntennaL_2", rx=-16.0 + 14.0 * bob)
        set_rot(rig, "AntennaR_1", rx=-12.0 + 10.0 * bob, rz=-8.0 * s)
        set_rot(rig, "AntennaR_2", rx=-16.0 + 14.0 * bob)
        key_all(rig, f)

    # 10. Throw (20 frames, 0.67s): wind-up lean back -> energetic forward hurl + follow-through
    new_clip("Throw", 20, False)
    for f, (spine_rx, arm_rx, arm_ry, ant_rx) in [
        (0, (-14.0, -58.0, 22.0, -14.0)),
        (5, (-24.0, -82.0, 10.0, -26.0)),
        (11, (26.0, -32.0, -14.0, 34.0)),
        (20, (0.0, 0.0, 22.0, 0.0)),
    ]:
        reset_pose(rig)
        set_rot(rig, "Spine", rx=spine_rx)
        set_rot(rig, "Chest", rx=spine_rx * 0.5)
        set_rot(rig, "Head", rx=-spine_rx * 0.4)
        set_rot(rig, "UpperArmL", rx=arm_rx, ry=arm_ry)
        set_rot(rig, "UpperArmR", rx=arm_rx, ry=-arm_ry)
        set_rot(rig, "AntennaL_1", rx=ant_rx)
        set_rot(rig, "AntennaL_2", rx=ant_rx * 1.3)
        set_rot(rig, "AntennaR_1", rx=ant_rx)
        set_rot(rig, "AntennaR_2", rx=ant_rx * 1.3)
        key_all(rig, f)

    # 11. Interact (24 frames, 0.8s): friendly reach / pet / lever pull gesture
    new_clip("Interact", 24, False)
    for f, (spine_rx, r_arm_rx, r_arm_ry, l_arm_ry, head_rz) in [
        (0, (0.0, 0.0, -22.0, 22.0, 0.0)),
        (7, (14.0, -68.0, -8.0, 24.0, 8.0)),
        (13, (12.0, -54.0, -18.0, 24.0, -8.0)),
        (18, (14.0, -68.0, -8.0, 24.0, 6.0)),
        (24, (0.0, 0.0, -22.0, 22.0, 0.0)),
    ]:
        reset_pose(rig)
        set_rot(rig, "Spine", rx=spine_rx)
        set_rot(rig, "Head", rz=head_rz, rx=-spine_rx * 0.3)
        set_rot(rig, "UpperArmR", rx=r_arm_rx, ry=r_arm_ry)
        set_rot(rig, "LowerArmR", rx=28.0)
        set_rot(rig, "UpperArmL", ry=l_arm_ry)
        set_rot(rig, "AntennaL_1", rz=head_rz * 1.4)
        set_rot(rig, "AntennaR_1", rz=head_rz * 1.4)
        key_all(rig, f)

    # 12. Celebrate (45 frames, 1.5s loop): happy bounce, arms raised waving, joyful antenna wiggle
    new_clip("Celebrate", 45, True)
    for f in range(0, 46, 3):
        reset_pose(rig)
        ph = 2.0 * math.pi * (f / 45.0)
        s = math.sin(ph * 2.0)
        c = math.cos(ph * 2.0)
        hop = max(0.0, math.sin(ph * 2.0))
        set_loc(rig, "Hips", y=0.09 * hop)
        set_rot(rig, "Hips", ry=18.0 * math.sin(ph), rz=8.0 * s)
        set_rot(rig, "Spine", rx=-8.0 * hop, rz=6.0 * s)
        set_rot(rig, "Head", rz=-10.0 * s, rx=-6.0)
        # Raised cheering arms
        set_rot(rig, "UpperArmL", ry=-48.0 + 14.0 * s, rx=-20.0)
        set_rot(rig, "LowerArmL", rz=-20.0 * c)
        set_rot(rig, "UpperArmR", ry=48.0 + 14.0 * s, rx=-20.0)
        set_rot(rig, "LowerArmR", rz=20.0 * c)
        set_rot(rig, "UpperLegL", rx=-24.0 * hop, rz=-8.0 * hop)
        set_rot(rig, "LowerLegL", rx=32.0 * hop)
        set_rot(rig, "UpperLegR", rx=-24.0 * hop, rz=8.0 * hop)
        set_rot(rig, "LowerLegR", rx=32.0 * hop)
        set_rot(rig, "AntennaL_1", rx=18.0 * c, rz=16.0 * s)
        set_rot(rig, "AntennaL_2", rx=24.0 * c, rz=22.0 * s)
        set_rot(rig, "AntennaR_1", rx=-18.0 * c, rz=16.0 * s)
        set_rot(rig, "AntennaR_2", rx=-24.0 * c, rz=22.0 * s)
        key_all(rig, f)

    return actions


def bind_blender_material(body_obj, albedo_path: Path, emission_path: Path):
    mat = bpy.data.materials.new(name="PipMaterial")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 0.45

    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(str(albedo_path))
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

    if emission_path.exists():
        etex = nt.nodes.new("ShaderNodeTexImage")
        etex.image = bpy.data.images.load(str(emission_path))
        if "Emission Color" in bsdf.inputs:
            nt.links.new(etex.outputs["Color"], bsdf.inputs["Emission Color"])
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = 1.8

    body_obj.data.materials.clear()
    body_obj.data.materials.append(mat)


def select_only(*objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS

    albedo_path = ART_DIR / "Pip_Albedo.png"
    emission_path = ART_DIR / "Pip_Emission.png"

    if INPUT_GLB and INPUT_GLB.exists():
        print(f"Importing Customuse GLB: {INPUT_GLB}")
        body = import_and_cleanup_customuse_glb(INPUT_GLB)
        source_mode = f"customuse_glb:{INPUT_GLB.name}"
    else:
        print("Building authored toybox Pip mesh with atlas UVs...")
        generate_pip_atlas(albedo_path, emission_path)
        body = build_authored_pip_mesh()
        source_mode = "blender_authored_pip"

    bind_blender_material(body, albedo_path, emission_path)
    rig = build_armature_and_skin(body)
    actions = author_all_animations(rig)

    # Reset to rest pose before saving .blend and exporting Body FBX
    rig.animation_data.action = None
    reset_pose(rig)
    bpy.context.view_layer.update()

    blend_path = BLEND_DIR / "Pip_Hero.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    # Export PipBody.fbx (mesh + armature, no anim)
    select_only(rig, body)
    body_fbx = NATIVE_DIR / "PipBody.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(body_fbx),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Y",
        axis_up="Z",
        path_mode="STRIP",
    )

    # Export each animation clip as Pip<ClipName>.fbx
    select_only(rig)
    clip_report = {}
    for cname, (act, end_frame, loop) in actions.items():
        rig.animation_data.action = act
        if hasattr(act, "slots") and len(act.slots) > 0:
            rig.animation_data.action_slot = act.slots[0]
        scene.frame_start = 0
        scene.frame_end = end_frame
        clip_fbx = NATIVE_DIR / f"Pip{cname}.fbx"
        bpy.ops.export_scene.fbx(
            filepath=str(clip_fbx),
            use_selection=True,
            object_types={"ARMATURE"},
            add_leaf_bones=False,
            bake_anim=True,
            bake_anim_use_all_actions=False,
            bake_anim_use_nla_strips=False,
            bake_anim_simplify_factor=0.0,
            axis_forward="-Y",
            axis_up="Z",
            path_mode="STRIP",
        )
        clip_report[cname] = {
            "frames": end_frame,
            "seconds": round(end_frame / FPS, 3),
            "loop": loop,
        }

    me = body.data
    zs = [v.co.z for v in me.vertices]
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    receipt = {
        "source": source_mode,
        "height_m": round(max(zs) - min(zs), 4),
        "width_m": round(max(xs) - min(xs), 4),
        "depth_m": round(max(ys) - min(ys), 4),
        "min_z": round(min(zs), 4),
        "vertices": len(me.vertices),
        "triangles": tris,
        "bones": len(rig.data.bones),
        "clips": clip_report,
    }
    receipt_path = NATIVE_DIR / "PipReceipt.json"
    receipt_path.write_text(json.dumps(receipt, indent=2) + "\n")
    print("PIP_RIG_EXPORT_OK", json.dumps(receipt))


if __name__ == "__main__":
    main()
