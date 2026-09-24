using UnityEngine;

namespace DreamForgeTD
{
    public class BounceSurface : MonoBehaviour, IBulletMechanic, IBulletTrajectoryRule
    {
        private void Awake()
        {
            Collider surfaceCollider = GetComponent<Collider>();
            if (surfaceCollider == null)
            {
                Debug.LogError("BounceSurface needs a Collider on the same GameObject.", this);
                enabled = false;
                return;
            }

            surfaceCollider.isTrigger = false;
        }

        public void OnBulletHit(BulletHitContext hit)
        {
            if (hit.Body == null || hit.Normal.sqrMagnitude < 0.0001f)
                return;

            Vector3 incomingVelocity = hit.IncomingVelocity.sqrMagnitude > 0f
                ? hit.IncomingVelocity
                : hit.Body.linearVelocity;
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

        private static Vector3 ReflectVelocity(Vector3 velocity, Vector3 normal)
        {
            return Vector3.Reflect(velocity, normal);
        }
    }
}
