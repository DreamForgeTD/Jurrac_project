# Hướng dẫn agent cho project Jurrac

Tài liệu này ghi lại cách mở project trong worktree `agent/codex2`, cách dùng Unity MCP khi được yêu cầu, và luồng quản lý level data-driven đã thêm. Hãy đọc cùng `Assets/Project/Data/DataDrivenLevels.md` trước khi sửa cấu trúc level.

## Worktree và Unity MCP

- Nhánh của worktree này là `agent/codex2`. Chỉ sửa và commit trong worktree tương ứng; không chuyển thay đổi sang checkout khác.
- Mở đúng thư mục project bằng Unity Hub. Package Unity MCP được khai báo duy nhất trong `Packages/manifest.json`, ghim ở tag `v10.2.0`; `Packages/packages-lock.json` phải cùng tag.
- `.codex/config.toml` khai báo endpoint MCP cục bộ `http://localhost:8080/mcp`. File này chỉ là cấu hình endpoint, không chứng minh Unity Editor hay MCP server đang chạy. Trước khi dùng MCP, kiểm tra editor state/instance và trạng thái kết nối; không tự khởi động hoặc kết nối MCP nếu người dùng chưa yêu cầu.
- Sau khi clone hoặc đổi package, mở project và đợi Unity Package Manager resolve xong. Nếu manifest báo JSON lỗi, kiểm tra các key trong object `dependencies` không bị trùng trước khi sửa lock file.
- Khi thao tác qua MCP, đọc resource/editor state và thông tin scene trước khi sửa. Sau khi sửa script qua Unity MCP, chờ compile kết thúc rồi đọc Console. Không coi template hoặc trạng thái MCP từ phiên agent khác là trạng thái hiện tại.

## Hệ thống quản lý object và level

Các script runtime nằm trong namespace `DreamForgeTD`:

- `Assets/Project/Scripts/Levels/GameObjectManager.cs`: spawn prefab dưới một root được quản lý, áp dụng transform local từ data, giữ danh sách instance và xóa toàn bộ content của level trước. Trong Play Mode manager gọi `DontDestroyOnLoad`.
- `Assets/Project/Scripts/Levels/LevelManager.cs`: singleton điều khiển manifest, tải JSON bằng `UnityWebRequest` từ `StreamingAssets`, kiểm tra schema/id/prefab/transform, spawn level, nhận sự kiện target bị hạ và phát sự kiện tiến trình. Có `LoadLevel(index)`, `LoadLevel(id)`, `LoadNextLevel()` và `RestartCurrentLevel()`.
- `Assets/Project/Scripts/Levels/LevelPrefabCatalog.cs`: ScriptableObject ánh xạ ID data sang prefab.
- `Assets/Project/Scripts/Target.cs`: phát event `Defeated` khi bị bắn trúng. Nếu không có level listener thì vẫn giữ hành vi cũ là dừng thời gian.

`LevelManager` yêu cầu cùng GameObject có `GameObjectManager`. Script đặt execution order để manager khởi tạo trước level manager. Trong scene chạy game, tạo một GameObject duy nhất (nên đặt tên `GameObject Manager`), thêm `LevelManager` và gán `LevelPrefabCatalog` vào trường `Prefab Catalog`. Để `Load On Start` bật nếu muốn tự tải level đầu. Chỉ giữ một manager sống giữa các scene.

Catalog mẫu đã có ở `Assets/Project/Data/LevelPrefabCatalog.asset` với các ID `target`, `bounce_wall`, `portal_pair`, `magnet`. Mỗi ID phải duy nhất và trỏ tới prefab hợp lệ. Khi thêm prefab mới, thêm entry vào catalog trước khi dùng ID đó trong JSON.

## Định dạng và thêm level

Dữ liệu đặt ở `Assets/StreamingAssets/DreamForgeTD/Levels/`:

1. Sửa `levels.json` để khai báo thứ tự ID trong `levelIds`.
2. Tạo `<id>.json` cho từng ID. File phải có `schemaVersion: 1`, `id` khớp ID trong manifest, `displayName`, và mảng `objects`.
3. Mỗi object khai báo `prefabId`, `instanceName` tùy chọn, `localPosition`, `localEulerAngles`, `localScale`. Ba transform là vector JSON `{ "x": 0, "y": 0, "z": 0 }`, tương đối đến root của `GameObjectManager`.
4. Chỉ dùng ID gồm chữ, số, `_` hoặc `-`; manifest không nhận ID trùng nhau. Tối đa 256 object mỗi level. Mọi `prefabId` phải có trong catalog.

Hai level mẫu là `level_01` và `level_02`. `LevelManager` kiểm tra tất cả prefab trước khi xóa level đang chơi. Các `Target` nằm trong prefab được đếm làm mục tiêu; khi mục tiêu cuối bị hạ, `LevelCompleted` được phát và `Time.timeScale` về 0. Level không có `Target` chỉ phát cảnh báo, không tự hoàn thành.

UI/game flow có thể gọi `LevelManager.Instance.LoadLevel("level_02")`, `LoadNextLevel()` hoặc `RestartCurrentLevel()`, và đăng ký các event `LevelLoaded`, `LevelCompleted`, `AllLevelsCompleted`, `LevelLoadFailed`. Khi thêm hệ thống pause/UI, lưu ý level hoàn thành đang dừng `Time.timeScale`; tải level mới sẽ đặt lại bằng 1.

`MapSpawner`/`MapDefinition` là luồng ScriptableObject cũ nằm trong `Assets/Project/Scripts/Maps`. Không nhầm nó với hệ thống JSON mới; hãy xác nhận scene hoặc tính năng đang dùng luồng nào trước khi chỉnh.

## Sự cố đã sửa và phạm vi scene

- `Packages/manifest.json` từng khai báo `com.coplaydev.unity-mcp` hai lần (`main` và `v10.2.0`). Đã bỏ key trùng và giữ tag ổn định `v10.2.0`; lock file được đồng bộ.
- `LevelManager.cs` từng gọi tên biến không tồn tại `currentLevelId`/`currentLevelName`. Đã đổi sang property `CurrentLevelId`/`CurrentLevelName`.
- `Target` được nối với event defeat để level manager biết khi nào hoàn thành, đồng thời giữ fallback cho target chạy độc lập.
- Thay đổi scene không nằm trong commit feature hiện tại theo yêu cầu của người dùng. Nếu cần triển khai manager vào scene, hãy làm và review riêng; đừng tự đưa scene vào commit.

Tài liệu JSON nhanh: `Assets/Project/Data/DataDrivenLevels.md`. Bộ cartoon VFX đã được lưu riêng dưới `Assets/Art/VFX/CartoonTextures/` và có README trong thư mục đó.
