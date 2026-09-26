# AGENTS — Kế hoạch core loop và phân việc A / B / C

Cập nhật: 26-09-2026. Trạng thái: **kế hoạch triển khai, chưa hoàn thành refactor**.

Tài liệu này thay README trong thư mục UIUX_Prototype. Agent làm core loop phải đọc tài liệu này cùng AGENTS.md ở gốc repo và Assets/Project/Data/DataDrivenLevels.md. Báo cáo công việc trước đây được giữ tại [HIEN_TRANG_2026-09-26.md](HIEN_TRANG_2026-09-26.md); đó là ảnh chụp hiện trạng bằng văn bản, không phải kết quả kiểm thử mới.

## 1. Mục tiêu và ranh giới hoàn thành

Hoàn thiện một vòng chơi: mở Gameplay → tải màn hợp lệ → hiện số đạn và mục tiêu → kéo ngắm → thả bắn → đạn tương tác với vật thể → xác định thắng/thua → chơi lại hoặc màn tiếp → hoàn thành danh sách màn → chơi lại từ đầu.

Bản cơ bản dùng lon BowlingCan làm mục tiêu bắt buộc. Target, portal, magnet và bounce wall được giữ tương thích; không bổ sung mechanic mới trong đợt này. Màn nghiệm thu cơ bản không phụ thuộc Target để thắng.

Các điều kiện bắt buộc:

- Một GameManager quyết định trạng thái màn, kết quả và Time.timeScale.
- Gameplay đọc LevelDefinition ScriptableObject; JSON là bản export và phục vụ LevelManager riêng. Không bật đồng thời hai manager điều khiển màn.
- Đạn không âm; một phát được chấp nhận chỉ trừ một viên khi thực sự spawn.
- Phát cuối còn đang bay hoặc còn phản ứng dây chuyền thì chưa được thua.
- Thắng được ưu tiên trước kiểm tra hết đạn. Kết quả chỉ chốt một lần.
- Retry xóa đạn, hủy phát đang chờ animation, dọn hiệu ứng của màn và reset mục tiêu.
- Chạm UI không đồng thời kéo/bắn cannon.
- Next chỉ hợp lệ ở trạng thái thắng; màn cuối có thông báo hoàn tất và nút chơi lại từ đầu.
- Màn trống hoặc dữ liệu lỗi có thông báo lỗi; không tính là chiến thắng.
- UI vẫn thao tác được khi timeScale bằng 0.
- Hoàn tất bằng kiểm tra Play Mode và các ca nghiệm thu ở mục 12, không chỉ bằng compile thành công.

Chưa đưa vào vòng này: shop, quảng cáo, tiền tệ, lưu tiến trình dài hạn, hệ thống sao, multiplayer, DI container, event bus, thay toàn bộ công cụ level hoặc đổi tên toàn bộ project.

## 2. Hiện trạng cần tiếp nhận

- Có GameManager, GameObjectManager, CannonController, CannonShooter, BowlingCan, GameFlowUI và LevelOneTutorial.
- GameManager và UI hiện dùng event/UnityEvent; refactor sang luồng ở mục 5 theo từng bước có tương thích.
- GameFlowUI có API cho HUD và panel riêng, nhưng bản scene đã rà soát còn thiếu một số reference. C phải đọc lại scene hiện tại trước khi gán.
- level_03 hiện có asset và JSON rỗng; chưa được coi là màn chơi hoàn chỉnh.
- Có pooling lon, âm thanh và VFX; ưu tiên giữ hành vi hoạt động rồi sửa vòng đời còn thiếu.
- Khi lập kế hoạch, checkout đang ở dev và LevelOneTutorial.cs có thay đổi chưa commit. C phải tiếp nhận thay đổi đó, không khôi phục bản HEAD đè lên.
- Trong lúc soạn tài liệu, repo xuất hiện thêm diff ở Bullet.cs, CannonShooter.cs và GameManager.cs. Đây là thay đổi đang diễn ra ngoài công việc viết kế hoạch; A/B phải đọc và tiếp nhận diff hiện tại trước khi bắt đầu, không áp bản refactor lên giả định HEAD sạch.
- Trạng thái repo có thể đổi sau tài liệu này. Mỗi agent phải đọc git status và diff ở đầu phiên, ghi nhận phần thay đổi đã tồn tại.

## 3. Phân vùng file — một file chỉ có một người sửa

Đường dẫn script dưới đây tính từ Assets/Project/Scripts/. Quyền sở hữu bao gồm file .meta đi kèm. Quyền đọc là toàn project. Quyền sửa chỉ theo bảng; file không được liệt kê phải được phân chủ trước khi sửa.

| Agent | Trách nhiệm | File/thư mục được sửa |
| --- | --- | --- |
| A | Luật chơi, trạng thái, load/retry/next, mục tiêu, pool lon, hợp đồng API | GameManager.cs; BowlingCan.cs; Target.cs; Levels/GameObjectManager.cs; Levels/LevelDefinition.cs; Levels/LevelManager.cs để giữ tương thích; thư mục mới CoreLoop/ |
| B | Input, ngắm, phát bắn, đạn, mechanic đạn, âm thanh/VFX runtime | CannonController.cs; CannonShooter.cs; CannonAnimationEventRelay.cs; Bullet.cs; BulletLifetime.cs; BulletImpactRouter.cs; BulletMotionSample.cs; BulletForceFieldReceiver.cs; BulletForceFieldRegistry.cs; BulletPortal.cs; BulletPortalPair.cs; BulletTrajectorySimulator.cs; BulletTrajectoryPreview.cs; TrajectoryDotRenderer.cs; TuongNay.cs; MagnetForceField.cs; SpawnTargetsOnBounce.cs; IBulletMechanic.cs; IBulletForceField.cs; IBulletTrajectoryRule.cs; AudioManager.cs; GameAudio.cs; VFX/*.cs |
| C | UI/tutorial, tích hợp scene/prefab, màn mẫu, công cụ dữ liệu | GameFlowUI.cs; LevelOneTutorial.cs; thư mục mới UI/ nếu tạo; Levels/LevelBoardBounds.cs; Levels/LevelGridUtility.cs; Levels/LevelObjectMarker.cs; Levels/LevelPrefabFootprint.cs; Levels/LevelPrefabCatalog.cs; Levels/Editor/*.cs |
| C | Tài nguyên serialized và cấu hình trình bày | Assets/Project/Scenes/Gameplay.unity; Assets/Project/Resoruce_game/Prefab/; Assets/Project/Resoruce_game/Art/UI/; Assets/Project/Data/Levels/; Assets/Project/Data/LevelPrefabCatalog.asset; Assets/StreamingAssets/DreamForgeTD/Levels/; Assets/Project/VFX/; Assets/Project/Resources/Audio/; Assets/Project/Audio/SoundSources.md; Assets/Project/Data/DataDrivenLevels.md |
| A | Hợp đồng và điều phối tài liệu | AGENTS.md gốc; Design/UIUX_Prototype/AGENTS.md; CoreLoop/API_V1.md và CoreLoop/BAN_GIAO_A.md dưới thư mục tài liệu này |
| B | Báo cáo riêng B | Design/UIUX_Prototype/CoreLoop/BAN_GIAO_B.md |
| C | Báo cáo riêng C và nghiệm thu chung | Design/UIUX_Prototype/CoreLoop/BAN_GIAO_C.md; Design/UIUX_Prototype/CoreLoop/NGHIEM_THU.md |

Các file CoreLoop/API_V1.md và BAN_GIAO_*.md được tạo khi triển khai; bảng này giữ chỗ quyền sở hữu, không khẳng định chúng đã tồn tại.

Các thư mục Tests mới cũng phải tách theo owner: Assets/Project/Tests/CoreLoop/A/, B/, C/. C sở hữu assembly definition chung nếu thực sự cần thêm; các agent không tự sửa cùng một asmdef.

**Ranh giới đặc biệt:**

- BowlingCan.cs thuộc A, dù có xử lý va chạm. B gửi yêu cầu điều chỉnh phản xạ/impulse cho A.
- B sở hữu code âm thanh/VFX; C sở hữu prefab, clip, material và binding trong scene. B ghi tham số cần gán, C thực hiện.
- A sở hữu schema LevelDefinition; C sở hữu nội dung asset/JSON và editor. Không đổi schema giữa đợt song song.
- Levels/LevelManager.cs thuộc A chỉ để xử lý tương thích khi cần; không mở rộng runtime JSON trong đợt này.
- Chỉ C lưu Gameplay.unity, prefab, catalog và level asset. A/B không mở và save scene để “sửa nhanh” Inspector.
- Chỉ C chạy thao tác Unity Editor có thể ghi serialized assets. Editor có thể tự sửa meta/material; C kiểm tra diff và chuyển phần ngoài quyền cho owner.
- Packages/, ProjectSettings/, .codex/, BlenderTarget/, Assets/Epic Toon FX/, Assets/Art/ và Scripts/Maps/ giữ nguyên phạm vi trong vòng cơ bản. Nếu thực sự cần thay đổi, A cập nhật phân quyền trước; không tự sửa do tiện tay.
- Không đổi tên/move file sang thư mục mới nếu việc đó thay đổi owner hoặc đòi sửa reference ở file người khác mà chưa bàn giao.

## 4. Quy tắc chống sửa chồng nhau

1. A chốt API v1 trước khi B/C sửa nơi gọi API. Khi API chưa có, B/C chỉ làm phần độc lập.
2. Mỗi người ghi task hiện tại và danh sách file sẽ sửa trong BAN_GIAO của chính mình. Không cùng cập nhật một checklist chung.
3. Muốn sửa file ngoài quyền: ghi path, lý do, thay đổi mong muốn và caller bị ảnh hưởng trong báo cáo của mình; gửi owner xử lý. Không tự sửa rồi báo sau.
4. Owner đang sửa thì các agent khác chỉ đọc. Chuyển chủ file phải có bàn giao rõ ràng và A cập nhật bảng trước khi người mới bắt đầu.
5. Không chạy formatter toàn repo, mass rename, auto-upgrade package hoặc regenerate asset toàn bộ.
6. Không dùng git add -A trong checkout có ba agent. Stage đúng path của mình, xem staged diff trước commit. Nếu dùng chung index, chỉ A thực hiện commit tích hợp sau khi mọi agent dừng ghi.
7. Không reset, clean, stash hoặc checkout đè thay đổi người khác. Không sửa cùng scene bằng cách hứa mỗi người sửa một GameObject: Unity vẫn ghi chung file.
8. Không push/force push/merge nhánh người khác theo suy đoán. Đợt lập kế hoạch này chưa yêu cầu triển khai code hoặc tạo ba phiên agent.
9. Nếu đã có worktree riêng được chỉ định, mỗi người ở đúng worktree của mình; không chuyển patch qua checkout khác bằng copy file. A tích hợp commit sau bàn giao theo quy trình đã thống nhất.
10. Trước commit cuối, mọi agent dừng ghi; A so danh sách file thực tế với bảng ownership và kiểm tra thay đổi có sẵn của người dùng vẫn còn.
11. Nếu phát hiện conflict, owner giải quyết file của mình bằng cách hiểu nội dung. Không tự chọn toàn bộ ours/theirs.
12. Ownership giảm xung đột file; API v1 và kiểm tra tích hợp xử lý xung đột hành vi. Không tuyên bố hết conflict chỉ vì Git merge được.

## 5. Kiến trúc đơn giản và yêu cầu “không callback”

### 5.1 Cách hiểu áp dụng

Core loop mới không dùng event bus, Action/Func truyền qua nhiều tầng, callback lồng nhau hoặc đăng ký/hủy event để quyết định thắng/thua. Dùng lời gọi phương thức trực tiếp và đọc trạng thái rõ ràng.

Unity vẫn cần các điểm vào như Awake, Update, OnCollisionEnter, Button.onClick và animation event. Giữ chúng ở lớp tiếp nhận input/va chạm/animation; từ đó gọi trực tiếp một phương thức có tên rõ nghĩa. Không lan truyền chuỗi callback nghiệp vụ. Đây là giới hạn kỹ thuật của yêu cầu, không phải cam kết loại bỏ cơ chế sự kiện nội bộ Unity.

Không tạo thêm hệ thống thay thế Button.onClick chỉ để “không callback”. Nút gọi một hàm của GameFlowUI; hàm gọi GameManager. Animation event chỉ xác nhận thời điểm spawn một phát đang chờ.

### 5.2 Hướng phụ thuộc

- C/UI → A/GameManager: gửi lệnh ChoiLaiMan, SangManTiep, ChoiLaiTuDau; đọc dữ liệu HUD.
- A/GameManager → B/CannonShooter: khóa/mở bắn, hủy phát chờ, reset ammo, dọn đạn; đọc số đạn và trạng thái phát bắn.
- A/GameManager → A/BowlingCan và GameObjectManager: đọc tiến trình mục tiêu, load và dọn màn.
- B/input → B/shooter → B/bullet/mechanic: thực hiện ngắm và bắn. B không tìm GameManager.Instance để tự kết luận thắng/thua.
- B có thể gọi phương thức mechanic trên BowlingCan qua hợp đồng va chạm sẵn có; thao tác đó cập nhật trạng thái lon, không phát event kết quả màn.
- C/LevelOneTutorial đọc mã lượt và số phát đã bắn; không subscribe LevelLoaded.
- UI không tự thay Time.timeScale, spawn level hoặc đếm collider làm điều kiện thắng.

GameManager là nơi điều phối duy nhất; không tạo một manager thứ hai song song. Chỉ tách helper khi một phần có trách nhiệm riêng thật sự, như kiểu dữ liệu trạng thái hoặc bước kiểm tra level. Không xây generic framework/state machine bằng nhiều class kế thừa cho sáu trạng thái đơn giản.

### 5.3 Dữ liệu và API dự kiến phải chốt ở cổng G0

A sở hữu enum TrangThaiMan và kiểu dữ liệu DuLieuLuotChoi trong Scripts/CoreLoop/. Kiểu dữ liệu chỉ chứa giá trị, không giữ reference scene hoặc delegate. UI đọc bản chụp chỉ đọc của GameManager.

| Chủ | API/thuộc tính | Ý nghĩa bắt buộc |
| --- | --- | --- |
| A | TrangThaiMan | DangTai, DangChoi, ChoKetQua, Thang, Thua, HoanTat, LoiTai |
| A | LayDuLieuLuotChoi() | Trả maLuot, phienBan, trangThai, chiSoMan, tenMan, soDanCon, soMucTieuCon, soPhatDaBan, thongBaoLoi |
| A | ChoiLaiMan() | Từ trạng thái chơi/đợi/kết quả/lỗi: nạp lại màn hợp lệ hiện tại; bỏ qua yêu cầu lặp khi đang tải |
| A | SangManTiep() | Chỉ chấp nhận từ Thang; màn cuối chuyển HoanTat |
| A | ChoiLaiTuDau() | Từ HoanTat hoặc lỗi không còn màn hiện tại: tải màn hợp lệ đầu tiên |
| B | DatChoPhepBan(bool) | Chặn cả input, Space, request shot và đường spawn; khóa thì hủy kéo/preview đang có |
| B | HuyLuotBanCho() | Xóa pending shot, reset trigger/charge; animation event cũ không sinh viên đạn |
| B | DonDanTrongMan() | Xóa toàn bộ đạn và trail thuộc lượt cũ; xóa bộ đếm/registry tương ứng |
| B | SoDanDangHoatDong | Số đạn còn có thể tương tác, không tính projectile trail đã tách để fade |
| B | RemainingBulletCount / HasPendingShot / ShotSequence / ResetAmmoForLevel() | Giữ API hiện có ở bước đầu, ghi rõ ánh xạ với thuật ngữ tiếng Việt |
| A | BowlingCan.DaBiHa / DangChuyenDong | Trạng thái chỉ đọc dùng cho đếm mục tiêu và đợi vật lý |
| A | GameObjectManager.ManagedObjects | Danh sách quản lý hiện có; dùng để lấy mục tiêu khi load, không FindObjects mỗi frame |

maLuot tăng mỗi lần bắt đầu tải/retry; phienBan tăng khi dữ liệu hiển thị thay đổi. C đọc snapshot trong Update nhưng chỉ ghi text/toggle panel khi phienBan đổi. Không tạo chuỗi, quét scene hay cấp phát danh sách mỗi frame.

Chữ ký trong bảng là **đề xuất cho đợt triển khai**, chưa tồn tại đầy đủ trong code. A ghi chữ ký chính xác vào API_V1.md, B/C xác nhận trong báo cáo riêng. Không để mỗi agent tự đoán signature.

### 5.4 Chuyển từ event cũ

- A chuyển GameManager sang đọc trạng thái lon; giữ tương thích cho LevelManager JSON nếu chưa chuyển đường đó.
- C chuyển GameFlowUI và LevelOneTutorial sang snapshot/lệnh trực tiếp sau khi API v1 có.
- B giữ các property public hiện có cho trajectory và animation, thêm gating và trạng thái đạn.
- Event cũ chỉ được xóa sau khi owner tìm toàn bộ caller, editor code và serialized UnityEvent binding. Những consumer ngoài core loop được giữ tương thích; không sửa tràn phạm vi để đạt mục tiêu “xóa mọi event”.
- Không đổi file/class MonoBehaviour cũ chỉ để Việt hóa. Giữ GUID .meta và binding script. Khi đổi field serialized phải có FormerlySerializedAs và C kiểm tra Inspector.
- Xóa legacy resultPanel/fallback và IMGUI cũ sau khi C hoàn thành binding panel mới, kiểm tra được các trạng thái và A xác nhận không còn consumer.

## 6. Máy trạng thái và thứ tự xử lý

| Trạng thái | Cho bắn | Điều kiện chuyển | Màn hình |
| --- | --- | --- | --- |
| DangTai | Không | Validate + spawn + reset thành công → DangChoi; lỗi → LoiTai | Đang tải, khóa nút thao tác |
| DangChoi | Có nếu còn đạn | Hết mục tiêu → Thang; hết đạn → ChoKetQua | HUD, Replay, tutorial nếu cần |
| ChoKetQua | Không | Hết mục tiêu → Thang; hết hoạt động vật lý và còn mục tiêu → Thua | HUD, báo đang chờ, Replay |
| Thang | Không | Next → tải màn tiếp hoặc HoanTat; Retry → DangTai | Panel thắng |
| Thua | Không | Retry → DangTai | Panel thua |
| HoanTat | Không | Chơi lại từ đầu → DangTai | Panel hoàn tất |
| LoiTai | Không | Retry hoặc về màn đầu hợp lệ | Thông báo lỗi đọc được |

Trình tự mỗi nhịp kiểm tra của A: bỏ qua nếu đang tải/kết thúc → cập nhật mục tiêu → ưu tiên thắng → đọc đạn/pending shot → quyết định đợi hoặc thua. Không lấy trạng thái của hai lượt khác nhau.

Điều kiện đợi vật lý:

- Hết đạn chưa đủ để thua. Phải không còn pending shot, không còn đạn có thể va chạm và các lon liên quan đã ổn định liên tục trong một khoảng thời gian.
- A/B dùng ngưỡng tốc độ và thời gian ổn định cấu hình được. Giá trị khởi đầu đề xuất: 0,1 đơn vị/giây, 0,1 radian/giây và 0,4 giây ổn định; phải đo lại trong scene.
- Bullet có tuổi thọ hữu hạn; out-of-bounds được dọn để portal/magnet không giữ màn vô hạn.
- Đồng hồ đợi dùng unscaled time. Thời hạn tối đa đề xuất 8 giây kể từ vào ChoKetQua; khi vượt hạn, A kiểm tra mục tiêu lần cuối, B dọn đạn, A đóng tương tác còn lại rồi mới chốt thua nếu còn mục tiêu.
- Thời hạn là phương án chống kẹt cần kiểm thử/tinh chỉnh, không là con số đã chứng minh cân bằng.
- Các lon đã bị hạ không bị đếm lại khi fade, disable hoặc trả pool. Việc dọn lon do Retry không được tạo chiến thắng cho lượt cũ.

Retry/Next phải đi qua cùng quy trình: khóa input → tăng maLuot → hủy pending shot và hiệu ứng charge → dọn đạn/trail → bỏ tham chiếu mục tiêu cũ → trả lon về pool → validate dữ liệu → spawn/reset → đưa timeScale về 1 → xuất snapshot → mở input. Validate toàn bộ dữ liệu và prefab trước khi hủy màn đang chơi nếu có thể.

## 7. Quy ước tên tiếng Việt dễ đọc

Dùng tiếng Việt không dấu cho tên mới; viết tắt chỉ ở prefix quen thuộc. Giữ tên engine/API chuẩn và class MonoBehaviour cũ để tránh đổi reference hàng loạt.

| Loại | Quy ước | Ví dụ |
| --- | --- | --- |
| Button field và GameObject mới | btn_ + snake_case | btn_choi_lai, btn_man_tiep, btn_ve_dau |
| TextMeshPro | txt_ + snake_case | txt_so_dan, txt_muc_tieu, txt_ten_man |
| Panel | pnl_ + snake_case | pnl_thang, pnl_thua, pnl_hoan_tat |
| Image | img_ + snake_case | img_nen_thang, img_bieu_tuong_thua |
| CanvasGroup | cg_ + snake_case | cg_ket_qua |
| Reference script | tên kiểu/chức năng dễ hiểu | quanLyMan, sungBan, huongDan |
| Biến dữ liệu | camelCase tiếng Việt | soDanCon, soMucTieuCon, maLuot, thoiGianOnDinh |
| Bool | câu hỏi/trạng thái | choPhepBan, daBiHa, dangChoKetQua |
| Hàm public | PascalCase động từ | ChoiLaiMan(), SangManTiep(), CapNhatHud() |
| Class/enum mới | PascalCase rõ nghĩa | DuLieuLuotChoi, TrangThaiMan |

- Không đặt tên kiểu qlm, sdc, xltt quá khó đoán; ưu tiên quanLyMan, soDanCon, XuLyTrangThai.
- Dùng nhất quán btn_choi_lai, không trộn Btn_retry, btnReplay và btn_choiLai cho cùng một chức năng mới.
- C thực hiện rename GameObject UI và sửa serialized field có migration trong một đợt riêng. Thay tìm button bằng tên bằng reference Inspector bắt buộc, có báo lỗi dễ hiểu khi thiếu.
- ID level/prefab như level_01, soda_can, portal_pair giữ nguyên. Không Việt hóa JSON key hoặc public API cũ hàng loạt.
- Namespace hiện tại DreamForgeTD được giữ.
- Comment giải thích lý do và điều kiện đặc biệt bằng tiếng Việt; tránh chú thích từng dòng hiển nhiên.
- Mỗi hàm có một nhiệm vụ; dùng guard clause, tên rõ nghĩa, tách validate/load/reset. Không tạo helper dùng một lần chỉ để giảm số dòng.

## 8. Công việc chi tiết của A — luật chơi và vòng đời

| Mã | Việc thực hiện | Bàn giao / điều kiện xong |
| --- | --- | --- |
| A0 | Đọc trạng thái repo, ghi file đang sửa; chốt schema không đổi trong đợt đầu | API_V1.md có chữ ký và ownership; B/C xác nhận |
| A1 | Thêm TrangThaiMan, DuLieuLuotChoi; tập trung quyền đổi trạng thái trong GameManager | Mọi kết quả đi qua một hàm; UI chưa cần tồn tại vẫn load được |
| A2 | Tách validate và quy trình load/reset dùng chung | Thiếu prefab, level rỗng, dữ liệu sai báo lỗi; không spawn nửa màn |
| A3 | Cho BowlingCan lộ DaBiHa/DangChuyenDong; GameManager đọc danh sách mục tiêu hiện tại | Không phụ thuộc event KnockedDown/Hidden cho core loop; không đếm trùng |
| A4 | Kết nối API B để quản lý input, đạn và ChoKetQua | Không thua sớm ở phát cuối; không bị kẹt khi đạn sống vô hạn |
| A5 | Làm Retry/Next/HoanTat và maLuot | Không nhận kết quả từ lượt cũ; nút spam không tạo hai load |
| A6 | Rà pool lon và Time.timeScale | Reset vật lý/fade/collider đúng; chơi lại luôn trở về tốc độ thường |
| A7 | Giữ đường LevelManager/Target ngoài core loop tương thích | Không đổi nghĩa scene cũ do xóa event thiếu caller |
| A8 | Dọn event/logic legacy đã hết consumer; cập nhật báo cáo | Không còn hai nguồn quyết định kết quả trong Gameplay |

A không sửa projectile mechanic, scene hoặc UI để giải quyết lỗi tích hợp; gửi yêu cầu đúng owner. Khi B thay đổi quy tắc phản xạ lon, A thực hiện phần trong BowlingCan và B kiểm tra phối hợp.

## 9. Công việc chi tiết của B — ngắm, bắn, vật lý, phản hồi

| Mã | Việc thực hiện | Bàn giao / điều kiện xong |
| --- | --- | --- |
| B0 | Xác nhận API v1; rà input, Space, pending shot và animation relay | Liệt kê tất cả đường có thể spawn đạn |
| B1 | Thêm một cổng choPhepBan dùng chung cho mọi đường bắn | Không bắn lúc đang tải, hết đạn, thắng/thua hoặc khi khóa |
| B2 | Sửa việc bắn qua animation để mỗi request chỉ sinh một viên | Event animation lặp, đến muộn sau Retry hoặc đã hủy đều vô hiệu |
| B3 | Bổ sung theo dõi đạn đang hoạt động và dọn đạn theo lượt | SoDanDangHoatDong về 0 khi bullet chết, disable hoặc bị dọn |
| B4 | Rà lifetime/out-of-bounds, portal, magnet, bounce và trajectory | Không giữ màn vô hạn; đường preview không còn khi bị khóa |
| B5 | Phối hợp C chặn pointer bắt đầu trên UI, xử lý hủy kéo khi mất focus | Click Replay/Next không đồng thời bắn; chuột và touch thống nhất |
| B6 | Sửa vòng đời charge/muzzle/trail và âm thanh cần thiết | Retry không để lại trail/charge; một phát không phát SFX hai lần |
| B7 | Gửi C bảng component/field/giá trị cần gán cho prefab | Không tự lưu prefab; báo cáo có tham số và cách kiểm tra |
| B8 | Dọn phần subscribe/callback thuộc đường bắn mới khi hết consumer | API rõ, không phụ thuộc GameManager từ tầng vật lý |

B dùng collection nhỏ theo vòng đời bullet; không gọi FindObjectsByType mỗi Update. Registry phải có reset khi bắt đầu phiên Play, không đếm đối tượng đã destroy, không trừ hai lần khi vừa cleanup vừa OnDisable. Không mở rộng pooling bullet nếu chưa cần cho vòng cơ bản.

## 10. Công việc chi tiết của C — UI, scene và nội dung

| Mã | Việc thực hiện | Bàn giao / điều kiện xong |
| --- | --- | --- |
| C0 | Tiếp nhận diff LevelOneTutorial hiện có; rà scene và ghi thiếu reference | Không mất thay đổi người dùng; danh sách binding cụ thể |
| C1 | Chuyển GameFlowUI sang đọc snapshot và gọi lệnh trực tiếp | Hết subscribe kết quả màn cho core loop; render khi phienBan đổi |
| C2 | Nối HUD đạn/mục tiêu/tên màn và ba panel kết quả | Mỗi trạng thái đúng panel; parent của Next được bật đúng |
| C3 | Đổi tên UI theo btn_/txt_/pnl_; gán reference thay tìm theo tên | Không còn dựa vào tên cũ để nút hoạt động; giữ migration field |
| C4 | Tutorial đọc maLuot/soPhatDaBan; giữ các tinh chỉnh hiện có | Hiện ở level_01; fade sau phát đầu; Retry reset; không chặn input |
| C5 | Safe area, anchors, animation unscaled và nút khi timeScale=0 | UI đọc được và bấm được ở tỉ lệ portrait thử nghiệm |
| C6 | Gán component và field A/B yêu cầu trong Gameplay/prefab | Một manager runtime; không thêm LevelManager JSON cạnh GameManager |
| C7 | Hoàn thiện ba màn mẫu có lon, quota và độ khó vừa phải | level_03 không còn rỗng hoặc được bỏ khỏi danh sách chơi một cách đồng bộ |
| C8 | Đồng bộ SO, JSON, manifest và GameManager list qua editor | Không có hai nội dung khác nhau cho cùng ID sau khi save |
| C9 | Chạy ca nghiệm thu tích hợp, ghi bằng chứng và lỗi về owner | NGHIEM_THU.md nêu pass/fail, điều kiện chạy, lỗi còn lại |

Đề xuất nội dung ba màn: level_01 vài lon và hướng bắn trực tiếp để dạy input; level_02 thêm tường nảy; level_03 dùng một portal hoặc magnet đã có. C kiểm tra khả năng thắng thật rồi mới chốt. Không đưa cơ chế mới vào để làm màn khó hơn. Quota riêng theo màn đã được người dùng duyệt: `LevelDocument.startingBulletCount` dương là quota của level, `0` kế thừa mặc định ở CannonShooter. A sở hữu schema và GameManager; B cần triển khai `IGioiHanDanTheoMan.NapDanTheoMan(int)` trong CannonShooter; C cần giữ trường này khi lưu và cho phép cấu hình trong Level Editor. Luồng gameplay dùng GameManager/LevelDefinition áp quota; LevelManager JSON riêng hiện chưa áp dụng trường này.

Menu Level Editor: Tools → DreamForge → Level Editor. Thao tác Save có thể tự cập nhật GameManager trong scene; vì vậy chỉ C vận hành editor này khi ba agent cùng làm.

## 11. Thứ tự triển khai và cổng bàn giao

### G0 — Chốt hợp đồng trước khi làm song song

A ghi API_V1.md và dựng kiểu dữ liệu/API tối thiểu trong file A sở hữu. A/B thống nhất điều kiện đạn còn hoạt động và ngưỡng ổn định; C xác nhận các trường HUD đủ dùng. Cả ba ghi xác nhận trong báo cáo riêng. Chưa có G0 thì không viết caller vào API giả định.

### G1 — Ba nhánh công việc độc lập

A thực hiện A1–A3 và bộ khung load/reset. B thực hiện B1–B4. C làm layout, reference, đổi tên có migration và adapter UI theo API đã chốt. Các API cũ còn consumer được giữ cho tới tích hợp. Mỗi phần bàn giao phải compile khi ghép với hợp đồng v1.

### G2 — Nối một vòng chơi tối thiểu

A nối B để một level đơn giản đi được từ tải → bắn → thắng/thua → Retry. C gán reference trên một scene tích hợp duy nhất. Chưa đánh bóng VFX hoặc cân bằng ba màn khi bước này còn lỗi.

### G3 — Hoàn thiện nội dung và trải nghiệm

C hoàn thiện màn 2/3, Next/HoanTat, safe area và tutorial. B xử lý sound/VFX bị lặp hoặc còn sót. A xử lý lỗi vòng đời, đợi vật lý, dữ liệu không hợp lệ và timeScale.

### G4 — Dọn refactor và nghiệm thu

Tìm consumer còn lại rồi xóa adapter/event cũ trong phạm vi cho phép; không xóa trước. C chạy ma trận kiểm thử. A rà ownership, static state và luồng chuyển màn. B rà lifetime/pending shot. Chỉ đánh dấu hoàn tất khi các ca bắt buộc đều đạt hoặc có quyết định phạm vi rõ ràng của người dùng.

### G5 — Đóng đợt làm việc

Mọi người dừng ghi, bàn giao file và kết quả. Người tích hợp kiểm tra diff, giữ thay đổi có sẵn, tạo commit theo scope khi được yêu cầu. Scene/assets có commit riêng để review. Không coi việc push thành công là bằng chứng core loop đã đúng.

## 12. Ma trận nghiệm thu

Đây là kế hoạch kiểm tra cho đợt triển khai, chưa phải các ca đã chạy.

| Mã | Tình huống | Kết quả yêu cầu | Chủ sửa lỗi chính |
| --- | --- | --- | --- |
| T01 | Mở Gameplay lần đầu | Tải màn hợp lệ, quota đúng, HUD hiện, input hoạt động | A + C |
| T02 | Kéo/thả một lần, animation phát event lặp | Chỉ một viên đạn, trừ một đạn, một SFX bắn | B |
| T03 | Phát cuối còn đang bay | Chưa hiện thua | A + B |
| T04 | Phát cuối hạ lon cuối qua phản ứng dây chuyền | Hiện thắng một lần, không lóe panel thua | A |
| T05 | Hết đạn và còn mục tiêu sau khi ổn định | Hiện thua, input bắn bị khóa, Retry dùng được | A + C |
| T06 | Retry trong lúc đạn bay/pending animation | Đạn cũ và phát chờ bị dọn, không ảnh hưởng lượt mới | A + B |
| T07 | Retry sau thắng/thua 10 lần | Ammo/mục tiêu/pool đúng; không tăng số manager hoặc callback | A |
| T08 | Click nút khi con trỏ nằm gần cannon | Không kéo hoặc sinh đạn ngoài ý muốn | B + C |
| T09 | Next sau màn 1, 2 và màn cuối | Đúng thứ tự; kết thúc có Chơi lại từ đầu | A + C |
| T10 | Nhấn Retry/Next nhanh liên tiếp | Chỉ một lần tải hợp lệ, không nhân đôi object | A |
| T11 | Tạm dừng thời gian ở kết quả | UI và animation hoạt động, Retry trả timeScale về 1 | A + C |
| T12 | Level rỗng/thiếu prefab/thiếu reference | Thông báo lỗi rõ, không thắng giả hoặc null spam | A + C |
| T13 | Đạn mắc portal/magnet hoặc ra ngoài board | Có đường cleanup hữu hạn, màn không đợi vô hạn | B + A |
| T14 | Play → Stop → Play, kể cả tắt domain reload nếu project dùng | Static registry/pool/count không mang dữ liệu cũ | A + B |
| T15 | Portrait 9:16 và màn dài có safe area | Không che nút/HUD, vùng kéo vẫn đủ | C |
| T16 | Tutorial level_01, Retry rồi sang level_02 | Không chặn chạm; reset đúng; màn 2 không có tutorial | C |
| T17 | Save và mở lại level trong editor | Placement, footprint, portal endpoints và data hai dạng khớp | C |
| T18 | Thắng trong khi lon đang fade/trả pool | Không đếm hai lần; không tác động level kế tiếp | A |

Ưu tiên test logic thuần cho các chuyển trạng thái phát cuối, thắng ưu tiên thua và lệnh không hợp lệ nếu tách được mà không tạo framework test phức tạp. Các lỗi scene, serialized reference, touch và animation cần kiểm tra Play Mode thực tế. Chỉ ghi pass khi đã chạy; thiếu môi trường ghi chưa kiểm tra cùng lý do.

## 13. Mẫu báo cáo bàn giao cho từng agent

Mỗi agent chỉ sửa file BAN_GIAO của mình, theo các mục:

- Vai trò, task ID, branch/worktree hiện tại, commit nền.
- File đã sửa và file/meta mới.
- API đã dùng/cung cấp; thay đổi so với API v1 nếu có.
- Giá trị Inspector hoặc bước tích hợp cần owner khác thực hiện.
- Ca đã kiểm tra, kết quả, lỗi còn lại; nêu rõ ca chưa chạy.
- Thay đổi người dùng đã có trước và cách đã giữ lại.
- Yêu cầu ngoài phạm vi: path, owner, nội dung cần sửa.
- Trạng thái: đang làm / chờ API / chờ tích hợp / đã bàn giao.

Không tự cập nhật báo cáo người khác. A tổng hợp từ ba báo cáo để cập nhật kế hoạch; C cập nhật NGHIEM_THU sau khi chạy tích hợp.

## 14. Lệnh giao việc có thể dùng cho ba phiên

**Giao A:** Đọc AGENTS.md gốc và Design/UIUX_Prototype/AGENTS.md. Nhận vai trò A, bắt đầu A0/G0, chốt API v1 rồi thực hiện phần A. Chỉ sửa file thuộc A, giữ diff có sẵn, báo cáo vào BAN_GIAO_A.md. Chưa có API v1 thì ưu tiên hợp đồng trước refactor rộng.

**Giao B:** Đọc hai AGENTS.md, nhận vai trò B. Rà B0 và làm phần độc lập trong phạm vi B; chỉ viết API tích hợp sau G0. Không sửa BowlingCan, GameManager, scene hoặc prefab; ghi yêu cầu cho A/C vào BAN_GIAO_B.md.

**Giao C:** Đọc hai AGENTS.md và DataDrivenLevels.md, nhận vai trò C. Tiếp nhận diff LevelOneTutorial, rà C0 và các reference UI; dùng API v1 sau G0. Là owner duy nhất cho Gameplay, prefab và level data. Ghi kết quả vào BAN_GIAO_C.md và NGHIEM_THU.md. Tuân thủ quy tắc Unity MCP ở AGENTS.md gốc; kế hoạch này không tự bật kết nối MCP.
