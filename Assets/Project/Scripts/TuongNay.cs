using System.Collections;
using UnityEngine;

namespace DreamForgeTD
{
    public class TuongNay : MonoBehaviour, IBulletMechanic, IBulletTrajectoryRule
    {
        private const float ThoiGianNayGiay = 0.18f;
        private const float DoPhongNay = 0.08f;

        [SerializeField, Min(0f), Tooltip("Hệ số nhân tốc độ của đạn sau khi bật nảy. 1.5 = nhanh hơn 50%.")]
        private float heSoBatNay = 1.5f;

        [SerializeField, Tooltip("Kéo Transform của phần model hiển thị cần nảy. Không gán object có Collider.")]
        private Transform phanHinhAnhNay;

        private Vector3 kichThuocGoc;
        private Coroutine coroutineNay;

        private void Awake()
        {
            if (phanHinhAnhNay != null)
                kichThuocGoc = phanHinhAnhNay.localScale;
            else
                Debug.LogWarning("TuongNay needs a visual child assigned to Phan Hinh Anh Nay.", this);

            Collider surfaceCollider = GetComponent<Collider>();
            if (surfaceCollider == null)
            {
                Debug.LogError("TuongNay needs a Collider on the same GameObject.", this);
                enabled = false;
                return;
            }

            surfaceCollider.isTrigger = false;
        }

        public void OnBulletHit(BulletHitContext hit)
        {
            if (hit.Body == null || hit.Normal.sqrMagnitude < 0.0001f)
                return;

            BatDauNayHinhAnh();

            Vector3 incomingVelocity = hit.IncomingVelocity.sqrMagnitude > 0f
                ? hit.IncomingVelocity
                : hit.Body.linearVelocity;
            GameAudio.PlayBounce(hit.Point, incomingVelocity.magnitude);
            hit.Body.linearVelocity = ReflectVelocity(incomingVelocity, hit.Normal);
        }

        public BulletTrajectoryResponse PredictTrajectory(BulletTrajectoryHit hit)
        {
            if (hit.Normal.sqrMagnitude < 0.0001f)
                return BulletTrajectoryResponse.Stop(hit.BulletPosition);

            Vector3 velocity = ReflectVelocity(hit.IncomingVelocity, hit.Normal);
            Vector3 position = hit.BulletPosition + hit.Normal * hit.CollisionSkin;
            return BulletTrajectoryResponse.Continue(position, velocity);
        }

        private void OnDisable()
        {
            if (coroutineNay != null)
            {
                StopCoroutine(coroutineNay);
                coroutineNay = null;
            }

            if (phanHinhAnhNay != null)
                phanHinhAnhNay.localScale = kichThuocGoc;
        }

        private void BatDauNayHinhAnh()
        {
            if (phanHinhAnhNay == null)
                return;

            if (coroutineNay != null)
                StopCoroutine(coroutineNay);

            phanHinhAnhNay.localScale = kichThuocGoc;
            coroutineNay = StartCoroutine(NayHinhAnh());
        }

        private IEnumerator NayHinhAnh()
        {
            float thoiGianDaChay = 0f;

            while (thoiGianDaChay < ThoiGianNayGiay)
            {
                thoiGianDaChay += Time.unscaledDeltaTime;
                float tienTrinh = Mathf.Clamp01(thoiGianDaChay / ThoiGianNayGiay);
                float heSoPhong = 1f + Mathf.Sin(tienTrinh * Mathf.PI) * DoPhongNay;
                phanHinhAnhNay.localScale = kichThuocGoc * heSoPhong;
                yield return null;
            }

            phanHinhAnhNay.localScale = kichThuocGoc;
            coroutineNay = null;
        }

        private Vector3 ReflectVelocity(Vector3 velocity, Vector3 normal)
        {
            return Vector3.Reflect(velocity, normal.normalized) * Mathf.Max(0f, heSoBatNay);
        }
    }
}
