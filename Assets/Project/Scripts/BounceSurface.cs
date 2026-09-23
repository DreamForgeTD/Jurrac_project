using UnityEngine;

namespace DreamForgeTD
{
    public class BounceSurface : MonoBehaviour
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
    }
}
