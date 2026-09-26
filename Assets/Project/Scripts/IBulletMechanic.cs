using UnityEngine;

namespace DreamForgeTD
{
    /// <summary>Optional hit behavior discovered by BulletImpactRouter.</summary>
    public interface IBulletMechanic
    {
        void OnBulletHit(BulletHitContext hit);
    }

    public readonly struct BulletHitContext
    {
        public Rigidbody Body { get; }
        public Collider Collider { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public Vector3 IncomingVelocity { get; }
        public Vector3 IncomingAngularVelocity { get; }
        public Vector3 IncomingCenterOfMass { get; }

        public BulletHitContext(
            Rigidbody body,
            Collider collider,
            Vector3 point,
            Vector3 normal,
            Vector3 incomingVelocity,
            Vector3 incomingAngularVelocity,
            Vector3 incomingCenterOfMass)
        {
            Body = body;
            Collider = collider;
            Point = point;
            Normal = normal;
            IncomingVelocity = incomingVelocity;
            IncomingAngularVelocity = incomingAngularVelocity;
            IncomingCenterOfMass = incomingCenterOfMass;
        }
    }
}
