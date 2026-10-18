#!/usr/bin/env python3
"""
Blender 5.2 LTS processor for the 14 Tripo 3D Studio GLB models + dense grass patches.
Decimates 1.9M-triangle raw H3.1 meshes down to ~6,500 game-ready triangles while
preserving UVs, normal smoothness, and 1024x1024 Base Color textures.
"""
import sys
import math
import json
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

ROOT = Path("/Users/midir/pocket-planet")
TRIPO_DIR = ROOT / "tools/tripo_sources"
PROP_DIR = ROOT / "Assets/PocketPlanet/Art/Props"
PROP_DIR.mkdir(parents=True, exist_ok=True)
D = math.radians

TRIPO_SPECS = [
    ("tripo_lush_tree.glb", "TripoLushTree", 6.8, 6500),
    ("tripo_alien_palm_tree.glb", "TripoAlienPalmTree", 5.8, 6000),
    ("tripo_flower_bush_cluster.glb", "TripoFlowerBush", 1.45, 5000),
    ("tripo_flying_saucer.glb", "TripoFlyingSaucer", 3.1, 7500),
    ("tripo_npc_astra.glb", "TripoNpcAstra", 1.42, 7000),
    ("tripo_solaris_core.glb", "TripoSolarisCore", 2.2, 6500),
    ("tripo_space_asteroid.glb", "TripoSpaceAsteroid", 4.5, 4500),
    ("tripo_deadwood_tree.glb", "TripoDeadwoodTree", 7.2, 6500),
    ("tripo_obsidian_ruin.glb", "TripoObsidianRuin", 5.6, 6500),
    ("tripo_npc_nyx.glb", "TripoNpcNyx", 1.52, 7000),
    ("tripo_shrine_lantern.glb", "TripoShrineLantern", 3.4, 7000),
    ("tripo_crashed_saucer.glb", "TripoCrashedSaucer", 3.8, 6500),
    ("tripo_obsidian_crystal_cluster.glb", "TripoObsidianCrystal", 2.8, 5000),
    ("tripo_spooky_bog_mushroom.glb", "TripoSpookyMushroom", 2.2, 5500),
]


def process_tripo_glb(glb_filename: str, target_name: str, target_height: float, target_polys: int):
    glb_path = TRIPO_DIR / glb_filename
    if not glb_path.exists():
        print(f"MISSING {glb_path}")
        return

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(glb_path))

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        print(f"NO MESH IN {glb_path}")
        return

    bpy.ops.object.select_all(action="DESELECT")
    for m in meshes:
        m.select_set(True)
        bpy.context.view_layer.objects.active = m
        bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    if len(meshes) > 1:
        bpy.ops.object.join()

    obj = bpy.context.view_layer.objects.active
    obj.name = target_name
    for o in list(bpy.data.objects):
        if o != obj:
            bpy.data.objects.remove(o, do_unlink=True)

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Save Base Color texture at 1024x1024 before decimation
    tex_out = PROP_DIR / f"{target_name}_Albedo.png"
    saved_tex = False
    for mat in obj.data.materials:
        if mat and mat.use_nodes:
            for node in mat.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image:
                    outs = [l.to_socket.name for out in node.outputs for l in out.links]
                    if "Base Color" in outs or not saved_tex:
                        img = node.image
                        if img.size[0] > 1024 or img.size[1] > 1024:
                            img.scale(1024, 1024)
                        img.filepath_raw = str(tex_out)
                        img.file_format = "PNG"
                        img.save()
                        saved_tex = True
                        if "Base Color" in outs:
                            break
        if saved_tex:
            break

    raw_polys = len(obj.data.polygons)
    if raw_polys > target_polys:
        mod = obj.modifiers.new("TripoDecimate", "DECIMATE")
        mod.ratio = float(target_polys) / float(raw_polys)
        bpy.ops.object.modifier_apply(modifier=mod.name)

    me = obj.data
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    span_z = max(1e-4, max(zs) - min(zs))
    sc = target_height / span_z
    cx = 0.5 * (max(xs) + min(xs))
    cy = 0.5 * (max(ys) + min(ys))
    min_z = min(zs)
    me.transform(Matrix.Translation((-cx * sc, -cy * sc, -min_z * sc)) @ Matrix.Scale(sc, 4))
    for p in me.polygons:
        p.use_smooth = True
    me.update()

    clean_mat = bpy.data.materials.new(name=f"{target_name}Mat")
    clean_mat.use_nodes = True
    bsdf = next(n for n in clean_mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 0.46
    if tex_out.exists():
        tnode = clean_mat.node_tree.nodes.new("ShaderNodeTexImage")
        tnode.image = bpy.data.images.load(str(tex_out))
        clean_mat.node_tree.links.new(tnode.outputs["Color"], bsdf.inputs["Base Color"])
    me.materials.clear()
    me.materials.append(clean_mat)

    fbx_out = PROP_DIR / f"{target_name}.fbx"
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=str(fbx_out),
        use_selection=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Y",
        axis_up="Z",
        path_mode="STRIP",
    )
    print(f"EXPORTED {fbx_out.name}: {raw_polys} -> {len(me.polygons)} polys (height={target_height}m)")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    chunk_idx = int(argv[0]) if len(argv) >= 1 else 0
    num_chunks = int(argv[1]) if len(argv) >= 2 else 1

    for i, spec in enumerate(TRIPO_SPECS):
        if i % num_chunks == chunk_idx:
            process_tripo_glb(*spec)


if __name__ == "__main__":
    main()
