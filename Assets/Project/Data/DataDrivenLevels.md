# Data-driven levels

`LevelManager` reads `Assets/StreamingAssets/DreamForgeTD/Levels/levels.json` at startup. The manifest controls the order in which levels run. Each listed ID maps to a file named `<id>.json` in the same folder.

Level files use schema version 1:

```json
{
  "schemaVersion": 1,
  "id": "level_01",
  "displayName": "First Shot",
  "objects": [
    {
      "prefabId": "target",
      "instanceName": "Target",
      "localPosition": { "x": -0.1, "y": 3.8, "z": 0.0 },
      "localEulerAngles": { "x": 0.0, "y": 0.0, "z": 0.0 },
      "localScale": { "x": 1.0, "y": 1.0, "z": 1.0 }
    }
  ]
}
```

`prefabId` must match an entry in `LevelPrefabCatalog`. Positions, rotations, and scales are local to the `GameObject Manager` origin. Targets are counted as level goals; the level completes after every spawned `Target` is defeated.

Use `LevelManager.Instance.LoadLevel("level_02")`, `LoadNextLevel()`, and `RestartCurrentLevel()` to control progression. Subscribe to `LevelLoaded`, `LevelCompleted`, `AllLevelsCompleted`, or `LevelLoadFailed` to connect UI or game flow.
