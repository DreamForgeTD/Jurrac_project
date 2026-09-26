# Data-driven levels

The Level Editor's **New Level** button clears the board and assigns the next unused `level_<number>` ID. Enter a display name, place objects on the grid, and use **Save Level** to create `Assets/Project/Data/Levels/<id>.asset` as the editable `LevelDefinition` ScriptableObject and write a matching JSON export under `StreamingAssets`. Gameplay's `GameManager` loads only the assigned `LevelDefinition` assets and uses `LevelPrefabCatalog` to spawn their objects. Runtime applies the saved `localPosition`, `localEulerAngles`, and `localScale` directly; `gridPlacement` is editor snapping metadata. Saving refreshes the level list on loaded GameManager components; use the inspector's refresh button if you saved while the gameplay scene was closed. The SO is loaded first when reopening a level in the editor.

Portal placement uses two clicks: Entry, then Exit. One `portal_pair` object stores the Entry in `gridPlacement` and the Exit in `portalExitPlacement`. The editor shows only Entry until Exit is chosen, refuses to save an incomplete pair, and removes both endpoints when either cell is erased. The prefab is instantiated once per pair; its child portals are moved to their saved cells in both Scene preview and Play Mode. Magnet visuals are centered on the force field root when instantiated so their visible position matches the selected cell.

`localEulerAngles` stores an extra rotation applied on top of the prefab root's authored rotation. `localScale` is a multiplier on the prefab root scale, so `(1, 1, 1)` keeps the prefab's authored size.

`LevelBoardBounds` on the gameplay camera draws the 1080x1920 frame and its 9x16 cell centers in Scene view. The editor derives its saved grid center, orientation, and cell spacing from this camera view; assign a placement plane or adjust `centerOffset` to move the frame. Objects snap to cell centers.

The separate `LevelManager` component reads `Assets/StreamingAssets/DreamForgeTD/Levels/levels.json` at startup. That JSON runtime path is independent from Gameplay's `GameManager`, whose assigned SO list is the source of truth for level spawning. Each manifest ID maps to a file named `<id>.json` in the same folder.

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

`prefabId` must match an entry in `LevelPrefabCatalog`. Positions, rotations, and scales are local to the `GameObject Manager` origin. Targets are counted as level goals; bowling cans are counted after they are hit or chained into motion and move away from their starting spots. The level completes when every target and can objective is cleared.

The `soda_can` catalog entry uses `SodaCan_330ml.prefab`. Its mesh transform and CapsuleCollider are kept on the soda can asset itself. The collider uses local center `(0, 0.05780002, 0)`, radius `0.03300001`, height `0.11360003`, and Y direction. A dynamic Rigidbody freezes position Z; gravity turns on when a bullet hits the can or a moving can reaches it. This lets a shot move a few cans or start a chain reaction through the rack.

Use `LevelManager.Instance.LoadLevel("level_02")`, `LoadNextLevel()`, and `RestartCurrentLevel()` to control progression. Subscribe to `LevelLoaded`, `LevelCompleted`, `AllLevelsCompleted`, or `LevelLoadFailed` to connect UI or game flow.

## Level Editor trực quan trong Unity Editor (Bảng lưới 9:16)

Menu mở tool: `Tools` -> `DreamForge` -> `Level Editor`.

Giao diện gồm 2 phần chính trực quan:
1. **Màn hình Board 9:16 (Màn dọc 1080x1920)**:
   - Gồm 9 cột x 16 dòng ô lưới chia đều tương ứng với tỷ lệ màn hình điện thoại.
   - **Click hoặc rê chuột trái**: Tô ô bằng loại mechanic đang chọn trên Palette.
   - **Click chuột phải**: Xóa nhanh ô tại vị trí con trỏ.
   - Hiển thị màu sắc và icon nhận diện rõ ràng (Cannon vàng, Lon đỏ, Bia bắn cam, Tường xanh dương, Portal tím, Nam châm xanh ngọc).

2. **Bảng chọn Palette & Lưu trữ**:
   - Chọn nhanh các ô màu: **Lon nước Win (🥤)**, **Target (🎯)**, **Cannon (🚀)**, **Tường nảy (🧱)**, **Portal (🌀)**, **Nam châm (🧲)**, hoặc **Tẩy (✖️)**.
   - Nút chỉnh góc xoay (`0°`, `90°`, `180°`, `270°`).
   - Thống kê tức thời số lượng lon, target và cảnh báo thiếu mục tiêu.
   - Nút **`💾 LƯU LEVEL`**: Tạo/cập nhật ScriptableObject (`Assets/Project/Data/Levels/<id>.asset`), xuất JSON runtime (`Assets/StreamingAssets/DreamForgeTD/Levels/<id>.json`) và tự động cập nhật `levels.json`.
   - Tùy chọn **`Tự động đồng bộ lên 3D Scene`**: Cập nhật cả mô hình 3D trong Scene view song song với bảng lưới 2D.
