# Unity Mobile Physics Puzzle

Triển khai cho Unity 6000.0.75f1, physics **3D trên mặt phẳng XY**, màn hình portrait.
Namespace: `DreamForgeTD.PhysicsPuzzle`. Bạn tự author scene, prefab và kéo reference trên Inspector.
Một projectile trong scene được tái sử dụng; chỉ có một shot đang bay. Không Instantiate/Destroy mỗi shot.

## Cấu trúc

```text
Assets/Project/Scripts/
  Core/          LevelController, LevelState, ILevelResettable
  Launcher/      LauncherController, TrajectoryPreview
  Projectile/    Projectile, ProjectileState, ProjectileHitContext,
                 IProjectileInteractable, ProjectileInteractionTarget
  Interactions/  BounceEffect, BoostEffect, Hazard, Goal, Switch, Gate,
                 TeleportSurface
  Environment/   MovingObstacle
  Config/        LevelConfig
  UI/            GameplayUI
```

Các file Bullet, CannonController, CannonShooter, Target, BounceSurface,
BulletPortal và BulletPortalPair tại gốc Scripts là prototype cũ.
Gỡ các component đó khỏi scene/prefab khi chuyển sang bộ mới, đặc biệt Target cũ
có chỉnh `Time.timeScale`. Nếu từng chạy Target cũ, bảo đảm Time Scale trở về 1.
Không gắn Bullet và Projectile cùng một object. Camera fallback trong CannonController cũ
đã được thay bằng validation reference Inspector.

## Dependency

```text
EventSystem -> LauncherController -> Projectile
                     |                 |
                     v                 v
              TrajectoryPreview   ProjectileInteractionTarget
                                       |
                                       v
                              IProjectileInteractable
                                       ^
                           các component mechanic
                                       |
                                       v
                              Projectile public API

Launcher -- Fired event ----------> LevelController
Projectile -- Finished event -----> LevelController
Goal -- Reached event ------------> LevelController
GameplayUI ----------------------> LevelController
LevelController -----------------> Launcher, Goal, LevelConfig
LevelController -----------------> ILevelResettable[]
Switch --------------------------> Gate
```

Projectile không reference loại mechanic cụ thể. Target chỉ dispatch interface.
Đây là callback qua contract, không có cặp dependency trực tiếp Projectile/Goal
hay Projectile/LevelController. Không dùng GameFlowController trong scope một scene mỗi level.

## Thành phần và trách nhiệm

| Thành phần | Loại | Trách nhiệm |
|---|---|---|
| Projectile | MonoBehaviour | Rigidbody, Ready/Flying/Finished, launch, cap speed, lifetime, đứng yên, bounds, collision/trigger, teleport, Finish |
| ProjectileInteractionTarget | MonoBehaviour | Cache component một lần, dispatch interaction theo thứ tự Inspector |
| ProjectileHitContext | readonly struct | Point, Normal, Collider; normal bằng zero đối với trigger |
| IProjectileInteractable | interface | Contract phản ứng khi projectile hit |
| ILevelResettable | interface | Contract reset runtime state |
| LauncherController | MonoBehaviour | Nhận pointer, tính hướng/lực, xoay barrel, chuẩn bị và bắn projectile |
| TrajectoryPreview | MonoBehaviour | Vẽ free-flight trajectory với LineRenderer |
| LevelController | MonoBehaviour | Lượt còn lại, điều phối shot, Win/Fail, restart, subscription |
| Goal | MonoBehaviour + cả hai interface | Reached một lần, reset cờ, kết thúc projectile |
| Hazard | MonoBehaviour + interaction | Finish projectile |
| BounceEffect | MonoBehaviour + interaction | Nhân vận tốc sau phản xạ từ Physics Material |
| BoostEffect | MonoBehaviour + interaction | Nhân vận tốc hiện tại |
| Switch | MonoBehaviour + cả hai interface | Mở một Gate qua reference, phát Activated, reset cờ |
| Gate | MonoBehaviour + reset | Bật/tắt collider và renderer, khôi phục initiallyOpen |
| MovingObstacle | MonoBehaviour + reset | MovePosition kinematic theo ping-pong, reset vị trí/thời gian |
| TeleportSurface | MonoBehaviour + interaction | Ví dụ extension, chuyển đến Transform được gán |
| GameplayUI | MonoBehaviour | Hiển thị shots/Win/Fail, nút Retry |
| LevelConfig | ScriptableObject | Số lượt ban đầu; runtime không sửa asset |
| LevelState, ProjectileState | enum | Ba trạng thái đơn giản mỗi loại |

Chỉ có hai domain interface. Ba interface Pointer của Unity phục vụ tích hợp EventSystem,
không phải một Input Service mới. Thông số launcher/projectile để private SerializeField ngay
component vì chưa cần share tuning asset giữa nhiều loại projectile/launcher.

## Setup Inspector

1. **Camera / plane:** gán camera orthographic nhìn về mặt XY. Author gameplay cùng một Z.
   Chọn portrait trong Player Settings. Không dùng Camera.main, tag hoặc Find để lấy dependency.
2. **Projectile scene object:** thêm SphereCollider, Rigidbody, Projectile. Object phải active
   khi level bắt đầu. Rigidbody: Freeze Position Z và Freeze Rotation XYZ, Linear/Angular Damping = 0,
   Collision Detection = Continuous Dynamic, Interpolation = Interpolate. Chọn Use Gravity theo game.
   Collider không là trigger. Tuning Max Speed, Lifetime, Stopped Speed/Duration và world Play Bounds
   đủ chứa level. Code quản lý isKinematic/detectCollisions giữa các trạng thái.
3. **Cannon:** tạo barrel có local +Y hướng theo nòng, child Muzzle nằm ngoài collider launcher.
   Muzzle và projectile phải cùng plane Z. Không scale vật lý âm hoặc phi đồng nhất.
4. **Aim input:** Canvas có GraphicRaycaster; một Image trong suốt, Raycast Target bật,
   stretch vùng chơi. Gắn LauncherController trên chính Image đó. Kéo Camera, Barrel, Muzzle,
   Projectile và tùy chọn Preview vào Inspector. Kéo ngược hướng muốn bắn theo kiểu slingshot;
   kéo dài tăng lực, thả bắn, dưới Min Drag Distance hủy. Đơn vị drag là world unit trên plane XY.
   Có EventSystem với InputSystemUIInputModule và UI actions; dùng Assign Default Actions nếu
   chưa có. Đặt HUD/nút/popup phía trên Aim Image trong Canvas để chúng nhận raycast trước.
   Một pointer được giữ quyền aim; các pointer khác bị bỏ qua. Mất focus/pause hủy aim.
5. **Preview:** object active, LineRenderer + TrajectoryPreview, gán material/width và Preview
   trên launcher. Preview là tùy chọn. LineRenderer dùng world space, fixed point count.
6. **Wall thường:** collider + renderer là đủ; không cần script. Với mỗi mechanic, thêm collider
   cụ thể trước, sau đó component mechanic. RequireComponent thêm ProjectileInteractionTarget.
   **Collider, Target và các mechanic phải nằm cùng GameObject.** Nếu collider ở child,
   đặt mechanic ở child đó; không có lookup parent hay object khác.
7. **Bounce:** solid collider với Physics Material: Bounciness = 1, Bounce Combine = Maximum,
   Dynamic/Static Friction = 0. Gắn BounceEffect nếu muốn nhân tốc độ. Tránh thêm phép Reflect
   thủ công vì solver đã phản xạ. Normal wall dùng material riêng có bounce thấp/zero.
   Boost trên trigger giữ hướng tốt; Boost trên solid wall nhân vận tốc sau solver, không tự
   sinh lực khi projectile đã đứng yên. Có thể đặt BounceEffect và BoostEffect cùng object.
8. **Goal / Hazard:** collider Is Trigger bật, thêm Goal hoặc Hazard. Tránh collider Goal/Hazard
   chồng nhau: interaction kết thúc đầu tiên có hiệu lực. Goal phát event trước Finish;
   LevelController chuyển Win trước khi nhận Finished, nên lượt cuối thắng không thành Fail.
9. **Gate / Switch:** Gate nhận Blocking Collider và Gate Visual riêng. Switch nhận Target Gate.
   Switch thường dùng trigger. Gate không SetActive cả object; nó chỉ bật/tắt collider/renderer.
10. **MovingObstacle:** collider + kinematic Rigidbody + MovingObstacle, đặt Travel Offset theo
    world space và Duration cho một chiều. Không animate transform đồng thời với Rigidbody này.
11. **Level:** Create > Physics Puzzle > Level Config, đặt projectile count.
    Tạo LevelController và gán Config, Launcher, Goal. Kéo từng Switch, Gate, MovingObstacle
    vào Resettable Components, mỗi component một lần. Goal được reset riêng, không cần thêm vào list.
    Các component có state phải active lúc setup để Awake hoàn tất. Gate reset trạng thái riêng,
    nên không phụ thuộc Switch đứng trước/sau trong list.
12. **UI:** object HUD luôn active có GameplayUI. Gán Level, TMP Shots Label, Win Panel,
    Fail Panel và Retry Button. Panels là object con/anh em riêng, không phải object chứa HUD,
    launcher hoặc LevelController. Retry được subscribe trong code; không cần thêm OnClick
    trên Inspector. Với chữ số TMP, bake sẵn glyph và chừa đủ dung lượng text/mesh.

Scene mới chủ yếu thay geometry, reference và LevelConfig. Không copy code.
Project chưa bao gồm scene mẫu, 5–7 layout level, nút Next hay progression; đây là bộ kiến trúc
để tự author scene. Scene progression có thể thêm sau khi thực sự cần.

## Flow và reset

```text
Start -> validate -> cache reset references -> subscribe -> Restart
Restart -> prepare projectile -> reset mechanics -> Playing -> enable aim
Pointer release -> Launch thành công -> Fired -> RemainingShots giảm 1
Flying -> Hazard / hết lifetime / đứng yên đủ lâu / ngoài bounds -> Finished
LateUpdate -> còn lượt: Ready + enable aim; hết lượt: Fail
Goal -> Reached -> Win -> disable aim -> Finish
Retry -> Restart
```

Không Fail ngay khi vừa bắn lượt cuối. Prepare không phát Finished; Restart không trừ lượt.
Finish idempotent. Disable LevelController hủy subscription và dừng projectile; enable lại
khởi động lại level. Không thay Time.timeScale. MovingObstacle tiếp tục chuyển động sau kết quả;
input/projectile đã khóa, UI vẫn hoạt động. Có thể bổ sung pause mechanic sau nếu game cần.

Không hủy/xây lại các object đã cache trong một level đang chạy. Xóa mechanic ở Editor rồi
chạy lại là đủ. Khi xóa mechanic có state, xóa luôn slot tương ứng khỏi Resettable Components;
slot thiếu reference báo lỗi rõ. Xóa BoostEffect không cần sửa core hay list reset.

## Thêm mechanic

TeleportSurface đã có full implementation: gắn cùng collider/Target và kéo Destination.
Đặt Destination **ngoài** collider đầu ra với khoảng cách lớn hơn bán kính projectile.
Teleport giữ velocity; core chặn các callback teleport còn lại trong cùng render frame.
Không đặt exit bên trong portal đối ứng, tránh vòng teleport qua nhiều frame.

Ví dụ sticky kết thúc shot, chỉ cần thêm file:

```csharp
using UnityEngine;
namespace DreamForgeTD.PhysicsPuzzle
{
    [RequireComponent(typeof(ProjectileInteractionTarget))]
    public sealed class StickySurface : MonoBehaviour, IProjectileInteractable
    {
        public void OnProjectileHit(Projectile projectile, ProjectileHitContext context)
        {
            projectile.Finish();
        }
    }
}
```

Hoặc SetVelocity(Vector3.zero) để chờ stopped timeout; nếu bật gravity thì nó sẽ rơi tiếp.
Component thứ tự trên Inspector cũng là thứ tự interaction. Disabled component được bỏ qua;
Finish ngắt dispatch. Không ghép Goal/Hazard với effect cần chạy sau Finish trên cùng object.
Contract hiện tại là **enter**, phù hợp surface hit. GravityZone/SpeedZone cần xử lý liên tục hoặc
exit có thể tự quản lý trigger riêng; không cố ép mọi mechanic tương lai vào một hit callback.

## GC và giới hạn performance

- Không Find/tag search/singleton/service locator. Reference Inspector bắt buộc được validate.
- Chỉ GetComponent trên chính object bắt buộc và TryGetComponent trên collider vừa hit.
- Target cache mảng MonoBehaviour một lần ở Awake, không GetComponents mới khi hit.
- Dùng Collision.GetContact(0), không dùng collision.contacts tạo mảng.
- Project hiện có Physics > Reuse Collision Callbacks bật; giữ setting này.
- Không LINQ, coroutine, lambda mỗi frame, runtime string formatting trong Update/physics/input.
- Một projectile được reuse; không pool framework. Reset dùng array cache; context là struct.
- Preview SetPosition từng điểm, không cấp phát Vector3[] mỗi lần kéo. UI dùng TMP SetText
  numeric overload, chỉ cập nhật khi shots/state đổi.

**Không tuyên bố toàn game 0 B GC**: array cache, event subscription và khởi tạo có allocation;
EventSystem, TMP/Canvas rebuild hoặc package bên ngoài có thể cấp phát. TMP có thể mở rộng
buffer/glyph ở lần đầu hoặc khi text dài hơn. Muốn xác nhận cần profile trên build thiết bị sau warm-up.
Preview chỉ ước lượng free flight, không dự đoán bounce/obstacle; giả định damping = 0,
gravity toàn cục và không có external force. CCD giúp giảm tunneling nhưng trigger nhỏ vẫn cần
test ở max speed. Cap nằm ở public velocity API, FixedUpdate và sau hit; solver/gravity có thể
tăng tốc giữa các thời điểm đó. Không sửa global Physics settings từ gameplay script.

| Mục | Trạng thái / cách đo |
|---|---|
| Tested scenario | Chưa chạy Play Mode/mobile: scene cần được bạn gán Inspector |
| Compile | Đã compile toàn bộ Scripts bằng Roslyn với reference Unity 6000.0.75f1 của project; không lỗi C#, có cảnh báo field được gán qua Inspector |
| Main bottleneck/risk | Physics contact dày, trigger tốc độ cao, Canvas/TMP rebuild, input package GC |
| Action taken | Reuse projectile, cache interaction/reset, GetContact, không allocation collection ở hot path |
| Result | Chưa có số liệu CPU/GC thiết bị; không ghi kết quả giả định |

Trước submit: Development Build trên máy mục tiêu, Profiler bật GC.Alloc call stacks, tắt Deep
Profile khi đo timing. Warm up aim/UI rồi đo 60 giây kéo liên tục, bắn max speed qua nhiều
bounce/boost và moving obstacle, Retry lặp lại. Ghi device, build/backend, frame CPU và B/frame.
Kiểm tra lượt cuối vào Goal, Hazard, timeout, bounds, reset Switch/Gate và teleport hai chiều.

## Vì sao đủ dùng

Core chỉ điều phối shot và state; mechanic mới cùng nhóm là component implement một interface.
Target cache là abstraction bổ sung duy nhất cho dispatch để tránh tạo mảng mỗi hit.
Reference tường minh và hai contract nhỏ đủ cho vài level, reviewer có thể đọc từ input đến reset.

Cố tình không thêm inheritance chain, Singleton Manager, Global EventBus, DI, ServiceLocator,
generic FSM/factory, framework pooling/Addressables/save hay GameFlowController chưa có use case.
Không tạo interface cho từng class và không ép mọi con số vào ScriptableObject.
Nhiều projectile đồng thời hoặc thay luật nền tảng sẽ cần refactor; đó là giới hạn có chủ đích.
