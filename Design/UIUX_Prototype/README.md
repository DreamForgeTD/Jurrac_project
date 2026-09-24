# UI/UX prototype — Direction 01

Tài liệu và ảnh trong thư mục này là concept để chốt hướng giao diện trước khi dựng UI trong Unity. Không có code hay scene nào được sửa.

## Đã xem trong project

- `Assets/Project/ARCHITECTURE.md` mô tả puzzle vật lý mobile chơi dọc: kéo ngược để ngắm, thả để bắn; đạn tương tác với chướng ngại và mục tiêu.
- `Assets/Project/Scripts/UI/GameplayUI.cs` hiện có HUD số lượt, hai trạng thái thắng/thua và một nút Retry.
- `Assets/Project/Scenes/SampleScene.unity` chưa gắn `GameplayUI`; scene hiện là mẫu hình học/Canvas cơ bản. Vì vậy concept dựa trên luồng gameplay trong tài liệu, không phải ảnh chụp scene đang chạy.
- `ProjectSettings/ProjectSettings.asset` đang để `defaultScreenOrientation: 4` (Auto Rotation), trong khi tài liệu dự án định hướng portrait. Các trường kích thước mặc định hiện là 1024 × 768.

## Hướng giao diện

- Giữ nền hồng chuyển sắc đã có; dùng cyan từ material hiện tại làm điểm nhấn, chữ và vật cản màu mận đậm, panel trắng hồng để bảo đảm độ tương phản.
- Bố cục portrait 9:16, co giãn theo safe area của máy. Chừa phần lớn màn hình cho playfield; HUD số lượt nằm phía trên, chỉ dẫn kéo ngắm ở mép dưới. Không đặt nút lên vùng người chơi cần chạm và kéo.
- Khi thắng hoặc hết lượt, dùng một panel gọn với một CTA duy nhất: `CHƠI LẠI`. Không thêm pause, menu hay Next Level vì prototype hiện chưa có các luồng đó.

## Button kit

- Nút label dùng shape bo góc gọn, màu vàng cam; chữ mận đậm, vùng chạm tối thiểu 48 dp.
- Trạng thái nhấn dùng Color Tint; disabled giảm alpha.
- Copy chuẩn trên tất cả nút: `CHƠI LẠI`. Cả hai panel kết quả dùng cùng một button component và cùng hành vi Retry.

## Refine màu và phong cách

Bộ sprite hiện tại chuyển sang cartoon puzzle casual phẳng hơn: không viền, không bóng; ba màu vàng, aqua và xanh lá nổi trên nền hồng. Có thêm nút bắn tròn với icon viên đạn. Ảnh concept board phía trên là hướng bố cục ban đầu; PNG mới nằm tại `Assets/Project/Resoruce_game/Art/UI/Buttons/`.

## Brief tạo concept

Mockup giao diện puzzle đại bác mobile portrait, bám nền hồng và accent cyan hiện có; thể hiện HUD số lượt, playfield thoáng với đường ngắm, hai panel kết quả thay thế nhau và nút chơi lại ở ba trạng thái. Không giả định thêm cơ chế hoặc màn hình ngoài scope hiện tại.

## Giới hạn

Ảnh PNG là concept board để tham khảo bố cục và style, không phải UI asset đã tách layer, sprite hoặc 9-slice. Chữ do công cụ sinh có thể chưa chính xác tuyệt đối; lấy copy chuẩn trong tài liệu này (`CÒN 3 LƯỢT`, `KÉO ĐỂ NGẮM`, `THẮNG RỒI!`, `HẾT LƯỢT`, `CHƠI LẠI`) khi dựng UI sau này.
