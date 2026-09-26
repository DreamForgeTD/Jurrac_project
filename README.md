# Cannon Shooter

Đây là một game giải đố vật lý trên điện thoại, chơi theo chiều dọc. Kéo để ngắm và chỉnh lực bắn của khẩu pháo, rồi thả tay để bắn. Mỗi màn có số đạn giới hạn. Đạn làm lon đổ và va vào nhau theo hiệu ứng dây chuyền. Hạ hết lon để thắng; nếu hết đạn mà vẫn còn lon sau khi đạn và lon dừng chuyển động thì thua. Màn 1 có gợi ý ngắn về thao tác kéo-thả.

## Gameplay và tiến trình

Scene `Gameplay.unity` đang gán 5 màn qua `GameManager` và các asset `LevelDefinition`. `level_06.asset` hiện là bản nháp trống trong editor, chưa nằm trong chuỗi màn chơi. Ngoài ngắm và bắn, game có:

- **Tường bật nảy:** đổi hướng đạn để người chơi chọn đường bắn quanh vật cản.
- **Cặp portal:** chuyển đạn giữa cổng vào và cổng ra sau một khoảng chờ ngắn.
- **Nam châm:** hút và làm cong quỹ đạo đạn.

Các mechanic này làm thay đổi vị trí và đường đi cần chọn cho mỗi phát bắn. Level Editor lưu bố cục thành dữ liệu và dùng `LevelPrefabCatalog` để ánh xạ ID sang prefab, nhờ đó có thể thêm bố cục mà không phải viết lại vòng chơi chính.

## Các quyết định chính

- Giữ phạm vi ở 5 màn được thiết kế và một vòng chơi chính: ngắm, bắn, quan sát vật lý, chơi tiếp hoặc thử lại.
- Dùng asset `LevelDefinition` và prefab catalog làm nguồn dữ liệu màn chơi của `Gameplay.unity`.
- Tách tương tác đạn thành các interface về cơ chế, quy tắc quỹ đạo và trường lực để mechanic mới có thể tham gia gameplay và phần xem trước đường đạn.
- Tái sử dụng lon và đưa VFX vào pool để giảm việc tạo-hủy object lặp lại. Đây là quyết định triển khai, chưa phải kết quả đo hiệu năng.

## Kỹ thuật và hiệu năng

- Bản Android: [`APK/Canon_shooter.apk`](APK/Canon_shooter.apk), ARM64. APK đã được tạo nhưng chưa chạy trên thiết bị Android thật trong workspace này.
- APK hiện tại được tạo trước đợt cập nhật prefab/material VFX và cấu hình Mobile URP gần nhất; cần build lại trước khi dùng làm bản nộp cuối.
- Không có thiết bị Android hoặc emulator kết nối để profile. Chưa có số đo frame time, CPU, GPU hay bộ nhớ. Rủi ro hiệu năng chính cần đo là số lượng va chạm vật lý và lượng hạt VFX trên màn nặng nhất. Pool lon và VFX đã có, nhưng mức cải thiện cần được đo trên thiết bị thật.
- Package ID của APK hiện tại là `com.UnityTechnologies.com.unity.template.urpblank`; cần thay bằng ID sản phẩm cuối trước khi phát hành nếu yêu cầu.
- Repository chưa có video gameplay dài 1-2 phút.

## AI và công cụ

Unity là game engine; các script Python cho Blender tự động hóa việc tạo asset thử nghiệm. AI coding assistant hỗ trợ rà soát, refactor code và chuẩn bị tài liệu. Thay đổi được đối chiếu với code hiện có, nhưng chưa kiểm tra gameplay và hiệu năng trên thiết bị Android.

## Nếu có thêm 24 giờ

1. Profile màn nặng nhất trên điện thoại Android phù hợp và xử lý bottleneck frame time hoặc bộ nhớ theo số đo.
2. Cho người mới chơi thử phút đầu; điều chỉnh độ nhạy thao tác kéo, phản hồi khi bắn và độ rõ của trạng thái thắng/thua dựa trên chỗ họ gặp khó.
3. Điều chỉnh tiến trình 5 màn để người chơi hiểu rồi phối hợp các quyết định bật nảy, portal và nam châm.

Nếu chỉ còn 24 giờ, giữ thao tác ngắm-bắn, vật lý dễ đọc, 5 màn, các mechanic bật nảy/portal/nam châm và luồng thắng-thua-thử lại ổn định. Cắt phần trang trí hình ảnh và nội dung bổ sung trước.
