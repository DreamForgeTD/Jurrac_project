# Cannon Shooter

## Ý tưởng game

Mình làm game giải đố vật lý trên điện thoại, chơi theo chiều dọc. Người chơi kéo để ngắm và chỉnh lực pháo, rồi thả tay để bắn. Đạn trúng lon làm lon đổ và va vào các lon khác. Mỗi màn giới hạn số đạn: hạ hết lon thì thắng; hết đạn mà vẫn còn lon sau khi mọi thứ dừng lại thì thua. Màn đầu có gợi ý ngắn về cách kéo-thả.

## Gameplay thêm ngoài ngắm-bắn

- **Tường bật nảy:** đổi hướng đạn; người chơi chọn góc bắn để vòng qua vật cản.
- **Portal:** chuyển đạn giữa một cặp cổng sau khoảng chờ ngắn.
- **Nam châm:** hút đạn và làm đổi quỹ đạo.

Các mechanic này tạo thêm lựa chọn về đường đi và thứ tự bắn. Scene `Gameplay.unity` đang gán 5 màn. `level_06.asset` chỉ là bản nháp trống, chưa nằm trong chuỗi chơi.

## Các quyết định quan trọng

- Giữ phạm vi ở 5 màn ngắn, tập trung hoàn thiện vòng ngắm, bắn, quan sát phản ứng vật lý rồi thắng hoặc thử lại.
- Lưu màn bằng `LevelDefinition` và ánh xạ prefab qua `LevelPrefabCatalog`, để chỉnh bố cục mà không viết lại gameplay.
- Tách tương tác đạn thành `IBulletMechanic`, `IBulletTrajectoryRule` và `IBulletForceField`, để thêm vật cản, portal hoặc lực hút mà không gom hết luật vào một script.
- Dùng pool cho lon và VFX vì chúng được tạo lại thường xuyên khi va chạm. Chưa đo trên máy nên chưa kết luận mức giảm cấp phát hay tăng FPS.

## Kỹ thuật và hiệu năng

- Đã sửa kiểm tra portal exit để chỉ áp dụng cho object portal; object thường không còn làm level lỗi vì thiếu dữ liệu portal.
- Lon giữ Rigidbody kinematic lúc xếp màn để các collider sát/chồng nhau không tự đẩy lệch cả hàng. Khi bị đạn hoặc phản ứng dây chuyền chạm, lon được kích hoạt vật lý. Cách này chưa được xác nhận trong Play Mode.
- Runtime và Editor scripts đã compile offline bằng Roslyn của Unity. Chưa chạy Play Mode hoặc Profiler; chưa có số đo FPS, CPU, GPU hay GC. Cần kiểm tra màn va chạm dày, Retry khi đạn/trail còn hoạt động, pool lặp và Play/Stop khi tắt domain reload.
- APK [`APK/Canon_shooter.apk`](APK/Canon_shooter.apk) là ARM64 nhưng được tạo trước các cập nhật VFX và Mobile URP mới nhất. Cần build lại và thử trên thiết bị Android; workspace chưa có thiết bị/emulator để chạy hoặc profile. Package ID hiện vẫn là `com.UnityTechnologies.com.unity.template.urpblank`.
- Video gameplay 1-2 phút chưa được thêm vào repository.

## AI và công cụ

AI coding assistant hỗ trợ rà soát/refactor code và viết tài liệu. Các script Python cho Blender tự động hóa một số asset thử nghiệm. Unity Level Editor dùng để sửa dữ liệu màn; Unity Roslyn dùng để compile offline. Mình vẫn cần kiểm tra trực tiếp trên thiết bị trước khi xác nhận chất lượng bản nộp.

## Nếu có thêm 24 giờ

1. **Build và profile trên Android:** hiện chưa biết màn nặng nhất có giật hoặc tốn bộ nhớ không. Kết quả cần có là một APK khớp source hiện tại, số đo thực tế và bottleneck cụ thể để xử lý.
2. **Cho người mới chơi thử phút đầu:** kiểm tra họ có tự hiểu cách ngắm, lực bắn và điều kiện thắng/thua không. Dùng kết quả để chỉnh thao tác và gợi ý, giúp người chơi vào game nhanh hơn.
3. **Chơi thử cả 5 màn:** xem bounce, portal và nam châm có tạo lựa chọn khác nhau, có được giới thiệu dễ hiểu không. Sau đó chỉnh thứ tự hoặc bố cục để tiến trình rõ hơn.

## Nếu chỉ có 24 giờ

Giữ thao tác ngắm-bắn, phản ứng dây chuyền của lon, ít nhất 5 màn, các mechanic bật nảy/portal/nam châm và luồng thắng-thua-thử lại. Ưu tiên build lại, kiểm tra gameplay và sửa lỗi chặn người chơi. Bỏ thêm mechanic, nội dung mới và polish hình ảnh trước.
