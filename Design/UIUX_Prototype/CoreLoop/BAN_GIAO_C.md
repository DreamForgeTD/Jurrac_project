# Bàn giao Agent C

## Trạng thái hiện tại

- Vai trò: Agent C — UI, tutorial và scene.
- Task hiện tại: hiển thị số đạn còn bắn được và làm chuyển động xuất hiện nhẹ cho UI Win/Lose.
- API v1 của A chưa có; phần này chỉ dùng thuộc tính đã tồn tại `CannonShooter.RemainingBulletCount`, chưa phải đợt chuyển GameFlowUI sang snapshot của core loop.

## Thay đổi của C

- `Assets/Project/Scripts/GameFlowUI.cs`: đổi reference TMP thành `txtSoDan`, giữ tương thích dữ liệu cũ bằng `FormerlySerializedAs("bulletCountText")`.
- GameFlowUI đọc `RemainingBulletCount` từ CannonShooter đã cache. Text chỉ được ghi lại khi số đạn đổi; hiển thị `Đạn còn: N`.
- Bỏ subscribe `BulletCountChanged` riêng của UI. Event này vẫn được các phần A/B hiện tại dùng; C không sửa các file của họ.
- `Assets/Project/Scenes/Gameplay.unity`: đã nối riêng nền thắng, nền thua và nút Retry vào GameFlowUI trong một diff C trước đó.
- `Assets/Project/Scripts/Levels/Editor/LevelEditorWindow.cs`: thêm trường **Số đạn màn này**. Editor khôi phục `startingBulletCount` khi mở level, giữ số không âm và ghi giá trị vào `LevelDocument` khi lưu cả ScriptableObject lẫn JSON. Level mới dùng `0` để kế thừa quota mặc định của súng.
- `Assets/Project/Data/DataDrivenLevels.md`: ghi rõ ý nghĩa quota, giá trị mặc định và phạm vi runtime áp dụng.
- `Assets/Project/Scripts/GameFlowUI.cs`: panel kết quả hiện lần lượt nền → logo → nút. Mỗi phần tử fade/scale nhẹ trong 0,28 giây; nút chỉ nhận tương tác sau khi hiệu ứng hoàn tất. Hoạt ảnh dùng `Time.unscaledDeltaTime` nên vẫn chạy khi game dừng thời gian.
- `Assets/Project/Scripts/GameFlowUI.cs`: thêm trường Inspector `txtSoMan` để gán TextMeshPro số màn. Nhãn chỉ hiện số thứ tự (ví dụ `1`) khi mở scene và mỗi lần GameManager tải level mới.
- `Assets/Project/Resoruce_game/Prefab/Wall_bounce.prefab`: gán child model `Wall_1` vào trường `TuongNay.phanHinhAnhNay`; va chạm làm model nảy nhẹ, collider root giữ nguyên vị trí/kích thước.
- Phối hợp A sửa luồng `BowlingCan` để bắt đầu mờ ngay khi lon rơi và giữ vật lý hoạt động trong lúc mờ. C đặt `disappearAnimationDuration` của prefab `SodaCan_330ml` bằng `knockedDownLifetime` (1,2 giây) để lon mờ dần suốt quãng rơi.
- Thực hiện yêu cầu tường Cube trong `BAN_GIAO_A.md`: tạo `Assets/Project/Resoruce_game/Prefab/Wall_Cube.prefab` với root `BoxCollider`, `LevelPrefabFootprint` 1×1 và `TuongNay` (`heSoBatNay = 1`); mesh hiển thị nằm trên child `HinhAnhTuong`, nên animation không co collider. Đăng ký ID `wall` trong `LevelPrefabCatalog.asset`; palette, bộ đếm và nhãn nêu rõ mức nảy 1. `bounce_wall` vẫn giữ ID riêng.
- Prefab Cube được tạo với cạnh gốc 0,3875 đơn vị. `LevelEditorWindow` lấy tâm ô qua `LevelGridUtility.GetLocalPosition`, footprint của `wall` đọc thành 1×1 từ prefab, và nhân scale bằng `grid.worldUnitsPerCell / 0.3875` khi preview lẫn lưu dữ liệu. Vì vậy cạnh X/Y của khối khớp đúng một ô kể cả khi kích thước grid thay đổi; Z theo cùng tỷ lệ để khối vẫn là cube. Dữ liệu `wall` đi qua luồng lưu/mở SO và JSON đang có của editor.
- `LevelEditorWindow` không cho palette `wall` ghi đè asset khác đang ở cùng ô. Đồng bộ khi kéo chuột được debounce 0,12 giây; preview mới dựng trong root tạm và chỉ thay root hiện tại sau khi dựng xong. Nếu đồng bộ lỗi, preview cũ được giữ lại thay vì xóa cả nhóm object.
- `Assets/Project/Scripts/Levels/LevelObjectMarker.cs`: khi object preview trong Gameplay Scene vào Play Mode, tắt Collider, xóa vận tốc và đặt Rigidbody thành kinematic. Đây là bản preview lưu trong Scene, không phải object runtime do catalog spawn; nếu GameManager từ chối level, preview vẫn đứng yên và không tự va chạm/fade.
- Cập nhật `Assets/Project/Data/DataDrivenLevels.md` để mô tả prefab, footprint, scale theo lưới và component `TuongNay` của `Wall_Cube`.

## Phát hiện và phối hợp: chai biến mất khi Play

- Đã dọn các trường `portalExitPlacement` bị sót trong cả LevelDefinition lẫn JSON của `level_01` và `level_02`. Mỗi level còn 98 object và không có `portal_pair`; JSON sau khi dọn vẫn hợp lệ. A cũng đã sửa validator trong `GameManager.TryValidateDefinition` để chỉ kiểm tra portal exit khi `prefabId == "portal_pair"`.
- Từ ảnh và cấu hình hiện tại: `level_01` có 48 lon, 50 tường, bước lưới 0,3875; có 78 cặp lon nằm ở hai ô kề nhau. Collider capsule trên prefab SodaCan cao 0,125 đơn vị ở scale gốc 5, tương đương khoảng 0,625 đơn vị trước khi xét góc xoay, lớn hơn khoảng cách hàng 0,3875.
- Bản sửa đầu chỉ ngăn va chạm ban đầu gọi fade nhưng Rigidbody runtime vẫn dynamic, nên solver vẫn đẩy các collider đang chồng lấn và làm lon lệch hàng. Ảnh người dùng gửi sau đó cho thấy còn 16 viên đạn, tức đã bắn 4 phát; level đã nạp nhưng các lon bị xô lệch sau va chạm.
- A đã sửa tiếp `BowlingCan`: lon runtime giữ Rigidbody kinematic khi chưa bị kích hoạt; đạn trúng sẽ bật physics và truyền phần xung lực mất của đạn cho lon. Khi lon đang chạy va vào lon kinematic kế tiếp, lon kế tiếp được bật physics, nhận xung lực đối chiều một lần, đăng ký knockdown/fade ngay để phản ứng dây chuyền tiếp tục. Tiếp xúc ban đầu giữa các lon chưa bị bắn vẫn inert; pooling đưa lon về trạng thái kinematic.
- C giữ preview Scene bất động khi Play để level lỗi tải không làm bản preview tự rơi/fade. Chưa chạy Play Mode; cần xác nhận: tải level giữ nguyên hàng lon, bắn một lon làm phản ứng lan ngay qua các lon va chạm, và Retry trả toàn bộ lon về trạng thái ban đầu.

## Việc còn cần làm

- Gameplay scene chưa có TextMeshPro để gán vào `txtSoDan`; HUD chưa thể hiện số đạn cho đến khi C tạo/gán label `txt_so_dan` trong Canvas.
- Khi API v1 có, đối chiếu cách đọc số đạn với snapshot của GameManager mà không sửa chéo file A/B.
- Working tree hiện tại đã có `CannonShooter` triển khai `IGioiHanDanTheoMan`, và `GameManager` truyền quota màn vào súng. Luồng JSON `LevelManager` riêng chưa áp dụng quota này.
- Chưa chạy Unity Play Mode. `git diff --check` không báo lỗi whitespace cho thay đổi GameFlowUI.
- Tường `wall` mới chưa được mở trong Unity Editor để xác nhận render/collider và đặt thử hai ô liền nhau; phiên này không có Unity MCP tool khả dụng. Cần xác nhận trực quan khi mở Level Editor.
- Chuyển màn kéo nội dung xuống/chuyển nội dung mới vào đang chờ A chốt API điều phối và root chung; C sẽ nối ẩn panel Win/HUD presentation vào API đó mà không tự gọi load level.

## Diff có sẵn được giữ nguyên

- `LevelOneTutorial.cs` có thay đổi spotlight intro trước khi C nhận task; C không ghi đè thay đổi đó.
- Các diff hiện có ở `GameManager.cs`, `CannonShooter.cs`, `Bullet.cs`, level asset/JSON, font và tài liệu thuộc các owner hoặc công việc khác. C không sửa các file này.
# Đợt tối ưu runtime — 26-09-2026

Nền: `ae0ce8f`, nhánh `dev`, worktree `E:/Project-Unity/Jurrac_project`. Đầu đợt working tree sạch. Phần A/B được giao riêng theo ownership; C tích hợp UI và kiểm tra compiler. Chưa commit/push đợt tối ưu này.

## Thay đổi C

- `GameFlowUI.cs`: bỏ chuỗi đăng ký/hủy event GameManager và tìm CannonShooter trong scene. Đọc `MaLuot`, trạng thái kết quả và `SungHienTai`; chỉ đổi HUD/panel khi dữ liệu thay đổi. Retry cùng level vẫn được nhận biết qua `MaLuot`. Button.onClick vẫn cần để nhận thao tác Unity UI.
- Giữ animation nảy, thời gian unscaled, panel thắng/thua và các reference serialized/legacy đang được scene sử dụng. FX thắng gắn sẵn trong scene được phát/dừng trên chính instance đó, không tạo/hủy instance mỗi lần thắng.
- `LevelOneTutorial.cs`: bỏ event load và tìm object lặp; lấy cannon/shooter từ manager. Vòng sáng dùng lại mảng pixel, chỉ xóa vùng sáng cũ và tính vùng sáng mới. Màu, bán kính, thời gian intro/fade và cách ngắm được giữ nguyên. Mảng 540 × 960 × 4 byte trước đây được tạo lại khi vòng sáng đổi; nay chỉ tạo khi kích thước texture đổi.
- `LevelGridUtility.cs`: bỏ object placement tạm trong chuyển tọa độ về ô lưới; công thức và kiểm tra đầu vào giữ nguyên.
- `LevelEditorWindow.cs`: cache danh sách tên màn khi refresh manifest, tránh `ToArray()` mỗi lần vẽ menu.

## Tích hợp A/B

- A cung cấp `MaLuot`, `DaHoanTatTatCaMan`, `SungHienTai`, `DieuKhienSung`; xóa C# events GameManager khi C đã bỏ consumer. UnityEvent serialized và callback mục tiêu của luồng JSON vẫn có consumer nên giữ.
- B pool FX runtime, A chuyển custom FX của lon sang cùng pool. Dọn đạn/phát chờ trước khi thu hồi FX lúc đổi màn; trail hết hạn tự nhiên được dừng phát và chờ hạt tan.
- Không cần sửa scene/prefab để dùng pool: component cleanup và root được quản lý trong runtime. Giữ GUID script hiện có.

## Kiểm tra và giới hạn

- Compile offline runtime và editor bằng Roslyn của Unity `6000.0.78f1`, dùng references/defines từ Bee response files; output ở thư mục tạm ngoài project. Không ghi đè DLL trong Library.
- Đối chiếu thuật toán vùng pixel với quét toàn ảnh qua 24 trường hợp di chuyển, thu nhỏ, sát mép và ba kích thước texture: mọi pixel khớp. Đây là kiểm tra thuật toán, chưa phải ảnh render trong Unity.
- Chưa chạy Play Mode/Profiler: chưa có số đo FPS, CPU hoặc GC thực tế. Cần kiểm tra va chạm dày, Retry khi trail đang bay, vòng pool lặp và Play/Stop khi tắt domain reload.

## C — Tự căn map theo tỉ lệ màn hình

- File: `Levels/LevelBoardBounds.cs`, `Levels/Editor/GameManagerEditor.cs`, `Assets/Project/Data/DataDrivenLevels.md`. Dùng component camera sẵn có, không cần A sửa GameManager hoặc B đổi vật lý/input.
- Inspector GameManager có mục **Co giãn map theo màn hình**: bật/tắt và độ phân giải thiết kế; giá trị lưu trên LevelBoardBounds của Main Camera. Mặc định bật với khung 1080×1920.
- Runtime căn camera orthographic theo công thức `sizeGoc * Max(1, tiLeThietKe / camera.aspect)`, cập nhật trước input/UI khi đổi kích thước màn hình. Khôi phục size gốc khi tắt; không scale collider, vị trí hay lực bắn. Grid editor tiếp tục dùng size gốc để tránh thay đổi dữ liệu level theo tỉ lệ Game view.
- Compile offline runtime và editor bằng Roslyn Unity 6000.0.78f1 thành công; DLL ở thư mục tạm ngoài project. Kiểm tra số học khung 6,975×12,4 ở 1080×1920, 1080×2400, 1080×2340, 720×1600, 768×1024, 1920×1080 và quay về 1080×1920: đều đủ khung; size về 6,2 tại tỉ lệ thiết kế.
- Chưa xác nhận trực quan trong Play Mode/Android. Cần xem HUD/tutorial, đổi tỉ lệ khi đang ngắm, Retry/Next và bật/tắt tùy chọn. Camera perspective và safe area không được xử lý trong tính năng này; tỉ lệ khác khung thiết kế có thể có khoảng dư ở mép.
