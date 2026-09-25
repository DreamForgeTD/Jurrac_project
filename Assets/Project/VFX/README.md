# Starter Gameplay VFX

Small, self-cleaning particle prefabs for the cannon prototype. The palette uses hot brass for firing and impact, cool cyan for ricochets and portals, and a mint/gold burst for the target win. Particle systems use unscaled time so the target celebration still plays when gameplay pauses.

## Folder layout

- `Prefabs/` — editable, composable gameplay effects (`FX_*`).
- `Materials/` — soft glow, expanding ring, and stretched spark materials (`MAT_VFX_*`).
- `Textures/` — lightweight alpha textures used by the particle materials (`T_VFX_*`).
- `../Scripts/VFX/` — runtime lookup, attachment, and cleanup helpers.
- `Assets/Resources/VFX/GameVfxLibrary.asset` — central prefab catalog loaded at runtime.

## Effects

| Prefab | Trigger | Included layers |
| --- | --- | --- |
| `FX_Cannon_MuzzleFlash` | Cannon fires | White-hot core, expanding brass ring, hot sparks, soft soot puffs |
| `FX_Cannon_ChargeLoop` | Player pulls the cannon | Orbiting cyan motes and fine arc filaments; scales with pull ratio |
| `FX_Bullet_Trail` | Bullet leaves the muzzle | Aether glow and short ember filaments; follows the bullet and fades after it is destroyed |
| `FX_Bullet_Impact` | Bullet collides with a solid surface | Contact flash, shock ring, sparks, and dust |
| `FX_Bullet_Bounce` | Bullet hits a `BounceSurface` | Cyan ricochet flash/ring and directional shard sparks |
| `FX_Target_VictoryBurst` | Bullet reaches the target | Prismatic core, double halo, starburst, rising shards |
| `FX_Portal_Enter` / `FX_Portal_Exit` | Bullet transfers through a portal pair | Color-coded energy hoops, arc sparks, floating motes |

Every prefab is a collection of named child particle systems. Tune each layer independently in the Particle System inspector; the runtime hooks live in `Assets/Project/Scripts/VFX/` and call the central library. Use **DreamForge → VFX → Rebuild Starter VFX Prefabs** only if you want to regenerate the starter set from the editor builder.
