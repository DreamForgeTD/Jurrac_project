# Data-driven levels

The Level Editor's **New Level** button immediately creates a blank level with the next unused `level_<number>` ID, adds it to `levels.json` and selects it for editing. For example, when `level_01` and `level_02` exist, the button creates `level_03`. Place objects on the grid and use **Save Level** to update `Assets/Project/Data/Levels/<id>.asset` and its matching JSON export under `StreamingAssets`. Gameplay's `GameManager` loads only the assigned `LevelDefinition` assets and uses `LevelPrefabCatalog` to spawn their objects. Runtime applies the saved `localPosition`, `localEulerAngles`, and `localScale` directly; `gridPlacement` is editor snapping metadata. Saving refreshes the level list on loaded GameManager components; use the inspector's refresh button if you saved while the gameplay scene was closed. The SO is loaded first when reopening a level in the editor.

The Level Editor's **Số đạn màn này** field sets `startingBulletCount`. A value above `0` is the level's bullet quota; `0` uses the default configured on `CannonShooter` (currently 20). The editor clamps negative input to `0`. **Save Level** writes this value to both the `LevelDefinition` asset and its JSON export, and reopening the level restores the saved value. New levels start at `0` so they inherit the cannon default. Gameplay applies per-level quotas through `GameManager`; the separate JSON `LevelManager` runtime path does not apply this field.

Portal placement uses two clicks: Entry, then Exit. One `portal_pair` object stores the Entry in `gridPlacement` and the Exit in `portalExitPlacement`. The editor shows only Entry until Exit is chosen, refuses to save an incomplete pair, and removes both endpoints when either cell is erased. The prefab is instantiated once per pair; its child portals are moved to their saved cells in both Scene preview and Play Mode. Magnet visuals are centered on the force field root when instantiated so their visible position matches the selected cell.

`localEulerAngles` stores an extra rotation applied on top of the prefab root's authored rotation. `localScale` is a multiplier on the prefab root scale, so `(1, 1, 1)` keeps the prefab's authored size.

`LevelBoardBounds` on the gameplay camera draws the 1080x1920 frame and its 18x32 cell centers in Scene view. The editor derives its saved grid center, orientation, and cell spacing from this camera view; assign a placement plane or adjust `centerOffset` to move the frame. Objects snap to cell centers.

Trong Inspector của GameManager, mục **Co giãn map theo màn hình** cho phép bật **Tự căn map** và chọn **Độ phân giải thiết kế** (mặc định 1080×1920). Các giá trị này lưu trên `LevelBoardBounds` của Main Camera. Khi Play, camera orthographic lấy size ban đầu làm chuẩn rồi tự zoom để giữ trọn khung thiết kế khi tỉ lệ màn hình thay đổi. Màn hẹp hơn được nới góc nhìn theo chiều cao; màn rộng hơn có khoảng dư hai bên. Vị trí, scale vật thể và thông số vật lý giữ nguyên. Khung lưới dùng size gốc để việc lưu level trong editor không phụ thuộc Game view đang chọn tỉ lệ nào. Tắt tùy chọn hoặc component sẽ khôi phục size camera ban đầu. Camera perspective không áp dụng tính năng này.

The Level Editor board uses 18 columns by 32 rows, twice the previous resolution on each axis while keeping the same portrait board size. Existing 9x16 level placements are scaled to the finer grid when opened. Enter any angle in **New object angle (°)** for new placements. Turn on **Rotate existing object**, click one object to select it, then edit **Object angle (°)** to rotate only that object. The click selects without changing its position or angle, and each object's configured footprint stays in place while it rotates.

Add `LevelPrefabFootprint` to a prefab to set its default editor footprint with `Width In Cells` and `Height In Cells`. New placements and levels opened in the editor read those values from the prefab, and the saved grid placement records them. This controls occupied board cells; it does not resize the prefab mesh or collider. Prefabs without this component use 1x1 cells. `SodaCan_330ml` is configured as 1x1; `Cannon 1` is configured as 3x3. The Cannon footprint is also used for its board highlight, rotation, saved placement, and Scene preview.

The `wall` catalog item uses `Wall_Cube.prefab`, which has a root `BoxCollider`, a 1x1 `LevelPrefabFootprint`, and `TuongNay` with `heSoBatNay = 1`. Its visible mesh is on the `HinhAnhTuong` child so the impact animation scales the visual without changing the collider. The cube is authored at the default cell width of `0.3875` units. The Level Editor scales it by `grid.worldUnitsPerCell / 0.3875` both in the Scene preview and in saved `localScale`, so its X/Y side stays exactly one cell wide when the grid size changes. The grid position is the cell center; adjacent cubes therefore meet at their edges without a gap or overlap. Its Z depth follows the same cube scale. This wall uses the `wall` ID and palette item, separate from `bounce_wall`. The editor places it only on an empty cell or over another `wall`, so drawing a wall cannot replace a can, target, portal, or magnet. Scene preview sync builds a replacement root first and swaps it in only after the build succeeds; a failed sync keeps the previous preview visible.

The separate `LevelManager` component reads `Assets/StreamingAssets/DreamForgeTD/Levels/levels.json` at startup. That JSON runtime path is independent from Gameplay's `GameManager`, whose assigned SO list is the source of truth for level spawning. Each manifest ID maps to a file named `<id>.json` in the same folder.

Level files use schema version 1:

```json
{
  "schemaVersion": 1,
  "id": "level_01",
  "displayName": "First Shot",
  "startingBulletCount": 0,
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

`prefabId` must match an entry in `LevelPrefabCatalog`. Positions, rotations, and scales are local to the `GameObject Manager` origin. Targets are counted as level goals; a bowling can is counted when its first confirmed collision starts its configured delayed-destruction sequence. The level completes when every target and can objective is cleared.

The `soda_can` catalog entry uses `SodaCan_330ml.prefab`. Its mesh transform and CapsuleCollider are kept on the soda can asset itself. The collider uses local center `(0, 0.05780002, 0)`, radius `0.03300001`, height `0.11360003`, and Y direction. A dynamic Rigidbody freezes position Z; gravity turns on when a bullet hits the can or a moving can reaches it. This lets a shot move a few cans or start a chain reaction through the rack.

Use `LevelManager.Instance.LoadLevel("level_02")`, `LoadNextLevel()`, and `RestartCurrentLevel()` to control progression. Subscribe to `LevelLoaded`, `LevelCompleted`, `AllLevelsCompleted`, or `LevelLoadFailed` to connect UI or game flow.

## Level Editor trực quan trong Unity Editor (Bảng lưới 18x32)

Menu mở tool: `Tools` -> `DreamForge` -> `Level Editor`.

Giao diện gồm 2 phần chính trực quan:
1. **Màn hình Board 9:16 (Màn dọc 1080x1920)**:
   - Gồm 18 cột x 32 dòng ô lưới chia đều tương ứng với tỷ lệ màn hình điện thoại.
   - **Click hoặc rê chuột trái**: Tô ô bằng loại mechanic đang chọn trên Palette.
   - **Click chuột phải**: Xóa nhanh ô tại vị trí con trỏ.
   - Hiển thị màu sắc và icon nhận diện rõ ràng (Cannon vàng, Lon đỏ, Bia bắn cam, Tường xanh dương, Portal tím, Nam châm xanh ngọc).

2. **Bảng chọn Palette & Lưu trữ**:
   - Chọn nhanh các ô màu: **Lon nước Win (🥤)**, **Target (🎯)**, **Cannon (🚀)**, **Tường Cube nảy mức 1 (■)**, **Tường nảy (🧱)**, **Portal (🌀)**, **Nam châm (🧲)**, hoặc **Tẩy (✖️)**.
   - Ô nhập góc xoay tùy ý cho vật thể mới và checkbox **Rotate existing object** để áp dụng góc đó lên vật thể đã đặt.
   - Thống kê tức thời số lượng lon, target và cảnh báo thiếu mục tiêu.
   - Nút **`💾 LƯU LEVEL`**: Tạo/cập nhật ScriptableObject (`Assets/Project/Data/Levels/<id>.asset`), xuất JSON runtime (`Assets/StreamingAssets/DreamForgeTD/Levels/<id>.json`) và tự động cập nhật `levels.json`.
   - Tùy chọn **`Tự động đồng bộ lên 3D Scene`**: Cập nhật cả mô hình 3D trong Scene view song song với bảng lưới 2D.
