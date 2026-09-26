# Kiểm tra đợt tối ưu — 26-09-2026

## Đã thực hiện

- Biên dịch runtime và editor bằng compiler Unity 6000.0.78f1, với references/defines hiện có của project. Output tách vào thư mục tạm; không chạy/mở Unity Editor bằng MCP.
- Đối chiếu vùng vẽ spotlight qua 24 trường hợp: kết quả thuật toán khớp cách quét toàn texture; kiểm tra cả thu nhỏ, di chuyển, mép ảnh và thay kích thước.
- Rà caller sau khi bỏ event GameManager, event inactive của Bullet, event số projectile của CannonShooter và interface quota tạm. Callback còn consumer JSON/Inspector được giữ.
- Rà đường FX: muzzle, charge, trail, impact, bounce, victory, portal và FX custom của lon đều đi qua GameVfx. FX UI gắn sẵn tái sử dụng instance scene.

## Chưa kiểm tra trong Play Mode/Profiler

| Ca | Kết quả cần xác nhận |
| --- | --- |
| Bắn vào 48 lon, bao tường quanh map | Phản ứng dây chuyền và tốc độ/lực không đổi; FX tiếp tục phát đủ |
| Lặp va chạm sau khi pool đã có instance | Giảm Instantiate/Destroy FX; không còn quét lại particle hierarchy mỗi lần phát |
| Retry giữa phát chờ hoặc khi đạn bay | Đạn/phát chờ/FX lượt cũ hết; ammo và tutorial reset |
| Đạn tự hết hạn | Trail tách khỏi đạn, hạt còn lại mờ tự nhiên rồi trả pool |
| Retry sau khi lon fade nhiều lần | Lon trở lại vật liệu gốc; không tăng material clone qua mỗi lượt |
| Next, thắng/thua, hết danh sách màn | HUD và animation panel đúng, UI hoạt động khi timeScale = 0 |
| Play → Stop → Play, domain/scene reload tắt | Không giữ FX hoạt động hoặc đăng ký pool từ phiên trước |
| Resize cửa sổ khi tutorial đang hiện | Vòng sáng không để lại vùng sáng cũ |

Chưa có số đo FPS/CPU/GC thực tế; compile thành công không thay thế các ca Play Mode này.

APK trong `APK/Canon_shooter.apk` được tạo trước đợt cập nhật prefab/material VFX và cấu hình Mobile URP mới nhất. Cần build lại APK rồi chạy các ca trên thiết bị Android trước khi dùng làm bản nộp cuối.
