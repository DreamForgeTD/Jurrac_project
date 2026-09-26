# Cannon Shooter

## Ý tưởng game

Đây là một game được lấy ý tưởng từ vài game cannon shot trên mobile kết hợp với game giải đố và liên tưởng tới sự lan truyền của bowling, vật lý trên điện thoại, chơi theo chiều dọc. Người chơi kéo để ngắm và chỉnh lực pháo, rồi thả tay để bắn. Đạn trúng lon làm lon đổ và va vào các lon khác. Mỗi màn giới hạn số đạn: hạ hết lon thì thắng; hết đạn mà vẫn còn lon sau khi mọi thứ dừng lại thì thua. Màn đầu có gợi ý ngắn về cách kéo-thả.

## Gameplay thêm ngoài ngắm-bắn

- **Tường bật nảy:** đổi hướng đạn; người chơi chọn góc bắn để vòng qua vật cản.
- **Portal:** chuyển đạn giữa một cặp cổng dịch chuyển sau khoảng chờ ngắn.
- **Nam châm:** hút đạn và làm đổi quỹ đạo của đường đạn

Các mechanic này tạo thêm lựa chọn về đường đi và thứ tự bắn. Scene `Gameplay.unity` đang gán 5 màn. 

## Các quyết định quan trọng
- Xác định scope gameplay đơn giản chỉ kéo thả đơn giản.
- Giữ phạm vi ở 5 màn ngắn, tập trung hoàn thiện vòng ngắm, bắn, quan sát phản ứng vật lý rồi thắng hoặc thử lại.
- Lưu màn bằng `LevelDefinition` và ánh xạ prefab qua `LevelPrefabCatalog`, để chỉnh bố cục mà không viết lại gameplay.
- Tách tương tác đạn thành `IBulletMechanic`, `IBulletTrajectoryRule` và `IBulletForceField`, để thêm vật cản, portal hoặc lực hút mà không gom hết luật vào một script.
- Dùng pool cho lon và VFX vì chúng được tạo lại thường xuyên khi va chạm.

## Kỹ thuật và hiệu năng

- Đã sửa kiểm tra portal exit để chỉ áp dụng cho object portal; object thường không còn làm level lỗi vì thiếu dữ liệu portal.
- Lon giữ Rigidbody kinematic lúc xếp màn để các collider sát/chồng nhau không tự đẩy lệch cả hàng. Khi bị đạn hoặc phản ứng dây chuyền chạm, lon được kích hoạt vật lý.
- Runtime và Editor scripts đã compile offline bằng Roslyn của Unity.
- Vấn đề kĩ thuật đáng chú ý đó chính là tối ưu VFX bằng việc dùng object pooling, khi không sử dụng pooling thì sẽ gây ra GC cao tích luỹ liên tục và crash game cũng như hiệu năng thấp.

## AI và công cụ

- Sử dụng hệ thống agent Codex Luna 6 MAX, Astra 6, Anigravity cùng Gemnini web để tets gameplay ý tưởng nhanh

## Nếu có thêm 24 giờ

1. **Build và profile trên Android:** Phải tối ưu hiệu năng thêm để tăng cảm giác felling chạm vuốt mượt mà.
2. **VFX**: Các hiệu ứng vfx chưa được đẹp và cần cải thiện bổ sung cũng như tối ưu partical tốt hơn.
3. **Cho người mới chơi thử phút đầu:** kiểm tra họ có tự hiểu cách ngắm, lực bắn và điều kiện thắng/thua không. Dùng kết quả để chỉnh thao tác và gợi ý, giúp người chơi vào game nhanh hơn.
4. **Chơi thử cả 5 màn:** xem bounce, portal và nam châm có tạo lựa chọn khác nhau, có được giới thiệu dễ hiểu không. Sau đó chỉnh thứ tự hoặc bố cục để tiến trình rõ hơn.

## Nếu chỉ có 24 giờ

Giữ coregameplay chính là shooter và đổ nghiêng ngả của lon nước, chỉ thêm 1 mechanic và tập trung thiết kế để có thể ưu tiên dễ dàng mở rộng và tối ưu sau này.
