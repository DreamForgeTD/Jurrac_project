using UnityEngine;

namespace DreamForgeTD
{
    public interface IBulletForceField
    {
        Vector3 GetAcceleration(BulletMotionSample bullet);
    }
}
