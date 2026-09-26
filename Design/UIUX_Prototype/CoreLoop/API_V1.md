

## API runtime sau toi uu 26-09-2026

- `GameManager.MaLuot`: `int`, tang sau load thanh cong de UI/tutorial nhan biet retry cung index.
- `GameManager.DaHoanTatTatCaMan`: `bool`, true khi NextLevel vuot man cuoi; reset khi load.
- `GameManager.SungHienTai`: `CannonShooter`; `DieuKhienSung`: `CannonController`; co the null truoc load.
- UI/tutorial doc cac property tren cung `IsLevelWon`, `IsLevelLost`, `CurrentLevelIndex`. C# events GameManager cu da bo sau khi chuyen het caller; UnityEvent Inspector van giu tuong thich serialized.
- Quota goi truc tiep `CannonShooter.NapDanTheoMan(int)`; interface tam `IGioiHanDanTheoMan` da xoa.
- Custom FX goi `GameVfx.Phat(GameObject, Vector3, Quaternion, float maximumLifetime = 0f)`. Gia tri `0` giu lifetime serialized tren prefab; prefab chua co VfxAutoCleanup dung mac dinh 8 giay. Pool giu toi da 128 FX dang nghi cho moi prefab. Khi doi man: `CannonShooter.DonDanTrongMan()` truoc `GameObjectManager.ClearManagedObjects()`; ham clear tra FX ve pool qua `GameVfx.DonHieuUngTrongMan()`.
