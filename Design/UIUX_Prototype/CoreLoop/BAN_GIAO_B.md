# Bàn giao Agent B — đạn và cổng bắn

## Trạng thái

- Vai trò: Agent B; hỗ trợ B1/B3 cho yêu cầu hết đạn và chờ viên cuối xử lý xong.
- Nhánh: `dev`; HEAD lúc rà soát: `a03a5b4`.
- Trạng thái: theo dõi đạn và hợp đồng quota theo màn thuộc B đã cập nhật; chờ C hoàn tất cấu hình/lưu quota trong Level Editor và chờ kiểm tra runtime.

## File thuộc B đã sửa

- `Assets/Project/Scripts/Bullet.cs`: báo khi viên đạn bị disable hoặc destroy.
- `Assets/Project/Scripts/CannonShooter.cs`: giữ bộ đếm đạn hiện có; cả `RequestShotWithForce` và `ShootWithForce` đều từ chối bắn khi số đạn bằng 0. Theo dõi projectile đang hoạt động, bỏ khỏi bộ đếm khi projectile ngừng hoạt động, và dọn projectile khi reset ammo. Triển khai `IGioiHanDanTheoMan.NapDanTheoMan(int)` để nạp quota của level mà không đổi quota mặc định đã serialize trên prefab.

## API và tích hợp

- `RemainingBulletCount` tiếp tục là nguồn dữ liệu đạn còn lại.
- `StartingBulletCount` giữ quota mặc định từ Inspector; `NapDanTheoMan` đặt quota lượt hiện tại và gọi `ResetAmmoForLevel()`. `BulletCountChanged` gửi tổng quota lượt hiện tại để UI dùng đúng số.
- `GameManager.cs` thuộc Agent A và đã gọi `NapDanTheoMan` trong diff hiện tại. Các API theo dõi trạng thái core loop rộng hơn vẫn chờ `API_V1.md`/G0.
- `GameFlowUI.cs`, HUD text và panel Lose thuộc Agent C. B không sửa UI hoặc serialized scene.
- Agent C cần thêm cấu hình `startingBulletCount` vào Level Editor và giữ giá trị đó khi lưu LevelDefinition; asset có giá trị `0` tiếp tục kế thừa mặc định của prefab.

## Xác minh

- Đã rà tĩnh các đường `RequestShotWithForce` và `ShootWithForce`; không đường nào spawn đạn khi `RemainingBulletCount <= 0`.
- Đã đối chiếu chữ ký `NapDanTheoMan(int soDan)` với `IGioiHanDanTheoMan`; chưa chạy Play Mode để xác nhận level dùng quota khác 20.
- Chưa chạy Play Mode hoặc test runtime. Các ca cần tích hợp gồm T03, T04, T05, T06, T13 và T14 trong kế hoạch A/B/C.
- Các thay đổi khác đang có trong working tree được giữ nguyên.

## TuongNay — hoạt ảnh khi đạn va chạm

- `Assets/Project/Scripts/TuongNay.cs`: khi nhận va chạm đạn hợp lệ, phóng nhẹ phần visual rồi đưa về tỷ lệ ban đầu. Hiệu ứng kéo dài 0,18 giây, nở tối đa 8%, dùng unscaled time; va chạm mới khởi động lại hoạt ảnh. Khi component bị tắt, tỷ lệ visual được khôi phục.
- `heSoBatNay` là hệ số tốc độ sau phản xạ, mặc định `1.5` (nhanh hơn 50%) và có thể chỉnh trên Inspector. Cả phản xạ vật lý và `PredictTrajectory` dùng chung hàm để giữ đường ngắm khớp vận tốc thực tế.
- Field visual `phanHinhAnhNay` (`Transform`) cần trỏ tới child chỉ chứa model, không có Collider. C đã gán child `Wall_1` trong working tree; thay đổi prefab này chưa nằm trong commit B.
- Khi C lưu binding prefab, xác nhận Inspector hiển thị `heSoBatNay = 1.5`; B không sửa prefab.
- Nếu field chưa được gán, component ghi cảnh báo và bỏ qua hoạt ảnh; cơ chế bật nảy vẫn hoạt động. `BulletImpactRouter` tiếp tục phát VFX va chạm; âm thanh bounce được giữ một lần mỗi va chạm.
- Chưa chạy Play Mode; cần kiểm tra tốc độ sau bật nảy, đường dự đoán và pulse trên prefab.

## Bàn giao phối hợp — animation kết thúc màn (phần Agent B)

### Phần B đã có sẵn

- `CannonShooter.HuyLuotBanCho()` hủy shot đang chờ, reset trigger bắn và dừng charge VFX. Animation event đến muộn không thể phát lại shot đã hủy.
- `CannonShooter.DonDanTrongMan()` gọi hủy shot chờ rồi dọn projectile đang hoạt động. Trail bị xóa hạt ngay; projectile bị `SetActive(false)` trước `Destroy` để collider/physics ngừng ngay trong frame, tránh đạn cũ lọt sang level mới. Trail của projectile hết hạn theo vòng đời bình thường vẫn giữ cách fade hiện có.
- `ResetAmmoForLevel()` tiếp tục dọn shot/projectile cũ khi nạp quota mới.

### Thứ tự phối hợp khi kết thúc level

1. **A** nhận kết quả thắng, khóa cannon và input trước khi bắt đầu chuyển cảnh.
2. **A** gọi `DonDanTrongMan()` đúng một lần. Không cần gọi riêng `HuyLuotBanCho()` trước vì hàm dọn đạn đã gọi nó.
3. **C** chạy animation presentation trên nội dung level cũ. Theo yêu cầu hiện tại, từng object còn lại trôi về cạnh màn hình gần nhất theo vị trí trên camera rồi biến mất; không đẩy toàn bộ managed root cùng một hướng. HUD đứng yên và panel Win ẩn khi bắt đầu Next.
4. **A** chỉ xóa/nạp level mới sau khi animation báo hoàn tất; giữ cannon khóa trong suốt khoảng chuyển. Mở lại input sau khi level mới và quota đã được nạp.

Để giữ luồng dễ theo dõi và không thêm callback nghiệp vụ, C nên cung cấp cho A một lệnh bắt đầu presentation cùng trạng thái hoàn tất để A chờ/đọc trực tiếp. C không tự gọi `LoadLevel`. Nếu sau này cần animate cả cannon, C và A cần thống nhất root của cannon trước; hiện cannon không nằm dưới managed root của object level.

### Ranh giới file và nghiệm thu của B

- **B** chỉ duy trì hành vi hủy shot/projectile trong `CannonShooter.cs` và `Bullet.cs`; không triển khai animation map/UI, không tự điều phối NextLevel.
- **A** sở hữu `GameManager.cs`, `GameObjectManager.cs`, khóa/mở input và thời điểm clear/spawn level.
- **C** sở hữu `GameFlowUI.cs`, scene/prefab và animation presentation.
- Cần nghiệm thu hai thời điểm: thắng khi projectile cuối còn bay và bắt đầu Next khi shot đang chờ animation event. Kết quả yêu cầu: không có shot muộn, projectile/trail/collider từ level cũ không tồn tại trong level mới, và chỉ load level mới sau khi presentation kết thúc.
- Luồng `NextLevel() → LoadLevel() → LoadDefinition()` hiện vẫn clear/spawn đồng bộ; chưa có animation/độ trễ chuyển màn được tích hợp.
- Chưa chạy Play Mode; phần B đã có API dọn đạn nhưng chưa được kiểm tra trong transition end-to-end.

## Hiệu ứng khi thắng màn — bàn giao cho C

- `FX_Target_VictoryBurst` là VFX lúc từng `Target` nhận đạn: `Target.OnBulletHit()` gọi `GameVfx.PlayTargetVictory()` → slot `targetVictory` trong `Assets/Resources/VFX/GameVfxLibrary.asset` → prefab `Assets/Project/VFX/Prefabs/FX_Target_VictoryBurst.prefab`. Đây là hiệu ứng trúng mục tiêu, không phải animation hoàn thành level.
- Khi hết lon, `GameManager` phát `LevelWon`; C-owned `GameFlowUI.HandleLevelWon()` hiện panel và gọi `AnimateResult()`. Code hiện có lần lượt fade/scale nền, logo rồi nút bằng unscaled time. Trong diff scene hiện tại, `winBackground`, `winLogo` và `winButton` đã được gán; `winParticleEffect` đang để trống.
- Runtime/prefab VFX mục tiêu là phần B có sẵn; không có thay đổi B nào cho animation panel thắng. `GameFlowUI.cs`, scene, prefab UI và mọi animation full-screen mới thuộc C; C gán các Image/ParticleSystem/visual reference cần dùng trong scene hoặc prefab. Luồng chuyển level sau presentation cần phối hợp A như phần “Thứ tự phối hợp khi kết thúc level” bên trên.
- Chưa chạy Play Mode để kiểm tra animation hoặc binding UI.
