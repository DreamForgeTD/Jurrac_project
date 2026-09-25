# Báo cáo tiến độ dự án

> Tài liệu theo dõi tiến độ và các điểm cần kiểm tra của project Unity. Cập nhật theo lịch sử Git và mã nguồn trong workspace; khi có thay đổi mới, thêm mục vào **Nhật ký cập nhật** và điều chỉnh **Việc cần xác minh**.

## 1. Ảnh chụp trạng thái hiện tại

- **Ngày ghi nhận:** 26/09/2026
- **Nhánh đang làm việc:** `dev`
- **Commit hiện tại trên `dev`:** `2b0c1c1` — merge nhánh `agent/codex3`.
- **Quan hệ nhánh:** `dev` đã nhận các commit của `agent/codex2` và `agent/codex3`; `origin/dev` vẫn ở `1e59530`, `dev` ahead 9 commit tại thời điểm ghi nhận. `main` chưa được cập nhật.
- **Phạm vi cập nhật:** merge hai nhánh, xử lý xung đột, rà soát và bổ sung mechanic nam châm. Chưa chạy Play Mode hay test tự động.
- **Trạng thái working tree:** có sửa đổi chưa commit cho mechanic nam châm; ngoài ra còn có `Wall_1.fbx`/meta bị thiếu trong working tree và các asset can/stand chưa được theo dõi. Những asset này được giữ nguyên, không đưa vào merge commit.
- **Unity MCP:** Editor đang chạy nhưng endpoint `http://127.0.0.1:8080/mcp` không phản hồi, nên chưa đọc được Unity Console. Đã biên dịch toàn bộ C# qua Roslyn đi kèm Unity 6 bằng response file của project; compile thành công. Chưa chạy Play Mode.

Các mục “đã có trong mã nguồn” bên dưới xác nhận rằng implementation tồn tại ở commit được rà soát; chúng **không đồng nghĩa** với việc đã được kiểm chứng đầy đủ trong runtime.

## 2. Tóm tắt tiến độ

Project có vòng gameplay cơ bản xoay quanh cannon, đạn, va chạm, portal, target và preview quỹ đạo. Đã merge thêm luồng level JSON/catalog, audio và VFX từ hai nhánh agent. Đang bổ sung force field nam châm dùng chung phép tính cho đạn thật và preview. Gameplay vẫn cần được kiểm tra trực tiếp trong Unity, nhất là cảm giác kéo/bắn, cấu hình scene/prefab và độ khớp giữa preview với đường đạn thật.

## 3. Các phần đã được triển khai

### 3.1. Nền tảng gameplay và scene

- Có scene `Assets/Project/Scenes/SampleScene.unity` cùng các prefab phục vụ cannon, bullet, portal, target và vật cản bật nảy.
- Lịch sử dự án từng bổ sung cấu hình map, quản lý trạng thái/reset màn chơi, vật cản di chuyển và các tương tác như bounce, boost, gate, switch, goal, hazard, teleport.
- Kiến trúc script đã được tổ chức lại qua các commit sau đó. Một số lớp thuộc cấu trúc `Launcher`/`Projectile` và nhóm `Interactions` ban đầu đã được thay thế hoặc chuyển sang luồng `Cannon`/`Bullet` hiện tại; cần dựa trên HEAD hiện tại khi tiếp tục phát triển.
- Scene, cannon prefab, target, portal và asset giao diện đã được cập nhật qua lịch sử commit.

### 3.2. Kéo cannon, ngắm và tích lực

Trong `CannonController.cs` hiện có:

- Nhận thao tác chuột và cảm ứng; bắt đầu kéo khi chạm trong vùng kích hoạt quanh cannon.
- Lực được tính từ khoảng kéo xuống, chuẩn hóa thành tỷ lệ `0..1`, rồi nội suy từ `minForce` tới `maxForce`. Giá trị mặc định trong mã hiện tại đặt `minForce = 0`.
- Góc ngắm có làm mượt khi đang kéo; khi nhả tay, góc dùng vị trí con trỏ cuối cùng.
- Có ngưỡng kéo tối thiểu trước khi yêu cầu bắn. Vì vậy mức lực thấp nhất có thể gần 0, nhưng vẫn cần kéo qua ngưỡng này để phát sinh phát bắn.
- Có giới hạn khoảng kéo, góc ngắm và tốc độ xoay để hiệu chỉnh trong Inspector.

**Cần kiểm tra trong runtime:** lực tăng đều từ mức thấp nhất khi kéo nhẹ, lực đạt giới hạn khi kéo tối đa, thao tác ngang điều chỉnh hướng đúng, và cảm giác kéo đủ mượt trên các độ phân giải/tốc độ khung hình mục tiêu.

### 3.3. Bắn đạn và animation event

Trong `CannonShooter.cs` và `CannonAnimationEventRelay.cs` hiện có:

- Lưu lực, vị trí và hướng của phát bắn đang chờ.
- Nếu có Animator/controller, kích hoạt trigger animation; `OnCannonFire` chuyển animation event về `CannonShooter`, nơi tạo viên đạn tại thời điểm event.
- Nếu không có Animator/controller, đường đi dự phòng gọi xử lý bắn ngay.
- Sau khi tạo đạn, shooter ghi lại thông tin phát bắn gần nhất và tăng `ShotSequence`; có giới hạn nhịp bắn bằng `fireRate`.
- Vị trí đầu nòng được tính dựa trên các collider của cannon để tránh sinh đạn bên trong thân súng.
- Lịch sử Git ghi nhận animation controller/clip bắn và cập nhật cannon prefab/scene.

**Cần kiểm tra trong runtime:** event được đặt đúng frame, mỗi lần nhả chỉ sinh một viên, không mất phát bắn khi animation bị ngắt, và animation giữ đúng transform/scale theo setup trong prefab/clip.

### 3.4. Mô phỏng và hiển thị preview đường đạn

Trong `BulletTrajectoryPreview.cs`, `BulletTrajectorySimulator.cs` và `TrajectoryDotRenderer.cs` hiện có:

- Preview cập nhật theo lực và hướng khi đang kéo.
- Simulator bước theo thời gian, có tính trọng lực, collider bán kính viên đạn, collision mask, collision skin và giới hạn số tương tác.
- `IBulletTrajectoryRule` cho phép mechanic mô tả phản hồi quỹ đạo; preview có thể tiếp tục, dừng hoặc dịch chuyển theo response mà mechanic trả về.
- Khi có phát bắn mới, preview dùng vị trí/hướng/lực đã ghi nhận của phát đó và khóa đường preview. Khi bắt đầu kéo lượt tiếp theo, preview quay lại cập nhật động.
- Hình hiển thị là các sprite tròn dùng sprite/material riêng `TrajectoryDot.png` và `TrajectoryDot.mat`; renderer tái sử dụng pool sprite thay vì tạo/hủy lại toàn bộ chấm ở mỗi frame.
- Độ mờ giảm dần dọc đường đi; kích thước chấm giảm nhẹ về phía xa. Khoảng cách chấm tăng theo tỷ lệ lực từ `dotSpacing` đến `maxDotSpacing`.

**Cần kiểm tra trong runtime:** material/sprite hiển thị đúng trên camera và sorting layer, preview không bị che hoặc biến mất, đường đã bắn được giữ đến lần kéo tiếp theo, và preview khớp với va chạm/portal thực tế.

### 3.5. Luồng mechanic cho va chạm, portal và target

- `IBulletMechanic` tách xử lý gameplay khi đạn va chạm khỏi `BulletImpactRouter`.
- `IBulletTrajectoryRule` tách dự đoán quỹ đạo khỏi xử lý va chạm thật, để mechanic có thể tham gia preview mà không buộc simulator chứa logic riêng cho từng loại vật thể.
- `BulletPortal`/`BulletPortalPair` xử lý cặp portal, hướng thoát, vị trí/tốc độ dự đoán; code hiện tại có hiệu ứng co/bung portal và pool cho tác vụ chuyển đạn.
- `SpawnTargetsOnBounce` là ví dụ mechanic độc lập: tạo target sau va chạm bật nảy, có offset và tùy chọn chỉ spawn một lần.

Thiết kế interface tạo điểm mở rộng cho mechanic mới. Khả năng thêm/xóa mechanic mà không ảnh hưởng luồng khác vẫn cần được xác nhận bằng kiểm thử tích hợp trong scene.

### 3.6. Asset và package

- Có asset model/material/texture cho cannon, bullet, portal, target, nền, nút giao diện và trajectory dot.
- `Packages/manifest.json` khai báo package Unity MCP từ `CoplayDev/unity-mcp`, cùng các package như Input System, URP và PrimeTween. Việc package có trong manifest không tự xác nhận trạng thái kết nối MCP của Unity Editor tại thời điểm chạy.

### 3.7. Lực hút nam châm — đã thêm code, chờ xác minh trong Editor

- Sửa `BulletMotionSample` và `IBulletForceField` (namespace/tên type và lỗi khai báo ban đầu).
- Thêm registry đăng ký các force field đang bật; `MagnetForceField` tính gia tốc hút theo bán kính, falloff mượt và giới hạn lực. Mặc định chiếu lực lên mặt phẳng XY.
- Thêm `BulletForceFieldReceiver` lên bullet prefab để cộng gia tốc trong `FixedUpdate` bằng `ForceMode.Acceleration`.
- `BulletTrajectorySimulator` gọi cùng registry ở từng bước mô phỏng để preview cong theo lực hút.
- Gắn `MagnetForceField` vào prefab `Magnett` trong catalog.
- **Giới hạn hiện tại:** `SampleScene`, `level_01` và `level_02` chưa đặt prefab nam châm. Hiệu ứng chỉ chạy ở level có instance của `Magnett`.
- C# compile kiểm tra bằng Unity Roslyn với toàn bộ source trong response file hiện có cộng ba script force-field mới: thành công, output được ghi ở thư mục tạm.
- **Chưa xác minh:** prefab import trong Unity Editor và độ khớp giữa đường đạn thật với preview trong Play Mode.

## 4. Nhật ký cập nhật theo Git

| Ngày | Commit | Mốc công việc |
|---|---|---|
| 23/09/2026 | `0912422` | Khởi tạo lịch sử repository và cấu hình package Unity ban đầu. |
| 24/09/2026 | `b0988c7` | Thêm scene và prefab nền tảng cho cannon, bullet, portal; bắt đầu luồng bắn, bật nảy và target. |
| 24/09/2026 | `6b44347` | Thêm các lớp quản lý level, cấu hình, reset, projectile/launcher và các tương tác gameplay ban đầu. |
| 25/09/2026 | `d58475d` | Mở rộng gameplay projectile/portal; thêm router va chạm, lifetime, interface mechanic/quỹ đạo, map data và asset cho màn chơi. |
| 25/09/2026 | `6b14854` | Cập nhật asset/scene, target và cách cấu hình hướng thoát portal; thêm asset nút giao diện. |
| 25/09/2026 | `8d08e69` | Chỉnh cannon/target, prefab và scene; tiếp tục cập nhật logic kéo, bắn và preview. |
| 26/09/2026 | `1e59530` | Thêm firing animation/event relay, chia simulator và renderer preview, thêm sprite/material chấm đường đạn; cập nhật cannon prefab, scene và manifest package. |
| 26/09/2026 | `5e236a8` | Gộp nhánh `agent/codex2`: thêm level flow theo JSON, prefab catalog, level manager và cartoon VFX texture packs. |
| 26/09/2026 | `2b0c1c1` | Gộp nhánh `agent/codex3`: thêm audio/VFX runtime và asset; xử lý xung đột `Target.cs` để giữ event level cùng hiệu ứng thắng, giữ một khai báo MCP ghim `v10.2.0`. |
| 26/09/2026 | working tree | Thêm force field nam châm, receiver trên bullet, đồng bộ simulator và gắn script vào prefab. Unity Roslyn compile thành công; chưa commit và chưa kiểm tra runtime. |
| 26/09/2026 | — | Tạo/cập nhật `PROJECT_PROGRESS.md` để ghi nhận tiến độ và danh sách xác minh tiếp theo. Chưa commit báo cáo. |

| 26/09/2026 | `d34766e` / `5095704` | Push sửa mechanic nam châm lên `dev`; merge và push model lon/đế cùng texture từ `agent/codex1`; bổ sung các FBX/material lon dùng trong project. Chưa cấu hình prefab/màn bowling hoặc xác minh import/play mode trong Unity. |

| 26/09/2026 | `0846469` | Sửa texture lon/chân đế: gán PNG vào `_BaseMap`/`_MainTex`, remap hai FBX tới đúng material và theo dõi các `.meta` để GUID giữ ổn định. Unity đã reimport model/material theo Editor log; chưa xác minh ảnh viewport vì Unity MCP chưa kết nối. |

## 5. Việc cần xác minh tiếp theo

1. Chạy scene trong Unity và kiểm tra kéo nhẹ, kéo tối đa, kéo ngang, nhả nhanh, nhả ngoài vùng màn hình; xác nhận lực thực bắt đầu từ 0 và tăng liên tục.
2. Kiểm tra input chuột/cảm ứng và độ mượt ngắm ở nhiều độ phân giải; đảm bảo không tự xoay cannon khi chưa bắt đầu kéo nếu đó là hành vi mong muốn.
3. Xác nhận trajectory dot sprite/material, alpha fade, kích thước và khoảng cách chấm dễ nhìn; kiểm tra preview được giữ sau khi bắn.
4. So sánh preview với đường đạn thật khi chạm collider, bật nảy và đi qua portal; kiểm tra nhiều lần va chạm và trường hợp đạn kết thúc.
5. Kiểm tra animation event sinh đúng một viên đạn tại frame dự kiến; xác nhận transform, scale và chuyển động cannon khớp setup trong prefab/animation.
6. Mở scene/prefab để xác nhận các serialized reference (bullet prefab, fire point, sprite, material, Animator, relay, portal endpoint) đều được gán hợp lệ.
7. Thêm `Magnett` vào level thử nghiệm; kiểm tra lực hút chỉ có tác dụng trong bán kính, mượt ở rìa vùng, và preview khớp với bullet thật.
8. Kiểm tra Unity Console sau khi MCP kết nối lại và chạy Play Mode QA; hiện báo cáo này chưa ghi nhận kết quả runtime.

## 6. Mẫu ghi tiến độ cho lần cập nhật sau

```text
### YYYY-MM-DD — [tiêu đề ngắn]
- Nhánh/commit:
- Đã làm:
- File/scene/asset liên quan:
- Đã xác minh:
- Chưa xác minh hoặc vấn đề còn lại:
```
