using UnityEngine;

namespace DreamForgeTD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BulletForceFieldReceiver : MonoBehaviour
    {
        private Rigidbody body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (body == null || body.isKinematic)
                return;

            BulletMotionSample sample = new BulletMotionSample(body.position, body.linearVelocity);
            Vector3 acceleration = BulletForceFieldRegistry.GetCombinedAcceleration(sample);
            if (acceleration.sqrMagnitude > 0.000001f)
                body.AddForce(acceleration, ForceMode.Acceleration);
        }
    }
}
