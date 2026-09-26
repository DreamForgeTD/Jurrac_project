# Cannon Shooter

A portrait mobile physics puzzle. Drag the cannon to adjust its aim and shot strength, then release to fire. Each level gives a limited number of shots. Cans fall and can knock into one another; clear every can to win. If ammunition runs out while cans remain, the level is lost after active shots and moving cans settle. Level 1 displays a short drag-and-release hint.

## Gameplay and progression

The game has five levels assigned to `Gameplay.unity` through `GameManager` and `LevelDefinition` assets. `level_06.asset` is currently an empty editor draft and is not part of the playable sequence. Beyond aiming and shooting, the levels use:

- **Bounce walls:** reflect bullets, letting the player route a shot around obstacles.
- **Portals:** transfer bullets between paired entrances and exits after a short delay.
- **Magnets:** bend bullet paths with an attraction field.

These mechanics change shot placement and route planning. The Level Editor stores level layouts as data and maps prefab IDs through `LevelPrefabCatalog`, so adding a layout does not require rewriting the core shot loop.

## Important decisions

- Kept the scope to five authored levels and one core loop: aim, shoot, observe physics, and retry or continue.
- Used `LevelDefinition` assets and a prefab catalog as the gameplay scene's level source of truth.
- Kept bullet interactions behind small mechanic, trajectory-rule, and force-field interfaces so new interactions can participate in gameplay and trajectory preview.
- Reused can instances and pooled VFX to reduce repeated creation and destruction during play. This is an implementation choice, not a measured performance result.

## Technical status and performance

- Android build: [`APK/Canon_shooter.apk`](APK/Canon_shooter.apk), ARM64. The APK artifact was generated, but it has not been launched on a physical Android device in this workspace.
- No Android device or emulator was connected for profiling. There are no frame-time, CPU, GPU, or memory measurements to report. The main performance risk to profile is the busiest level's physics contacts and particle overdraw. Can and VFX pooling are in place; their impact still needs measurement on target hardware.
- The current APK package ID is `com.UnityTechnologies.com.unity.template.urpblank`; replace it with the final product ID before distribution if required.
- A 1-2 minute gameplay capture has not yet been added to the repository.

## AI and tools

Unity is the game engine; Blender Python scripts automate prototype asset creation. An AI coding assistant was used to help review and refactor code and prepare project documentation. Changes were checked against the existing project code, but Android device playtesting and performance profiling remain outstanding.

## If there were 24 more hours

1. Profile the busiest level on a representative Android phone and fix measured frame-time or memory bottlenecks.
2. Have new players try the first minute; tune drag sensitivity, shot feedback, and win/loss readability from observed confusion.
3. Adjust the five-level progression based on playtest results so bounce, portal, and magnet decisions are introduced and combined clearly.

If only 24 hours remained, keep the drag-and-shoot loop, readable physics, five levels, bounce and portal/magnet interactions, and reliable restart/win/loss flow. Cut optional visual polish and extra content first.
