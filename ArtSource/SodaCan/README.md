# Soda can knockdown target

Editable Blender sources and Unity-ready exports for a simple can-shooting target.

- Current assembly source: `CanTargetStack_12Cans.blend`.
- Separate can export: `Assets/Models/Targets/SodaCan/SodaCan_330ml.fbx` with `SodaCan_BaseColor.png` beside it.
- Separate stand export: `Assets/Models/Targets/CanTargetStand/WoodCanTargetStand.fbx`.
- Wood texture: `Assets/Models/Targets/CanTargetStand/WoodStand_BaseColor.png`, one 1024 x 1024 base-color atlas beside the stand FBX.
- Assembly preview: `CanTargetStack_Preview.png`.
- Stand builder: `create_can_target_stack.py`; wood texture generator: `generate_wood_texture.py`.

Import the can and stand FBXs as two separate assets; both FBXs reference a PNG in their own folder. The Blender source keeps the full 12-can layout for editing. `CanTargetPuzzleStand.fbx` and `CanTargetStand_Preview.png`, if present from the earlier prototype, show the superseded three-slot design.

The stand is a single 560 x 160 x 270 mm mesh with a horizontal plank and two supports underneath. Its wood material uses one texture. The plank top is at 270 mm. The assembly has six columns of cans, two cans high, for 12 separate can objects. Each can is about 66 mm across and 115 mm high; all 12 share the same 624-triangle mesh and one material/1024 x 1024 color atlas.

In Unity, place multiple instances of the can FBX on the stand FBX. Keep the stand static and add suitable box colliders. Add a Rigidbody and capsule collider to each can instance to let it fall when hit. The FBX files reference external PNG sidecars so Unity can import each texture alongside its model.

The original standalone can remains at `Assets/Models/Targets/SodaCan/SodaCan_330ml.fbx`, with its geometry source in `SodaCan_330ml.blend`.
