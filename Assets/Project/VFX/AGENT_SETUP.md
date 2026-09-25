# VFX agent handoff and setup

## Working context

- Unity project: Jurrac project, Unity 6000.0.78f1.
- Intended worktree and branch: E:\Project-Unity\Jurrac_project-codex3 on agent/codex3.
- VFX assets are authored as editable Unity Particle System prefabs. The current set is a starter foundation for the project's VFX artist to polish.
- Keep follow-up work on this worktree/branch. Do not edit or save gameplay scenes unless the task explicitly requires a scene change.

## What is implemented

Eight gameplay effects are stored under Assets/Project/VFX/Prefabs:

| Prefab | Runtime trigger | Layers |
| --- | --- | --- |
| FX_Cannon_MuzzleFlash | Cannon fires | Core flash, expanding ring, hot sparks, soot puffs |
| FX_Cannon_ChargeLoop | Player pulls the cannon | Orbiting motes and arc filaments; whole effect scales with pull ratio |
| FX_Bullet_Trail | Bullet is spawned | Aether glow and ember filaments follow the projectile |
| FX_Bullet_Impact | Bullet collides with a solid surface | Contact flash, shock ring, sparks, dust |
| FX_Bullet_Bounce | Bullet collides with a BounceSurface | Cyan flash and ring, directional shard sparks |
| FX_Target_VictoryBurst | Bullet reaches the target | Prismatic core, double halo, starburst, rising shards |
| FX_Portal_Enter | Bullet begins portal transfer | Cyan/blue core, hoop, arc sparks, floating motes |
| FX_Portal_Exit | Bullet exits the paired portal | Brass/cyan core, hoop, arc sparks, floating motes |

Shared supporting assets:

- Assets/Project/VFX/Materials: MAT_VFX_GlowSoft, MAT_VFX_RingPulse, MAT_VFX_Streak.
- Assets/Project/VFX/Textures: T_VFX_GlowSoft, T_VFX_RingPulse, T_VFX_Streak.
- Assets/Resources/VFX/GameVfxLibrary.asset: runtime prefab catalog loaded from the Resources key VFX/GameVfxLibrary.
- Assets/Project/VFX/Editor/BuildGameVfxPrefabs.cs: editor builder for the starter set.
- Assets/Project/Scripts/VFX: runtime lookup, cleanup, and projectile trail lifecycle helpers.

All particle systems are configured to use unscaled time. One-shot prefabs carry VfxAutoCleanup, which destroys the root after all systems finish or after the eight-second safety limit. The projectile trail uses world simulation space so already-emitted particles remain behind the bullet. When the bullet is destroyed, ProjectileTrailAttachment detaches the trail and stops emission so it can fade out and clean itself up.

## Runtime wiring

- Assets/Project/Scripts/CannonShooter.cs attaches FX_Cannon_ChargeLoop while CannonController.IsPulling is true, updates its scale using PullRatio, and stops emission when pulling ends or the shooter is disabled. On firing it attaches FX_Bullet_Trail to the new bullet and plays FX_Cannon_MuzzleFlash at the fire point.
- Assets/Project/Scripts/BulletImpactRouter.cs plays FX_Bullet_Bounce for a collision whose collider has BounceSurface; other solid collisions play FX_Bullet_Impact. This VFX path is for collision callbacks. Trigger-hit mechanics are dispatched separately.
- Assets/Project/Scripts/Target.cs plays FX_Target_VictoryBurst when the target accepts a bullet hit. The target pauses gameplay time afterward; unscaled particle simulation lets the burst continue.
- Assets/Project/Scripts/BulletPortalPair.cs plays FX_Portal_Enter at the source when transfer starts and FX_Portal_Exit at the destination when the bullet is released.

Assets/Project/Scripts/VFX/GameVfx.cs resolves the catalog once through Resources.Load, instantiates one-shot effects oriented toward the supplied direction, and parents attached effects to the requested transform. GameVfxLibrary is the single place to change which prefab each runtime slot uses. Keep the catalog asset path and its references valid when moving or renaming assets.

## Editing and previewing

1. Open the worktree at E:\Project-Unity\Jurrac_project-codex3 with Unity 6000.0.78f1. Work on branch agent/codex3.
2. Let Unity import the assets, then open a prefab from Assets/Project/VFX/Prefabs in Prefab Mode. Each effect has named child systems such as PS_Impact_Sparks or PS_Portal_EnergyHoop; tune those children independently in the Particle System inspector.
3. Adjust shared appearance through the three VFX materials and their alpha textures. Preserve the particle renderer/material references unless intentionally replacing the shared look.
4. Preserve the prefab names and GameVfxLibrary slots while polishing where possible; gameplay scripts refer to catalog slots rather than directly finding prefab names.
5. For an in-game visual check, use the existing gameplay flow for cannon charge/fire, a solid collision, a BounceSurface, target hit, and portal transfer. Avoid saving scene changes as part of this check.
6. Review prefab and material overrides before committing. Unity can serialize unrelated defaults when an inspector value is touched, so inspect the diff and retain only intentional tuning.

The Unity menu DreamForge > VFX > Rebuild Starter VFX Prefabs runs BuildGameVfxPrefabs.Build. It recreates the three textures and materials, rebuilds all eight prefabs, and assigns the catalog slots. Treat this as a destructive reset of the starter look: it can overwrite artist edits in those generated assets. Commit or back up intended polish before running it. Edit prefabs directly for normal VFX iteration; edit the builder only when the generated baseline itself should change.

## Current handoff notes

The initial starter implementation was committed as 042730b (Add editable gameplay VFX starter set). The current pending prefab edits in this worktree affect FX_Bullet_Bounce, FX_Cannon_ChargeLoop, FX_Portal_Enter, and FX_Target_VictoryBurst. No scene asset is part of this handoff change. Keep local editor configuration such as .codex out of Git.