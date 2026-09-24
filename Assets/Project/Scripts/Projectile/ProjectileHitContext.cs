using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    public readonly struct ProjectileHitContext
    {
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public Collider Collider { get; }

        public ProjectileHitContext(Vector3 point, Vector3 normal, Collider collider)
        {
            Point = point;
            Normal = normal;
            Collider = collider;
        }
    }
}
