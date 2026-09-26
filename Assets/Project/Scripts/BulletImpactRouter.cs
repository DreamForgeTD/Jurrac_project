using System.Collections.Generic;
using UnityEngine;

namespace DreamForgeTD
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BulletImpactRouter : MonoBehaviour
    {
        private readonly List<MonoBehaviour> hitBehaviours = new List<MonoBehaviour>(4);
        private Rigidbody body;
        private Vector3 velocityBeforePhysics;
        private Vector3 angularVelocityBeforePhysics;
        private Vector3 centerOfMassBeforePhysics;

        private void Awake() => body = GetComponent<Rigidbody>();

        private void FixedUpdate()
        {
            velocityBeforePhysics = body.linearVelocity;
            angularVelocityBeforePhysics = body.angularVelocity;
            centerOfMassBeforePhysics = body.worldCenterOfMass;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0)
                return;

            ContactPoint contact = collision.GetContact(0);
            if (collision.collider.GetComponent<BounceSurface>() != null)
                GameVfx.PlayBulletBounce(contact.point, contact.normal);
            else if (collision.collider.GetComponentInParent<BowlingCan>() == null)
            {
                GameVfx.PlayBulletImpact(contact.point, contact.normal);
                GameAudio.PlayObstacleCollision(contact.point, collision.relativeVelocity.magnitude);
            }

            Dispatch(collision.collider, contact.point, contact.normal, velocityBeforePhysics,
                angularVelocityBeforePhysics, centerOfMassBeforePhysics);
        }

        private void OnTriggerEnter(Collider other)
            => Dispatch(other, body.position, Vector3.zero, body.linearVelocity,
                body.angularVelocity, body.worldCenterOfMass);

        private void Dispatch(
            Collider other,
            Vector3 point,
            Vector3 normal,
            Vector3 incomingVelocity,
            Vector3 incomingAngularVelocity,
            Vector3 incomingCenterOfMass)
        {
            if (other == null)
                return;

            hitBehaviours.Clear();
            other.GetComponents(hitBehaviours);
            BulletHitContext hit = new BulletHitContext(
                body, other, point, normal, incomingVelocity, incomingAngularVelocity, incomingCenterOfMass);

            // Inspector component order defines mechanic order on the hit object.
            for (int i = 0; i < hitBehaviours.Count; i++)
            {
                MonoBehaviour behaviour = hitBehaviours[i];
                if (behaviour != null && behaviour.isActiveAndEnabled && behaviour is IBulletMechanic mechanic)
                    mechanic.OnBulletHit(hit);
            }
        }
    }
}
