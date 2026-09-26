# UI/UX prototype — Direction 01

**Cập nhật:** 26-09-2026
**Scene gameplay tham chiếu:** Assets/Project/Scenes/Gameplay.unity

README này ban đầu ghi lại concept UI trước khi dựng trong Unity. Kể từ lần cập nhật này, project đã có thay đổi ở script, prefab, dữ liệu và scene gameplay. Phần dưới phân biệt hướng thiết kế ban đầu với những gì hiện có trong project và những chỗ cần nối tiếp.

## Trạng thái và phạm vi

- Hướng hình ảnh chính vẫn là puzzle đại bác casual trên màn hình dọc, nền hồng, điểm nhấn aqua/cyan, các vật thể màu sáng và chữ dễ đọc.
- Scene đã được cập nhật để chạy gameplay cùng Canvas, luồng thắng/thua, tutorial đầu màn và Audio Manager.
- Công việc trong đợt này còn bao gồm level editor, footprint prefab, dữ liệu level, pooling lon và cập nhật prefab/VFX.
- README mô tả trạng thái mã nguồn và Inspector đã kiểm tra; đây không phải biên bản xác nhận đã Play Mode test trên thiết bị.

## Hướng UI/UX

### Bố cục và màu sắc

- Dùng bố cục portrait 9:16, Canvas tham chiếu 1080 × 1920 và chừa phần lớn diện tích cho playfield.
- Giữ nền hồng chuyển sắc; dùng cyan/aqua làm điểm nhấn, màu mận đậm cho chữ và vật cản, panel sáng để duy trì tương phản.
- HUD nằm ngoài vùng ngắm chính; hướng dẫn thao tác ở gần khu vực đại bác.
- Khi thiết kế nút, giữ vùng chạm đủ lớn cho thao tác di động. Kiểm tra lại padding, font và safe area ở nhiều tỉ lệ màn hình trước khi chốt.

### Trạng thái gameplay

- Trong lúc chơi, người chơi kéo để ngắm và thả để bắn. Scene có nút replay riêng để khởi động lại màn hiện tại.
- Khi thắng level, luồng UI gọi sang level kế tiếp. Khi thua, nút Retry khởi động lại level.
- Nếu hoàn thành toàn bộ danh sách level, GameManager phát sự kiện AllLevelsCompleted; GameFlowUI hiển thị trạng thái kết thúc mà không bật nút Next.
- Copy và hình ảnh trên các PNG concept chỉ là tham khảo. Chỉnh lại nội dung chữ trong Unity để tránh lỗi chữ do công cụ tạo ảnh.

## Những phần đã được triển khai

### Scene, Canvas và luồng UI

- Gameplay.unity hiện có Canvas với CanvasScaler dùng Scale With Screen Size và reference resolution 1080 × 1920.
- GameFlowUI được gắn trên Canvas. Script đăng ký sự kiện với GameManager và CannonShooter, nhận thay đổi level, kết quả thắng/thua và số đạn.
- Khi bật, GameFlowUI chuyển GameManager sang luồng UI thủ công: tắt HUD IMGUI cũ và tắt tự động chuyển level để thao tác qua Canvas.
- Các nút trong scene được nhận diện theo tên:
  - Btn_replay: khởi động lại level đang chơi.
  - Btn_Next: chuyển sang level kế tiếp khi thắng.
  - Btn_retry: khởi động lại sau khi thua.
- Panel kết quả có hiệu ứng hiện nền và phóng logo/nút từ 72% lên kích thước gốc trong khoảng 0,38 giây. Animation dùng unscaled time nên vẫn chạy khi gameplay slow motion hoặc tạm dừng.
- Có thể gắn ParticleSystem tùy chọn cho hiệu ứng thắng; GameFlowUI chuẩn bị và dừng particle cùng trạng thái panel.
- UI được nối qua event và listener trong script; nút được tháo listener khi component bị disable để tránh đăng ký lặp.

#### Các tham chiếu UI cần hoàn thiện trong scene

- GameFlowUI có trường bulletCountText, nhưng Gameplay.unity hiện chưa gán TextMeshPro vào trường này. CannonShooter đã phát event số đạn; HUD số đạn chỉ hiện khi trường được nối.
- Các trường winBackground, loseBackground, winLogo và loseLogo chưa được gán trong scene. Component hiện còn dùng legacyResultPanel làm nền dự phòng chung; hãy nối từng panel và logo riêng để trạng thái thắng/thua dùng đúng hình.
- Scene có object bg_wingame riêng và Btn_Next nằm trong nhánh UI của scene. Hãy kiểm tra liên kết parent/active state của panel này cùng với winBackground trước khi xác nhận màn hình thắng hoạt động.
- LoseCurrentLevel có API trên GameManager để bật luồng thua, nhưng hiện chưa thấy gameplay script nào gọi API này. Cần nối điều kiện thua đã chọn vào API đó.
- Canvas có reference resolution dọc; trong thay đổi này chưa có xử lý inset safe area riêng. Cần kiểm tra vùng tai thỏ và tỉ lệ màn hình khi tinh chỉnh layout.

### Tutorial level đầu

- LevelOneTutorial được gắn trên Canvas và chỉ hiển thị cho level có ID level_01.
- Overlay được tạo lúc chạy: một lớp spotlight làm tối phần còn lại, mũi tên hướng ngắm, hình bàn tay kéo và dòng hướng dẫn.
- Vị trí spotlight và hướng dẫn theo vị trí cannon; overlay cập nhật khi kích thước màn hình thay đổi.
- Canvas tutorial dùng sorting order 100; CanvasGroup không chặn raycast để người chơi vẫn kéo cannon được.
- Overlay mờ dần và ẩn sau phát bắn đầu tiên. Fade kéo dài 0,45 giây; hoạt ảnh bàn tay dùng chu kỳ kéo 0,72 giây.
- Tutorial lắng nghe event LevelLoaded, tự hiện lại khi level_01 được tải lại và ẩn ở các level khác.

### Artwork UI

- Đã thêm bốn PNG hình nút vào Assets/Project/Resoruce_game/Art/UI/Buttons/ cùng metadata Unity tương ứng.
- Đây là ảnh bitmap nguồn để tham khảo/tích hợp vào UI. Nếu dùng cho nút co giãn, cần thiết lập Sprite Mode, pivot và 9-slice trong Unity; file PNG tự nó chưa định nghĩa các vùng co giãn.
- Giữ các ảnh concept board làm tài liệu tham khảo bố cục. Chúng không thay thế việc kiểm tra kích thước chữ, độ tương phản, anchor và vùng chạm trong Canvas.

### Âm thanh

- Gameplay.unity có object Audio Manager đang bật. AudioManager giữ nhạc qua lần đổi scene, phát nhạc nền lặp và quản lý nhiều one-shot source để các hiệu ứng va chạm có thể phát chồng nhau.
- Scene đã gán trực tiếp nhạc nền và các clip gameplay; AudioManager cũng có thể tải clip từ Resources làm phương án dự phòng.
- Các cue được nối gồm nhạc nền, hiệu ứng bắt đầu level, click nút, bắn đại bác, nảy, va chạm vật cản, va chạm lon, portal vào/ra và trúng mục tiêu.
- Các hiệu ứng cartoon được sao chép vào Assets/Project/Resources/Audio/SFX/ từ thư viện Epic Toon FX có sẵn trong project. Nhạc nền BackgroundMusic.mp3 và nguồn cùng giấy phép của các clip được ghi trong Assets/Project/Audio/SoundSources.md.
- SoundSources.md có thông tin nguồn Kenney CC0, OpenGameArt và mapping tên clip gốc sang clip dùng trong game. Giữ tài liệu đó đồng bộ khi thay clip hoặc thêm âm thanh.

### Gameplay, level và editor

#### Luồng level đang dùng trong Gameplay

- GameManager trên scene đọc danh sách LevelDefinition ScriptableObject được gán trong Inspector và dùng LevelPrefabCatalog để tìm prefab theo prefabId.
- Danh sách level trên GameManager hiện gồm level_01, level_02 và level_03; loadOnStart đang bật và bắt đầu tại phần tử đầu tiên.
- GameManager đếm BowlingCan trong level. Khi lon được xác nhận hạ hoặc ẩn, số mục tiêu còn lại giảm; lon cuối cùng kích hoạt thắng và slow motion.
- CannonShooter quản lý số đạn, ghi nhận chuỗi phát bắn và phát âm thanh/VFX khi bắn. GameFlowUI dùng event số đạn để cập nhật HUD khi đã gán TextMeshPro.
- Không trộn luồng này với LevelManager đọc JSON. Gameplay scene hiện chạy GameManager với LevelDefinition asset; LevelManager và manifest JSON trong StreamingAssets là một luồng riêng.

#### Dữ liệu level và lưới

- Level Editor hiện dùng board 18 cột × 32 hàng, tương ứng lưới portrait 1080 × 1920. Khi mở dữ liệu 9 × 16 cũ, editor ánh xạ vị trí và footprint sang lưới mới, giữ tâm vật thể và giới hạn placement trong board.
- Palette editor hỗ trợ soda_can, target, cannon, bounce_wall, portal_pair, magnet và erase; portal đặt bằng hai ô Entry/Exit. Editor có chế độ chọn vật thể đang đặt để chỉnh góc mà không đổi vị trí.
- LevelPrefabFootprint mới lưu số ô mà prefab chiếm. Editor dùng footprint khi đánh dấu vùng bận, kiểm tra chồng lấn, xoay, lưu dữ liệu và dựng preview.
- Footprint hiện tại: Cannon 1 là 3 × 3; SodaCan_330ml là 1 × 1; Target là 2 × 2; Wall_bounce là 5 × 3; Magnett là 3 × 3; mỗi đầu PortalPair là 2 × 2. Footprint chỉ mô tả vùng ô trong editor, không tự thay đổi mesh hoặc collider.
- level_01 có lưới 18 × 32 và 16 object trong JSON: 14 soda_can, một magnet và một portal_pair. Cannon chiếm footprint 3 × 3.
- levels.json hiện liệt kê level_01, level_02, level_03. level_03 đã có cả LevelDefinition asset và JSON file; hai bản đều đang để trống object để làm level khởi đầu.
- Level Editor lưu LevelDefinition asset và JSON export, đồng thời cập nhật manifest. Dữ liệu JSON được LevelManager đọc theo đường dẫn StreamingAssets; dữ liệu ScriptableObject được GameManager của Gameplay scene đọc trực tiếp.

#### Pooling lon và cập nhật prefab/VFX

- GameObjectManager có pool runtime riêng cho prefab chứa BowlingCan. Khi reset level, lon được đưa về pool thay vì hủy; khi spawn lại, trạng thái vật lý, collider, coroutine và event được reset.
- BowlingCan phát event khi bị hạ, làm mờ trước khi ẩn rồi thử trả object về pool. Nếu không tìm được pool phù hợp, object được hủy theo đường dự phòng.
- Các prefab Cannon 1, SodaCan_330ml, Target, Wall_bounce, Magnett và PortalPair được cập nhật cùng footprint metadata và cấu hình scene hiện tại.
- Prefab FX_Cannon_MuzzleFlash và các material Epic Toon FX được cập nhật serialized settings trong cùng đợt asset. Sound và VFX được gọi từ các điểm bắn, va chạm và UI liên quan.

## Tệp tham khảo trong project

- Assets/Project/Scripts/GameFlowUI.cs — nối HUD, panel kết quả và button vào event gameplay.
- Assets/Project/Scripts/LevelOneTutorial.cs — overlay hướng dẫn cho level_01.
- Assets/Project/Scripts/AudioManager.cs và Assets/Project/Scripts/GameAudio.cs — quản lý và gọi âm thanh.
- Assets/Project/Scripts/Levels/Editor/LevelEditorWindow.cs — công cụ tạo/sửa level trong Unity Editor.
- Assets/Project/Scripts/Levels/LevelPrefabFootprint.cs và Assets/Project/Scripts/Levels/LevelGridUtility.cs — footprint và phép đổi tọa độ lưới.
- Assets/Project/Data/DataDrivenLevels.md — hướng dẫn chi tiết level, editor, footprint và hai luồng level.
- Assets/Project/Audio/SoundSources.md — nguồn, giấy phép và tên clip âm thanh.

## Ghi chú khi tiếp tục UI

1. Gán TextMeshPro vào bulletCountText và kiểm tra vị trí HUD ở tỉ lệ portrait khác nhau.
2. Gán riêng winBackground, loseBackground, winLogo và loseLogo; kiểm tra trạng thái active của bg_wingame và parent của Btn_Next.
3. Chọn và gọi GameManager.LoseCurrentLevel từ điều kiện thua của gameplay.
4. Kiểm tra safe area, kích thước nút và khả năng đọc chữ trên thiết bị.
5. Kiểm tra lại PNG dưới dạng Sprite trong Unity trước khi dùng làm button skin; tạo 9-slice nếu thiết kế cần co giãn.
6. Chạy thử scene để xác nhận panel, nút, audio, tutorial và luồng chuyển level trước khi coi UI là hoàn tất.
