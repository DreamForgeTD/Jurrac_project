using UnityEngine;

namespace DreamForgeTD
{
    public readonly struct BulletMotionSample
    {
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }

        public BulletMotionSample(Vector3 position, Vector3 velocity)
        {
            Position = position;
            Velocity = velocity;
        }
    }
}
