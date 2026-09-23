using UnityEngine;

namespace DreamForgeTD
{
    public class BulletPortal : MonoBehaviour
    {
        private BulletPortalPair portalPair;

        private void Awake()
        {
            Collider portalCollider = GetComponent<Collider>();
            if (portalCollider == null)
            {
                Debug.LogError("BulletPortal needs a Collider on the same GameObject.", this);
                enabled = false;
                return;
            }

            portalCollider.isTrigger = true;
        }

        public void SetPortalPair(BulletPortalPair pair)
        {
            portalPair = pair;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (portalPair != null)
            {
                portalPair.HandlePortalHit(this, other);
            }
        }
    }
}
