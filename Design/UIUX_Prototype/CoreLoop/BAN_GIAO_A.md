# Bàn giao Agent A

## Vai trò và task

- Vai trò: Agent A — luật chơi, trạng thái và mục tiêu.
- Task: xử lý thua khi hết đạn; khóa cannon trong lúc chờ kết quả; phát kết quả thua sau khi lượt bắn và lon đã ổn định.
- Checkout: `dev` (`a03a5b49` là HEAD lúc bắt đầu task).

## File A đã sửa

- `Assets/Project/Scripts/GameManager.cs`
- `Assets/Project/Scripts/BowlingCan.cs`
- `Assets/Project/Scripts/Levels/LevelManager.cs` (shared level data schema only)
- `Assets/Project/Scripts/CoreLoop/IGioiHanDanTheoMan.cs` and its `.meta`

## Thay đổi

- GameManager đọc `CannonShooter.RemainingBulletCount`. Khi số đạn về 0, khóa `CannonController` và `CannonShooter` ngay.
- Chỉ chốt thua khi không còn phát bắn chờ, không còn projectile đang hoạt động, các lon còn lại không chuyển động quá ngưỡng trong 0,4 giây liên tục, và mục tiêu chưa được hoàn thành. Điều kiện thắng được kiểm tra trước điều kiện thua.
- `LoseCurrentLevel()` đặt `IsLevelLost`, dừng thời gian và phát `LevelLost`; GameFlowUI hiện có handler để bật panel thua.
- `BowlingCan.DangChuyenDong` dùng ngưỡng khởi đầu 0,1 đơn vị/giây và 0,1 radian/giây.
- Khi nạp lại level, GameManager khôi phục trạng thái enabled ban đầu của cannon/shooter đã khóa.

## Tương thích và thay đổi có sẵn

- Giữ và dùng các API đang có trong diff B: `RemainingBulletCount`, `HasPendingShot`, `ActiveProjectileCount`, cùng các event đạn. Không sửa file B.
- Không sửa `GameFlowUI.cs`, `Gameplay.unity` hoặc tutorial; các file này thuộc C.
- `GameFlowUI` đã đọc cùng số đạn để cập nhật text và nghe `LevelLost` để hiện panel. Scene hiện chưa có label `txt_so_dan` được gán, nên C cần hoàn tất binding để người chơi thấy số đạn trên HUD.
- API v1/G0 chưa được xác nhận cùng B và C; thay đổi hiện tại dùng các API đã có trong working tree.

## Quota đạn theo level — đang chờ bàn giao B/C

- Thêm `LevelDocument.startingBulletCount`. Giá trị dương là quota của level; `0` kế thừa quota mặc định trên `CannonShooter`, giúp dữ liệu cũ tiếp tục dùng mức 20 hiện có. Giá trị âm bị từ chối khi validate.
- GameManager truyền quota qua hợp đồng A/B `IGioiHanDanTheoMan.NapDanTheoMan(int soDan)`. Nếu shooter chưa triển khai hợp đồng và level yêu cầu giá trị khác mặc định, GameManager báo lỗi rồi dùng quota mặc định để giữ runtime chạy được.
- B cần triển khai hợp đồng trên `CannonShooter` mà không sửa giá trị mặc định serialized của prefab. Khi level load, quota được áp vào lượt mới; API `ResetAmmoForLevel()` cũ tiếp tục giữ tương thích.
- C cần expose/preserve `startingBulletCount` trong Level Editor và chọn quota cho từng level. Các asset hiện hữu có thể giữ `0` để kế thừa mặc định. DataDrivenLevels.md cũng thuộc C.
- Trường này nằm trong document được SO/JSON export dùng chung. Luồng gameplay qua GameManager áp quota; LevelManager JSON riêng hiện chưa áp quota này.

## Kiểm tra và việc còn lại

- Chưa chạy Unity Play Mode. Cần kiểm tra hết đạn, phát cuối vẫn trúng mục tiêu, Retry khôi phục input/đạn, và panel thua hoạt động ở `Time.timeScale == 0`.
- Chưa thêm timeout dọn projectile sau 8 giây; cần thống nhất API dọn đạn với B nếu muốn thực thi ngưỡng đó.
- Per-level quota chưa hoạt động cho giá trị khác mặc định cho tới khi B triển khai `IGioiHanDanTheoMan` và C thêm điều khiển dữ liệu trong Level Editor.

## Yêu cầu ngoài phạm vi A — chuyển B/C

- B sở hữu `Assets/Project/Scripts/TuongNay.cs`: khi bullet va chạm, tạo phản hồi bounce nhìn thấy được trên phần visual của tường và dùng cùng hệ số cho vận tốc phản xạ lẫn `PredictTrajectory`.
- `BulletImpactRouter` hiện đã gọi VFX và `TuongNay` phát âm thanh; giữ một lần mỗi va chạm, không phát lặp.
- Nếu animation cần Animator/Transform reference hoặc clip trên prefab, B nêu field cần có và C gán vào prefab. A không sửa `TuongNay.cs` hoặc prefab.

## Yêu cầu ngoài phạm vi A — EditMap/Level Editor (chuyển C)

- C sở hữu `Assets/Project/Scripts/Levels/Editor/LevelEditorWindow.cs`, `LevelPrefabCatalog.asset` và prefab. Editor hiện có mục `bounce_wall` riêng; `wall` là `Wall_Cube` với hệ số nảy 1, không đổi ID `bounce_wall`.
- Tạo prefab placeholder dùng khối Cube có collider, đăng ký ID `wall` trong catalog và cho chọn/đặt trong palette của Level Editor.
- Mỗi object `wall` chiếm đúng footprint 1×1 cell. Kích thước mặt khối theo `grid.worldUnitsPerCell`, tâm lấy từ `LevelGridUtility.GetLocalPosition`, để các khối đặt ở hai ô kề nhau chạm cạnh khít, không hở hoặc chồng lên nhau. Giữ độ dày theo trục Z đủ để nhìn thấy và va chạm; không làm thay đổi footprint trên mặt phẳng map.
- Lưu, tải lại và đồng bộ preview/scene phải giữ đúng ID, vị trí và footprint `1×1`. Không chạy hoặc save Unity Editor/MCP nếu chưa được người dùng yêu cầu.
- A không sửa editor, catalog hay prefab vì thuộc quyền C.

## BowlingCan: vừa rơi vừa mờ dần

- Khi va chạm đầu tiên được xác nhận, lon bắt đầu fade ngay và tiếp tục chuyển động bằng Rigidbody trong suốt vòng đời; không còn dừng vận tốc hoặc chuyển Rigidbody sang kinematic trước khi mờ.
- Lon được ẩn/trả về pool khi hết knockedDownLifetime, giữ thời điểm kết thúc vòng đời hiện có. VFX, va chạm/âm thanh, thông báo mục tiêu đúng một lần, cùng khôi phục vật liệu khi dùng pool được giữ nguyên.
- C đã cập nhật Assets/Project/Resoruce_game/Prefab/SodaCan_330ml.prefab: knockedDownLifetime = 1.2 giây và disappearAnimationDuration = 1.2 giây, để fade diễn ra xuyên suốt lúc lon rơi.
- Chưa kiểm tra bằng Play Mode.


## Toi uu runtime (26-09-2026)

Pham vi A: BowlingCan.cs, GameManager.cs, Levels/GameObjectManager.cs, Levels/LevelManager.cs, CoreLoop/IGioiHanDanTheoMan.cs va tai lieu A. Khong sua scene/prefab hay thong so vat ly.

- Lon dung mot Update cho ca fade va het lifetime; bo coroutine fade va countdown trung nhau. Thoi gian van theo realtime, van vua roi vua mo, giu impulse/ricochet/day chuyen hien tai.
- Material fade chi clone mot lan cho moi lon duoc pool. Khi tra pool/reset thi gan lai shared material goc va shadow; lan trung tiep theo tai su dung clone. Chi Destroy material clone khi lon bi huy that. Doi lai pool giu material trong bo nho de tranh tao/huy moi luot.
- Lon kinematic dung cho khong doc velocity/angular velocity/center of mass moi FixedUpdate. Material color dung Shader.PropertyToID.
- FX va cham lon goi GameVfx.Phat cua B, bo Instantiate/GetComponents/Play/cleanup lap lai rieng. GameObjectManager don FX khi don man. GameManager va LevelManager don dan/trail truoc khi tra FX ve pool de khong giu reference sang luot sau.
- GameManager va GameObjectManager dung lai List cho GetComponentsInChildren; tra lon vao pool chi tim/xoa trong managedObjects mot lan. Bo log cho tung lon bi ha.
- C da chuyen UI/tutorial sang doc trang thai. Xoa nam C# event GameManager khong con consumer; giu UnityEvent serialized trong Inspector. Giu event lon cho GameManager va LevelManager JSON, event Target.Defeated cho JSON. Khong xoa callback Unity/serialized dang co vai tro.
- Bo subscription ActiveProjectileCountChanged chi lap kiem tra ammo; giu BulletCountChanged de khoa input ngay khi het dan. Ket qua van doi dan/physics theo logic hien tai.
- Bo interface rollout IGioiHanDanTheoMan (chi co mot implementation concrete); GameManager goi thang CannonShooter.NapDanTheoMan. B da bo implementation declaration.
- API moi cho C: MaLuot tang sau khi load thanh cong (ke ca retry cung level), DaHoanTatTatCaMan duoc set khi NextLevel vuot level cuoi va reset khi load, SungHienTai/DieuKhienSung cung cap reference da bind.

Kiem tra: da ra soat diff va reference caller; git diff --check pham vi A sach. Chua chay Unity Play Mode hoac do Profiler, chua khang dinh FPS/GC cai thien bang so do. Can nghiem thu retry khi dang fade, retry khi dan/trail/charge dang chay, day chuyen lon, win/lose va man cuoi. Khong commit/push trong dot nay.
