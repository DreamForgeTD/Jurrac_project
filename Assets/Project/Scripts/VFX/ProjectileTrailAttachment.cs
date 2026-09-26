using UnityEngine;

namespace DreamForgeTD
{
    [DisallowMultipleComponent]
    public sealed class ProjectileTrailAttachment : MonoBehaviour
    {
        private GameObject trail;

        public void Track(GameObject trailInstance)
        {
            trail = trailInstance;
        }

        public void ClearImmediately()
        {
            if (trail == null)
                return;

            GameVfx.StopAndClear(trail);
            trail.SetActive(false);
            Destroy(trail);
            trail = null;
        }

        private void OnDestroy()
        {
            if (trail == null)
                return;

            trail.transform.SetParent(null, true);
            GameVfx.Stop(trail);
        }
    }
}
