using UnityEngine;

namespace DreamForgeTD
{
    public interface IBulletTrajectoryRule
    {
        BulletTrajectoryResponse PredictTrajectory(BulletTrajectoryHit hit);
    }

    public enum BulletTrajectoryResponseType
    {
        Stop,
        Continue,
        Teleport
    }

    public readonly struct BulletTrajectoryHit
    {
        public Collider Collider { get; }
        public Vector3 BulletPosition { get; }
        public Vector3 ContactPoint { get; }
        public Vector3 Normal { get; }
        public Vector3 IncomingVelocity { get; }
        public float CollisionSkin { get; }

        public BulletTrajectoryHit(
            Collider collider,
            Vector3 bulletPosition,
            Vector3 contactPoint,
            Vector3 normal,
            Vector3 incomingVelocity,
            float collisionSkin)
        {
            Collider = collider;
            BulletPosition = bulletPosition;
            ContactPoint = contactPoint;
            Normal = normal;
            IncomingVelocity = incomingVelocity;
            CollisionSkin = collisionSkin;
        }
    }

    public readonly struct BulletTrajectoryResponse
    {
        public BulletTrajectoryResponseType Type { get; }
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }

        private BulletTrajectoryResponse(BulletTrajectoryResponseType type, Vector3 position, Vector3 velocity)
        {
            Type = type;
            Position = position;
            Velocity = velocity;
        }

        public static BulletTrajectoryResponse Stop(Vector3 position)
        {
            return new BulletTrajectoryResponse(BulletTrajectoryResponseType.Stop, position, Vector3.zero);
        }

        public static BulletTrajectoryResponse Continue(Vector3 position, Vector3 velocity)
        {
            return new BulletTrajectoryResponse(BulletTrajectoryResponseType.Continue, position, velocity);
        }

        public static BulletTrajectoryResponse Teleport(Vector3 position, Vector3 velocity)
        {
            return new BulletTrajectoryResponse(BulletTrajectoryResponseType.Teleport, position, velocity);
        }
    }
}
