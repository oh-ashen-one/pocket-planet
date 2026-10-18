# Asset Credits, Provenance & Licensing

This document details the provenance, generation pipeline, and redistribution rights for every code, 3D, texture, audio, and concept art asset in **Pocket Planet**.

---

## 1. Source Code & Editor Automation (`Assets/PocketPlanet/Scripts/`, `Assets/PocketPlanet/Editor/`, `tools/*.py`)
- **Author**: Created specifically for the Pocket Planet project (2026).
- **License**: **MIT License** (see [`LICENSE`](LICENSE)).

---

## 2. Customuse MCP Generated 3D Assets (`tools/customuse_sources/*.glb` & Derived FBXs)
Generated via the **Customuse MCP** workflow API (`workflowId`: `cb6e7680-32a8-4b4e-878e-a18ed2a39e8d`, model: `cr1` image-to-3D with 2048×2048 PBR texture generation, 70 CustoCoins per model, 280 CustoCoins total) from original project concept art created for this repository:

| Asset File | In-Game Usage | Triangles | Customuse Node ID | Redistribution & License |
| :--- | :--- | :---: | :--- | :--- |
| `tools/customuse_sources/pip_3d.glb` | Hero character **Pip** mesh & 2K albedo (`PipBody.fbx`, `Pip_Albedo.png`) | 14,352 | `cr1-pip-hero` | Owned by repository creator under Customuse commercial/creator terms; redistributed with permission in this open-source repository. |
| `tools/customuse_sources/bloop_3d.glb` | Friendly alien creature **Bloop** (`BloopCreature.fbx`, `BloopCreature_Albedo.png`) | 9,594 | `cr1-bloop-pet` | Owned by repository creator under Customuse commercial/creator terms; redistributed with permission in this open-source repository. |
| `tools/customuse_sources/spaceship_3d.glb` | Explorer ship **SS Starhopper** (`StarhopperShip.fbx`, `StarhopperShip_Albedo.png`) | 13,526 | `cr1-starhopper-ship` | Owned by repository creator under Customuse commercial/creator terms; redistributed with permission in this open-source repository. |
| `tools/customuse_sources/bloom_plant_3d.glb` | Bioluminescent garden landmark (`CustomuseFloraStatue.fbx`, `CustomuseFloraStatue_Albedo.png`) | 11,343 | `cr1-bloom-plant` | Owned by repository creator under Customuse commercial/creator terms; redistributed with permission in this open-source repository. |

---

## 3. Blender 5.2 Authored Rig, Animations & Modular Interactive Props (`tools/blender_sources/*.blend`, `Assets/PocketPlanet/Art/Props/*.fbx`)
Authored and exported reproducibly via [`tools/blender_rig_hero.py`](tools/blender_rig_hero.py) and [`tools/blender_build_world_assets.py`](tools/blender_build_world_assets.py):

- **Blender Source Files**:
  - [`tools/blender_sources/Pip_Hero.blend`](tools/blender_sources/Pip_Hero.blend): Cleaned Customuse `Pip` mesh bound to the 22-bone `PipRig` armature with 12 authored skeletal Actions (`Idle`, `Walk`, `Run`, `Turn`, `Jump`, `Fall`, `Landing`, `Pickup`, `Carry`, `Throw`, `Interact`, `Celebrate`).
  - [`tools/blender_sources/PocketPlanet_WorldAssets.blend`](tools/blender_sources/PocketPlanet_WorldAssets.blend): Modular interactive props (`Starberry`, `StarSeed`, `BouncyShroom`, `PushBall`, `DominoBlock`, `CrystalLever`, `UnfoldingBridge`, `AlienPodHome`, `PastelRockPlatform`, `GlowingFloraTuft`, `BloomPlant`) and Customuse prop imports.
- **License**: **CC0 1.0 / MIT** (original procedural geometry, rigging, skin weights, UV atlases, and keyframe animations authored for this repository).

---

## 4. Original Synthesized Audio (`Assets/PocketPlanet/Resources/Audio/*.wav`)
Generated reproducibly via [`tools/generate_audio_assets.py`](tools/generate_audio_assets.py) using pure mathematical synthesis (sine/triangle/marimba harmonics, pitch envelopes, and filtered noise bursts; zero external sample libraries):
- `bgm_pocket_planet.wav` (16.0s seamless space-garden loop)
- `step_grass_01.wav` .. `04.wav`, `step_water_01.wav` .. `03.wav`, `water_splash.wav`
- `jump.wav`, `land.wav`, `pickup_fruit.wav`, `throw_fruit.wav`, `plant_eat_bloom.wav`, `mushroom_boing.wav`, `boulder_roll_hit.wav`, `domino_topple.wav`, `lever_pull.wav`, `bridge_unfold.wav`, `alien_pet_chirp.wav`, `flora_rustle.wav`, `star_seed_collect.wav`, `ship_repair_fanfare.wav`
- **License**: **CC0 1.0 Public Domain / MIT**.

---

## 5. Pre-Production Concept Art (`docs/concepts/*.png`)
Generated via built-in AI image generation (`image-generator`) solely as pre-production visual direction and 3D reconstruction input references:
- `docs/concepts/hero_pip_front_a_pose.png`
- `docs/concepts/hero_pip_turnaround.png`
- `docs/concepts/environment_concept.png`
- `docs/concepts/creatures_and_props_sheet.png`
- **License**: Redistributed as part of the project documentation under the repository license.
