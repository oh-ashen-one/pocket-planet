#!/usr/bin/env python3
"""
Blender 5.2 LTS generator & Customuse GLB integrator for Pocket Planet world props and creatures.

Usage:
  blender --background --python tools/blender_build_world_assets.py

Outputs FBX assets + shared palette texture atlas to:
  Assets/PocketPlanet/Art/Props/
and saves editable Blender source:
  tools/blender_sources/PocketPlanet_WorldAssets.blend
"""
import os
import sys
import json
import math
from pathlib import Path
import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix, Euler

ROOT = Path("/Users/midir/pocket-planet")
PROP_DIR = ROOT / "Assets/PocketPlanet/Art/Props"
CUSTOMUSE_DIR = ROOT / "tools/customuse_sources"
BLEND_DIR = ROOT / "tools/blender_sources"

PROP_DIR.mkdir(parents=True, exist_ok=True)
BLEND_DIR.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
D = math.radians


def save_rgba_png(arr: np.ndarray, out_path: Path, name="tmp_img"):
    h, w, _ = arr.shape
    flipped = np.flipud(arr).astype(np.float32) / 255.0
    im = bpy.data.images.new(name, width=w, height=h, alpha=True)
    im.pixels.foreach_set(flipped.reshape(-1))
    im.filepath_raw = str(out_path)
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


# 16-tile (4x4) cohesive toybox space-garden palette
PALETTE = [
    (110, 212, 190, 255),  # 0: Mint Teal
    (240, 120, 90, 255),   # 1: Coral Orange
    (245, 202, 82, 255),   # 2: Starlight Gold
    (182, 164, 214, 255),  # 3: Lavender Rock
    (244, 151, 192, 255),  # 4: Blossom Pink
    (228, 72, 148, 255),   # 5: Starberry Magenta
    (92, 218, 156, 255),   # 6: Sprout Leaf Green
    (112, 235, 245, 255),  # 7: Jelly Cyan Glow (emissive)
    (255, 236, 120, 255),  # 8: Radiant Star Yellow (emissive)
    (248, 224, 186, 255),  # 9: Warm Cream Pod Wall
    (142, 118, 178, 255),  # 10: Deep Violet Stone
    (38, 28, 52, 255),     # 11: Glossy Dark Eye
    (255, 255, 255, 255),  # 12: Pure White Highlight
    (235, 138, 112, 255),  # 13: Terracotta Cliff
    (168, 242, 218, 255),  # 14: Glass Dome Aqua
    (255, 175, 228, 255),  # 15: Petal Glow Pink (emissive)
]
EMISSIVE_TILES = {7, 8, 15}


def generate_prop_atlas(albedo_path: Path, emission_path: Path):
    size = 1024
    tile = 256
    arr = np.zeros((size, size, 4), dtype=np.uint8)
    em = np.zeros((size, size, 4), dtype=np.uint8)
    em[:, :, 3] = 255

    for idx, col in enumerate(PALETTE):
        gx = (idx % 4) * tile
        gy = (idx // 4) * tile
        arr[gy:gy + tile, gx:gx + tile] = col
        if idx in EMISSIVE_TILES:
            em[gy:gy + tile, gx:gx + tile] = col

    save_rgba_png(arr, albedo_path, "props_albedo")
    save_rgba_png(em, emission_path, "props_emission")


def set_uv_tile(bm, faces, tile_idx):
    uv_layer = bm.loops.layers.uv.verify()
    u = ((tile_idx % 4) + 0.5) / 4.0
    v = 1.0 - (((tile_idx // 4) + 0.5) / 4.0)
    for f in faces:
        for loop in f.loops:
            loop[uv_layer].uv = (u, v)


def add_uv_sphere(bm, radius, scale_xyz, center, tile_idx, segs=24, rings=16):
    ret = bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=radius)
    verts = ret["verts"]
    for v in verts:
        v.co.x = v.co.x * scale_xyz[0] + center[0]
        v.co.y = v.co.y * scale_xyz[1] + center[1]
        v.co.z = v.co.z * scale_xyz[2] + center[2]
    vset = set(verts)
    faces = [f for f in bm.faces if all(v in vset for v in f.verts)]
    set_uv_tile(bm, faces, tile_idx)
    return verts


def add_cone_or_cyl(bm, r1, r2, depth, scale_xyz, center, rot_euler, tile_idx, segs=20):
    ret = bmesh.ops.create_cone(
        bm,
        cap_ends=True,
        cap_tris=False,
        segments=segs,
        radius1=r1,
        radius2=r2,
        depth=depth,
    )
    verts = ret["verts"]
    rmat = Euler(rot_euler, "XYZ").to_matrix()
    for v in verts:
        p = Vector((v.co.x * scale_xyz[0], v.co.y * scale_xyz[1], v.co.z * scale_xyz[2]))
        p = rmat @ p + Vector(center)
        v.co = p
    vset = set(verts)
    faces = [f for f in bm.faces if all(v in vset for v in f.verts)]
    set_uv_tile(bm, faces, tile_idx)
    return verts


def add_beveled_box(bm, size_xyz, center, rot_euler, tile_idx, bevel_width=0.06, bevel_segs=2):
    sub = bmesh.new()
    bmesh.ops.create_cube(sub, size=1.0)
    for v in sub.verts:
        v.co.x *= size_xyz[0]
        v.co.y *= size_xyz[1]
        v.co.z *= size_xyz[2]
    bmesh.ops.bevel(
        sub,
        geom=list(sub.verts) + list(sub.edges) + list(sub.faces),
        offset=bevel_width,
        offset_type="OFFSET",
        segments=bevel_segs,
        profile=0.5,
        affect="EDGES",
    )
    rmat = Euler(rot_euler, "XYZ").to_matrix()
    for v in sub.verts:
        v.co = rmat @ v.co + Vector(center)
    set_uv_tile(sub, sub.faces, tile_idx)
    me_tmp = bpy.data.meshes.new("_tmp_box")
    sub.to_mesh(me_tmp)
    sub.free()
    bm.from_mesh(me_tmp)
    bpy.data.meshes.remove(me_tmp)


def finalize_mesh_obj(name, bm, mat, smooth=True):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name + "Mesh")
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = smooth
    me.materials.append(mat)
    me.update()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    return obj


def export_fbx_objs(objs, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    out_path = PROP_DIR / filename
    bpy.ops.export_scene.fbx(
        filepath=str(out_path),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        bake_anim=False,
        axis_forward="-Y",
        axis_up="Z",
        path_mode="STRIP",
    )
    return out_path


def import_customuse_prop_if_valid(glb_name, target_obj_name, target_height_m, fallback_fn):
    glb_path = CUSTOMUSE_DIR / glb_name
    if glb_path.exists() and glb_path.stat().st_size > 10000:
        try:
            before = set(bpy.context.scene.objects)
            bpy.ops.import_scene.gltf(filepath=str(glb_path))
            new_objs = [o for o in bpy.context.scene.objects if o not in before]
            meshes = [o for o in new_objs if o.type == "MESH"]
            if meshes:
                bpy.ops.object.select_all(action="DESELECT")
                for m in meshes:
                    m.select_set(True)
                    bpy.context.view_layer.objects.active = m
                    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
                if len(meshes) > 1:
                    bpy.ops.object.join()
                obj = bpy.context.view_layer.objects.active
                obj.name = target_obj_name
                for o in new_objs:
                    if o != obj and o.name in bpy.data.objects:
                        bpy.data.objects.remove(o, do_unlink=True)
                bpy.ops.object.select_all(action="DESELECT")
                obj.select_set(True)
                bpy.context.view_layer.objects.active = obj
                bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
                me = obj.data
                # Customuse CR1 GLBs face +X; rotate -90 deg around Z so +X faces -Y (Unity forward)
                me.transform(Matrix.Rotation(D(-90.0), 4, "Z"))
                me.update()
                zs = [v.co.z for v in me.vertices]
                xs = [v.co.x for v in me.vertices]
                ys = [v.co.y for v in me.vertices]
                sc = target_height_m / max(1e-4, max(zs) - min(zs))
                cx = 0.5 * (max(xs) + min(xs))
                cy = 0.5 * (max(ys) + min(ys))
                min_z = min(zs)
                me.transform(Matrix.Translation((-cx * sc, -cy * sc, -min_z * sc)) @ Matrix.Scale(sc, 4))
                for p in me.polygons:
                    p.use_smooth = True
                me.update()
                # Extract Base Color texture specifically (not Emission/Roughness)
                tex_out = PROP_DIR / f"{target_obj_name}_Albedo.png"
                saved_base = False
                for mat in me.materials:
                    if mat and mat.use_nodes:
                        for node in mat.node_tree.nodes:
                            if node.type == "TEX_IMAGE" and node.image:
                                outs = [l.to_socket.name for out in node.outputs for l in out.links]
                                if "Base Color" in outs:
                                    node.image.filepath_raw = str(tex_out)
                                    node.image.file_format = "PNG"
                                    node.image.save()
                                    saved_base = True
                                    break
                    if saved_base:
                        break
                # Bind clean single-texture material for FBX export
                clean_mat = bpy.data.materials.new(name=f"{target_obj_name}Mat")
                clean_mat.use_nodes = True
                bsdf = next(n for n in clean_mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
                bsdf.inputs["Roughness"].default_value = 0.42
                if tex_out.exists():
                    tnode = clean_mat.node_tree.nodes.new("ShaderNodeTexImage")
                    tnode.image = bpy.data.images.load(str(tex_out))
                    clean_mat.node_tree.links.new(tnode.outputs["Color"], bsdf.inputs["Base Color"])
                me.materials.clear()
                me.materials.append(clean_mat)
                print(f"Integrated Customuse GLB for {target_obj_name} ({len(me.polygons)} polys)")
                return [obj], f"customuse_glb:{glb_name}"
        except Exception as e:
            print(f"Customuse GLB fallback for {target_obj_name}: {e}")
    return fallback_fn(), "blender_procedural"


def build_starberry(mat):
    bm = bmesh.new()
    # Plump heart-shaped berry body (tile 5: Starberry Magenta)
    ret = bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=0.32)
    for v in ret["verts"]:
        t = (v.co.z + 0.32) / 0.64  # 0 at bottom, 1 at top
        taper = 0.68 + 0.48 * math.sin(t * math.pi * 0.82)
        v.co.x *= taper
        v.co.y *= taper
        v.co.z = v.co.z * 1.08 + 0.34
    vset = set(ret["verts"])
    set_uv_tile(bm, [f for f in bm.faces if all(v in vset for v in f.verts)], 5)

    # Golden seed dimples around berry (tile 8: Radiant Star Yellow)
    for ring_z, r_xy, count in [(0.22, 0.25, 6), (0.38, 0.31, 8), (0.52, 0.27, 6)]:
        for k in range(count):
            ang = 2.0 * math.pi * (k + 0.5 * (ring_z > 0.3)) / count
            add_uv_sphere(
                bm, 1.0, (0.025, 0.025, 0.035),
                (math.cos(ang) * r_xy, math.sin(ang) * r_xy, ring_z),
                8, segs=10, rings=6
            )

    # Mint-green leafy crown (tile 6: Sprout Leaf Green)
    for k in range(5):
        ang = 2.0 * math.pi * k / 5.0
        add_cone_or_cyl(
            bm, 0.09, 0.01, 0.22, (1.0, 0.45, 1.0),
            (math.cos(ang) * 0.12, math.sin(ang) * 0.12, 0.68),
            (D(65.0) * math.sin(ang), -D(65.0) * math.cos(ang), ang),
            6, segs=12
        )
    # Stem
    add_cone_or_cyl(bm, 0.032, 0.022, 0.14, (1, 1, 1), (0, 0, 0.73), (D(10.0), 0, 0), 6, segs=10)
    return [finalize_mesh_obj("Starberry", bm, mat)]


def build_star_seed(mat):
    bm = bmesh.new()
    # 5-pointed plump 3D star body (tile 8: Radiant Star Yellow emissive)
    outer_r = 0.36
    inner_r = 0.17
    depth = 0.14
    ring_verts = []
    for k in range(10):
        ang = math.pi / 2.0 + k * (math.pi / 5.0)
        r = outer_r if k % 2 == 0 else inner_r
        ring_verts.append(bm.verts.new((math.cos(ang) * r, 0.0, 0.44 + math.sin(ang) * r)))
    front_c = bm.verts.new((0.0, -depth, 0.44))
    back_c = bm.verts.new((0.0, depth, 0.44))
    star_faces = []
    for k in range(10):
        v0 = ring_verts[k]
        v1 = ring_verts[(k + 1) % 10]
        star_faces.append(bm.faces.new((front_c, v1, v0)))
        star_faces.append(bm.faces.new((back_c, v0, v1)))
    set_uv_tile(bm, star_faces, 8)

    # Cute sprout swirl on top (tile 6: Sprout Leaf Green)
    add_cone_or_cyl(bm, 0.028, 0.016, 0.16, (1, 1, 1), (0.0, 0.0, 0.84), (0, D(15.0), 0), 6, segs=12)
    add_uv_sphere(bm, 1.0, (0.075, 0.035, 0.045), (0.08, 0.0, 0.90), 6, segs=14, rings=8)
    return [finalize_mesh_obj("StarSeed", bm, mat)]


def build_bouncy_shroom(mat):
    bm = bmesh.new()
    # Coiled springy base & stalk (tile 9: Warm Cream)
    add_cone_or_cyl(bm, 0.48, 0.36, 0.28, (1, 1, 1), (0, 0, 0.14), (0, 0, 0), 9, segs=24)
    add_cone_or_cyl(bm, 0.32, 0.38, 0.36, (1, 1, 1), (0, 0, 0.44), (0, 0, 0), 9, segs=24)
    # Plump dome cap (tile 4: Blossom Pink)
    add_uv_sphere(bm, 1.0, (0.85, 0.85, 0.42), (0, 0, 0.72), 4, segs=32, rings=18)
    # Glowing cyan/yellow spots on top of cap (tile 7: Jelly Cyan Glow)
    add_uv_sphere(bm, 1.0, (0.20, 0.20, 0.06), (0, 0, 1.12), 7, segs=16, rings=10)
    for k in range(6):
        ang = 2.0 * math.pi * k / 6.0
        add_uv_sphere(
            bm, 1.0, (0.15, 0.15, 0.07),
            (math.cos(ang) * 0.52, math.sin(ang) * 0.52, 0.92),
            7, segs=14, rings=8
        )
    return [finalize_mesh_obj("BouncyShroom", bm, mat)]


def build_push_ball(mat):
    bm = bmesh.new()
    # Rounded stone sphere centered at origin so Unity Rigidbody rolling rotates around its center!
    add_uv_sphere(bm, 0.75, (1.0, 1.0, 1.0), (0, 0, 0.0), 3, segs=28, rings=20)
    # Star-band inlay rings so rolling rotation is clearly visible!
    add_uv_sphere(bm, 1.0, (0.77, 0.77, 0.12), (0, 0, 0.0), 2, segs=24, rings=10)
    add_uv_sphere(bm, 1.0, (0.12, 0.77, 0.77), (0, 0, 0.0), 2, segs=24, rings=10)
    return [finalize_mesh_obj("PushBall", bm, mat)]


def build_domino_block(mat):
    bm = bmesh.new()
    # Standing domino block with base at z=0.0 and top at z=1.80m
    add_beveled_box(bm, (0.78, 0.36, 1.80), (0, 0, 0.90), (0, 0, 0), 1, bevel_width=0.06, bevel_segs=2)
    # Glowing star stripe across upper-middle
    add_beveled_box(bm, (0.81, 0.39, 0.22), (0, 0, 1.15), (0, 0, 0), 8, bevel_width=0.02, bevel_segs=1)
    return [finalize_mesh_obj("DominoBlock", bm, mat)]


def build_crystal_lever(mat):
    # Separate Base and Handle objects so Handle can rotate smoothly around its pivot when pulled!
    bm_base = bmesh.new()
    add_cone_or_cyl(bm_base, 0.42, 0.32, 0.26, (1, 1, 1), (0, 0, 0.13), (0, 0, 0), 10, segs=20)
    add_uv_sphere(bm_base, 1.0, (0.24, 0.18, 0.16), (0, 0, 0.28), 3, segs=16, rings=10)
    base_obj = finalize_mesh_obj("CrystalLever", bm_base, mat)

    bm_handle = bmesh.new()
    add_cone_or_cyl(bm_handle, 0.045, 0.035, 0.65, (1, 1, 1), (0, 0, 0.32), (0, 0, 0), 9, segs=14)
    add_uv_sphere(bm_handle, 1.0, (0.12, 0.12, 0.16), (0, 0, 0.68), 7, segs=16, rings=12)
    handle_obj = finalize_mesh_obj("LeverHandle", bm_handle, mat)
    handle_obj.location = Vector((0, 0, 0.28))
    handle_obj.rotation_euler = Euler((D(-32.0), 0, 0), "XYZ")
    handle_obj.parent = base_obj
    return [base_obj, handle_obj]


def build_unfolding_bridge(mat):
    root = bpy.data.objects.new("UnfoldingBridge", None)
    bpy.context.collection.objects.link(root)
    objs = [root]
    for i in range(5):
        bm = bmesh.new()
        tile = 15 if i % 2 == 0 else 7
        add_beveled_box(bm, (2.2, 1.45, 0.22), (0, 0, 0), (0, 0, 0), tile, bevel_width=0.06, bevel_segs=2)
        # Side petal rails
        add_uv_sphere(bm, 1.0, (0.18, 0.65, 0.16), (-1.05, 0, 0.12), 4, segs=14, rings=8)
        add_uv_sphere(bm, 1.0, (0.18, 0.65, 0.16), (1.05, 0, 0.12), 4, segs=14, rings=8)
        seg_obj = finalize_mesh_obj(f"BridgeSeg_{i}", bm, mat)
        seg_obj.location = Vector((0, -i * 1.5, 0))
        seg_obj.parent = root
        objs.append(seg_obj)
    return objs


def build_bloom_plant(mat):
    # Multi-part plant so its 6 petals physically unfold into a reward platform when fed a Starberry!
    bm_body = bmesh.new()
    # Base leaves
    for k in range(5):
        ang = 2.0 * math.pi * k / 5.0
        add_uv_sphere(
            bm_body, 1.0, (0.55, 0.28, 0.08),
            (math.cos(ang) * 0.55, math.sin(ang) * 0.55, 0.07),
            6, segs=16, rings=10
        )
    # Plump bulb body
    add_uv_sphere(bm_body, 1.0, (0.68, 0.68, 0.62), (0, 0, 0.58), 9, segs=28, rings=18)
    # Happy eyes and open mouth on -Y front
    for side in (-1.0, 1.0):
        add_uv_sphere(bm_body, 1.0, (0.08, 0.05, 0.09), (side * 0.22, -0.62, 0.68), 11, segs=14, rings=10)
        add_uv_sphere(bm_body, 1.0, (0.025, 0.025, 0.025), (side * 0.20, -0.66, 0.71), 12, segs=10, rings=6)
        add_uv_sphere(bm_body, 1.0, (0.09, 0.04, 0.05), (side * 0.34, -0.56, 0.56), 4, segs=12, rings=8)
    add_uv_sphere(bm_body, 1.0, (0.22, 0.12, 0.14), (0, -0.62, 0.50), 5, segs=16, rings=10)
    # Glowing nectar pad on top
    add_cone_or_cyl(bm_body, 0.52, 0.58, 0.16, (1, 1, 1), (0, 0, 1.15), (0, 0, 0), 8, segs=24)
    root_obj = finalize_mesh_obj("BloomPlant", bm_body, mat)

    objs = [root_obj]
    for k in range(6):
        ang = 2.0 * math.pi * k / 6.0
        bm_p = bmesh.new()
        add_uv_sphere(bm_p, 1.0, (0.36, 0.58, 0.09), (0, -0.52, 0.0), 15, segs=18, rings=10)
        p_obj = finalize_mesh_obj(f"BloomPetal_{k}", bm_p, mat)
        p_obj.location = Vector((math.cos(ang) * 0.42, math.sin(ang) * 0.42, 1.16))
        p_obj.rotation_euler = Euler((D(55.0), 0, ang + math.pi / 2.0), "XYZ")
        p_obj.parent = root_obj
        objs.append(p_obj)
    return objs


def build_bloop_creature(mat):
    bm = bmesh.new()
    # Chubby lavender-pink body
    add_uv_sphere(bm, 1.0, (0.38, 0.40, 0.35), (0, 0, 0.42), 4, segs=26, rings=18)
    # 4 stubby feet touching z=0
    for sx in (-0.20, 0.20):
        for sy in (-0.18, 0.18):
            add_uv_sphere(bm, 1.0, (0.09, 0.11, 0.10), (sx, sy, 0.09), 3, segs=14, rings=10)
    # Big glossy eyes & happy smile on -Y front
    for side in (-1.0, 1.0):
        add_uv_sphere(bm, 1.0, (0.085, 0.045, 0.095), (side * 0.14, -0.36, 0.46), 11, segs=16, rings=12)
        add_uv_sphere(bm, 1.0, (0.030, 0.025, 0.030), (side * 0.12, -0.40, 0.49), 12, segs=10, rings=8)
        add_uv_sphere(bm, 1.0, (0.065, 0.03, 0.04), (side * 0.24, -0.33, 0.38), 5, segs=12, rings=8)
    # Glowing leaf sprout on top
    add_cone_or_cyl(bm, 0.025, 0.018, 0.18, (1, 1, 1), (0, 0, 0.82), (0, 0, 0), 6, segs=10)
    add_uv_sphere(bm, 1.0, (0.14, 0.06, 0.04), (-0.12, 0, 0.92), 7, segs=14, rings=8)
    add_uv_sphere(bm, 1.0, (0.14, 0.06, 0.04), (0.12, 0, 0.92), 7, segs=14, rings=8)
    # Cute round tail on +Y back
    add_uv_sphere(bm, 1.0, (0.09, 0.11, 0.09), (0, 0.42, 0.36), 4, segs=12, rings=8)
    return [finalize_mesh_obj("BloopCreature", bm, mat)]


def build_starhopper_ship(mat):
    bm = bmesh.new()
    # Rounded mint-teal main cabin hull
    add_uv_sphere(bm, 1.0, (1.85, 1.65, 1.15), (0, 0, 1.25), 0, segs=32, rings=20)
    # Lower coral trim ring & 4 landing pods
    add_cone_or_cyl(bm, 1.75, 1.85, 0.38, (1, 0.9, 1), (0, 0, 0.42), (0, 0, 0), 1, segs=28)
    for sx in (-1.25, 1.25):
        for sy in (-1.05, 1.05):
            add_uv_sphere(bm, 1.0, (0.34, 0.42, 0.24), (sx, sy, 0.22), 1, segs=16, rings=10)
    # Side thruster pods (Mustard gold + glowing cyan exhaust)
    for side in (-1.0, 1.0):
        add_cone_or_cyl(bm, 0.58, 0.52, 1.15, (1, 1, 1), (side * 1.95, 0.1, 1.10), (D(90.0), 0, 0), 2, segs=20)
        add_uv_sphere(bm, 1.0, (0.44, 0.15, 0.44), (side * 1.95, -0.48, 1.10), 7, segs=16, rings=10)
    # Upper glass cockpit dome
    add_uv_sphere(bm, 1.0, (1.10, 1.00, 0.85), (0, 0, 2.15), 14, segs=28, rings=16)
    add_cone_or_cyl(bm, 1.18, 1.12, 0.18, (1, 0.92, 1), (0, 0, 1.95), (0, 0, 0), 1, segs=24)
    # Front porthole door & step ramp on -Y
    add_uv_sphere(bm, 1.0, (0.52, 0.22, 0.62), (0, -1.55, 0.95), 2, segs=20, rings=12)
    add_uv_sphere(bm, 1.0, (0.36, 0.18, 0.44), (0, -1.66, 0.95), 8, segs=18, rings=10)
    add_beveled_box(bm, (0.85, 0.65, 0.18), (0, -1.75, 0.18), (D(18.0), 0, 0), 2, bevel_width=0.03, bevel_segs=1)
    return [finalize_mesh_obj("StarhopperShip", bm, mat)]


def build_alien_pod_home(mat):
    bm = bmesh.new()
    # Warm cream/lavender pod cottage
    add_uv_sphere(bm, 1.0, (1.65, 1.65, 1.55), (0, 0, 1.35), 9, segs=28, rings=18)
    # Pastel roof cap + leaf sprout
    add_uv_sphere(bm, 1.0, (1.35, 1.35, 0.55), (0, 0, 2.55), 3, segs=24, rings=12)
    add_cone_or_cyl(bm, 0.08, 0.05, 0.45, (1, 1, 1), (0, 0, 3.10), (0, 0, 0), 6, segs=12)
    add_uv_sphere(bm, 1.0, (0.32, 0.14, 0.08), (-0.24, 0, 3.30), 6, segs=14, rings=8)
    add_uv_sphere(bm, 1.0, (0.32, 0.14, 0.08), (0.24, 0, 3.30), 6, segs=14, rings=8)
    # Cozy glowing arched doorway & round window
    add_uv_sphere(bm, 1.0, (0.55, 0.25, 0.72), (0, -1.52, 0.78), 1, segs=18, rings=12)
    add_uv_sphere(bm, 1.0, (0.42, 0.20, 0.58), (0, -1.60, 0.78), 8, segs=16, rings=10)
    for side in (-1.0, 1.0):
        add_uv_sphere(bm, 1.0, (0.32, 0.22, 0.32), (side * 1.15, -1.10, 1.45), 8, segs=14, rings=10)
    return [finalize_mesh_obj("AlienPodHome", bm, mat)]


def build_pastel_rock_platform(mat):
    bm = bmesh.new()
    # Soft rounded terraced cliff block
    add_beveled_box(bm, (3.8, 3.8, 1.6), (0, 0, 0.8), (0, 0, 0), 3, bevel_width=0.32, bevel_segs=3)
    # Mint-turquoise grass top cap
    add_beveled_box(bm, (3.95, 3.95, 0.36), (0, 0, 1.68), (0, 0, 0), 0, bevel_width=0.14, bevel_segs=2)
    return [finalize_mesh_obj("PastelRockPlatform", bm, mat)]


def build_glowing_flora_tuft(mat):
    bm = bmesh.new()
    # 5 curved grass blades + 2 glowing bulb stalks rooted at z=0 so bending pivots from the ground!
    for k in range(5):
        ang = 2.0 * math.pi * k / 5.0
        rx = math.sin(ang) * D(22.0)
        ry = math.cos(ang) * D(22.0)
        add_cone_or_cyl(
            bm, 0.08, 0.01, 0.65, (1.0, 0.45, 1.0),
            (math.cos(ang) * 0.14, math.sin(ang) * 0.14, 0.32),
            (rx, ry, ang), 0, segs=10
        )
    for side, h in [(-0.12, 0.78), (0.14, 0.66)]:
        add_cone_or_cyl(bm, 0.025, 0.018, h, (1, 1, 1), (side, 0, h * 0.5), (0, side * D(45.0), 0), 6, segs=10)
        add_uv_sphere(bm, 1.0, (0.085, 0.085, 0.095), (side * 1.35, 0, h + 0.04), 7 if side < 0 else 15, segs=12, rings=8)
    return [finalize_mesh_obj("GlowingFloraTuft", bm, mat)]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    albedo_path = PROP_DIR / "Props_Albedo.png"
    emission_path = PROP_DIR / "Props_Emission.png"
    generate_prop_atlas(albedo_path, emission_path)

    mat = bpy.data.materials.new(name="PocketPlanetPropsMat")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 0.42
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(str(albedo_path))
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

    etex = nt.nodes.new("ShaderNodeTexImage")
    etex.image = bpy.data.images.load(str(emission_path))
    if "Emission Color" in bsdf.inputs:
        nt.links.new(etex.outputs["Color"], bsdf.inputs["Emission Color"])
    if "Emission Strength" in bsdf.inputs:
        bsdf.inputs["Emission Strength"].default_value = 1.6

    manifest = {}

    def register_and_export(key, objs, fbx_name, source_tag="blender_procedural"):
        p = export_fbx_objs(objs, fbx_name)
        tris = sum(sum(len(poly.vertices) - 2 for poly in o.data.polygons) for o in objs if o.type == "MESH")
        manifest[key] = {"fbx": fbx_name, "source": source_tag, "triangles": tris, "parts": [o.name for o in objs]}
        print(f"Exported {fbx_name} ({tris} tris, source={source_tag})")

    register_and_export("Starberry", build_starberry(mat), "Starberry.fbx")
    register_and_export("StarSeed", build_star_seed(mat), "StarSeed.fbx")
    register_and_export("BouncyShroom", build_bouncy_shroom(mat), "BouncyShroom.fbx")
    register_and_export("PushBall", build_push_ball(mat), "PushBall.fbx")
    register_and_export("DominoBlock", build_domino_block(mat), "DominoBlock.fbx")
    register_and_export("CrystalLever", build_crystal_lever(mat), "CrystalLever.fbx")
    register_and_export("UnfoldingBridge", build_unfolding_bridge(mat), "UnfoldingBridge.fbx")
    register_and_export("AlienPodHome", build_alien_pod_home(mat), "AlienPodHome.fbx")
    register_and_export("PastelRockPlatform", build_pastel_rock_platform(mat), "PastelRockPlatform.fbx")
    register_and_export("GlowingFloraTuft", build_glowing_flora_tuft(mat), "GlowingFloraTuft.fbx")

    # BloomPlant ALWAYS keeps its multi-part animated petals, and we also export Customuse meshes when available
    register_and_export("BloomPlant", build_bloom_plant(mat), "BloomPlant.fbx")

    bloop_objs, bloop_src = import_customuse_prop_if_valid(
        "bloop_3d.glb", "BloopCreature", 0.88, lambda: build_bloop_creature(mat)
    )
    register_and_export("BloopCreature", bloop_objs, "BloopCreature.fbx", bloop_src)

    ship_objs, ship_src = import_customuse_prop_if_valid(
        "spaceship_3d.glb", "StarhopperShip", 2.85, lambda: build_starhopper_ship(mat)
    )
    register_and_export("StarhopperShip", ship_objs, "StarhopperShip.fbx", ship_src)

    # Also import bloom_plant_3d.glb as Decorative Alien Flora Landmark if present!
    if (CUSTOMUSE_DIR / "bloom_plant_3d.glb").exists():
        bp_objs, bp_src = import_customuse_prop_if_valid(
            "bloom_plant_3d.glb", "CustomuseFloraStatue", 1.65, lambda: build_glowing_flora_tuft(mat)
        )
        register_and_export("CustomuseFloraStatue", bp_objs, "CustomuseFloraStatue.fbx", bp_src)

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_DIR / "PocketPlanet_WorldAssets.blend"))
    (PROP_DIR / "PropsManifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print("WORLD_ASSETS_EXPORT_OK", json.dumps(manifest))


if __name__ == "__main__":
    main()
