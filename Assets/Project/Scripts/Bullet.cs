using UnityEngine;
using UnityEngine.Serialization;

namespace DreamForgeTD
{
    [RequireComponent(typeof(Rigidbody), typeof(BulletForceFieldReceiver))]
    public sealed class Bullet : MonoBehaviour
    {
        [Tooltip("Impulse applied once at launch; Rigidbody mass affects resulting velocity.")]
        [FormerlySerializedAs("speed"), SerializeField, Min(0f)] private float launchImpulse = 20f;

        private Rigidbody body;

        public float LaunchImpulse => launchImpulse;
        private CannonShooter chuSoHuu;

        internal void GanChuSoHuu(CannonShooter cannon) => chuSoHuu = cannon;

        private void Awake() => body = GetComponent<Rigidbody>();

        private void Start()
        {
            // The cannon barrel points along local +Y; Rigidbody handles motion after this impulse.
            body.AddForce(transform.up * launchImpulse, ForceMode.Impulse);
        }

        public void SetLaunchImpulse(float impulse)
        {
            launchImpulse = Mathf.Max(0f, impulse);
            if (body != null && body.linearVelocity.sqrMagnitude > 0f)
            {
                body.linearVelocity = transform.up * (launchImpulse / body.mass);
            }
        }

        private void OnDisable()
        {
            if (chuSoHuu != null)
                chuSoHuu.BoTheoDoiDan(this);
            chuSoHuu = null;
        }
    }
}
