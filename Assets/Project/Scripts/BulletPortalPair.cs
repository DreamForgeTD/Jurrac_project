using UnityEngine;

namespace DreamForgeTD
{
    public class BulletPortalPair : MonoBehaviour
    {
        [SerializeField] private BulletPortal entryPoint;
        [SerializeField] private BulletPortal exitPoint;
        [Tooltip("Khoảng cách đưa đạn ra khỏi tâm điểm đích theo hướng bay hiện tại.")]
        [SerializeField] private float exitOffset = 0.75f;

        private void Awake()
        {
            if (entryPoint == null || exitPoint == null || entryPoint == exitPoint)
            {
                Debug.LogError("Assign two different portal endpoints to Entry Point and Exit Point.", this);
                enabled = false;
                return;
            }

            entryPoint.SetPortalPair(this);
            exitPoint.SetPortalPair(this);
        }

        public void HandlePortalHit(BulletPortal source, Collider other)
        {
            if (source != entryPoint && source != exitPoint)
            {
                return;
            }

            Rigidbody bulletBody = other.attachedRigidbody;
            if (bulletBody == null || !bulletBody.TryGetComponent(out Bullet bullet))
            {
                return;
            }

            BulletPortal destination = source == entryPoint ? exitPoint : entryPoint;
            bullet.TeleportTo(destination.transform, exitOffset);
        }
    }
}
