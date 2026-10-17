# Asset Licenses & Provenance

All newly authored C# source code, Unity editor scripts, Python Blender scripts, and procedural audio synthesis scripts in this repository are licensed under the **MIT License** (see [LICENSE](LICENSE)).

## Art, 3D Models, Concept References & Audio

Except as noted below for third-party provider terms, original project-created 3D meshes, rigs, animations, procedural textures, concept illustrations, and synthesized sound effects are licensed by the project owner under **Creative Commons Attribution 4.0 International (CC BY 4.0)**.

| Asset Path | Description | Origin & Tooling | License / Terms |
| --- | --- | --- | --- |
| `docs/concepts/*.png` | Concept art & reference sheets (`hero_pip_front_a_pose.png`, `hero_pip_turnaround.png`, `environment_concept.png`, `creatures_and_props_sheet.png`) | Generated with built-in AI image generation from original prompts; labeled honestly as concept art, never as gameplay screenshots. | CC BY 4.0 (owner output rights per provider terms) |
| `Assets/PocketPlanet/Resources/Characters/*` & `tools/blender_sources/*.blend` | Hero character ("Pip"), friendly alien ("Bloop"), skeleton rig, skin weights, and 12 authored animation clips | Customuse MCP 3D generation (pending owner credit budget approval) + local Blender 5.2 cleanup, UV/material correction, rigging, skinning, and animation authoring (`tools/blender_rig_hero.py`). | Customuse Terms §4 (user retains content rights for lawful personal/commercial use) + CC BY 4.0 for local Blender rig & animation clips |
| `Assets/PocketPlanet/Resources/Props/*` | Modular garden planet environment assets, spaceship, star seeds, starberries, bloom-plant, bouncy mushrooms, boulder, domino blocks, lever & unfolding bridge | Customuse MCP (for distinctive hero props after budget approval) + modular Blender 5.2 procedural modeling (`tools/blender_build_world_assets.py`). | CC BY 4.0 |
| `Assets/PocketPlanet/Resources/Audio/*.wav` | Original synthesized SFX & ambient music loop (footsteps, jump, land, mushroom boing, fruit pickup/throw, plant bloom, lever & bridge unfold, alien chirp, water splash, plant rustle, star seed fanfare, ship repair celebration) | Locally synthesized via `tools/generate_audio.py` (deterministic Python/NumPy wave synthesis; zero third-party samples). | CC BY 4.0 |
