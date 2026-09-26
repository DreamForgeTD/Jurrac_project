# Audio sources

The imported clips below come from Kenney audio packs. Each listed pack is licensed CC0; attribution is not required. Sources are recorded here for provenance.

- [Sci-fi Sounds](https://kenney.nl/assets/sci-fi-sounds), Kenney: laserSmall_002.ogg -> CannonShot.ogg; forceField_001.ogg -> PortalEnter.ogg; forceField_003.ogg -> PortalExit.ogg.
- [Impact Sounds](https://kenney.nl/assets/impact-sounds), Kenney: impactMetal_medium_002.ogg -> Bounce.ogg.
- [Music Jingles](https://kenney.nl/assets/music-jingles), Kenney: jingles_NES00.ogg -> TargetWin.ogg.
- [Music Loops](https://opengameart.org/content/music-loops), Pauliuw: `four_loop.mp3` -> `Assets/Project/Resources/Audio/BGM/BackgroundMusic.mp3` (CC0, loopable background track).
- [Kenney license information](https://kenney.nl/support): assets are CC0/public domain; attribution is optional.

The Cartoon effects below are copied from the project's existing Epic Toon FX library in `Assets/Epic Toon FX/Sound/`. Their original library files remain in place; copies under `Resources` let the runtime audio manager find them automatically.

- `etfx_shoot_energy01.wav` -> `CannonShot_Cartoon.wav` for cannon fire.
- `etfx_impact_metal01.wav` -> `Bounce_Cartoon.wav` for bounce surfaces.
- `etfx_impact_metal02.wav` -> `ObstacleCollision_Cartoon.wav` for other solid obstacles.
- `etfx_impact_metal03.wav` -> `CanCollision_Cartoon.wav` for can impacts with bullets, other cans, and solid surfaces.
- `etfx_explosion_bubble.wav` -> `ButtonClick_Cartoon.wav` for UI clicks.
- `etfx_target_hit.wav` -> `TargetHit_Cartoon.wav` layered with the existing Kenney win jingle.
- `etfx_spawn.wav` -> `LevelStart_Cartoon.wav` as a short opening cue while the project has no looping background track.

The portal enter/exit effects and target win jingle continue to use their existing Kenney clips. `Assets/Project/Scenes/Gameplay.unity` now has an enabled `Audio Manager` object with the background track and every gameplay clip assigned directly. The Resources copies remain as fallback for other scenes.

`AudioManager` loads these optional clips from `Resources` when present:

- Button click: `Assets/Project/Resources/Audio/SFX/ButtonClick.ogg` (or another supported audio format).
- Background music: `Assets/Project/Resources/Audio/BGM/BackgroundMusic.ogg` (or another supported audio format). It starts automatically and keeps playing across scene loads.
