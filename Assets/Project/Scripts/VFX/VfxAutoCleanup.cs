using UnityEngine;

namespace DreamForgeTD
{
    [DisallowMultipleComponent]
    public sealed class VfxAutoCleanup : MonoBehaviour
    {
        [SerializeField, Min(0.25f)] private float maximumLifetime = 8f;

        private ParticleSystem[] systems;
        private float elapsed;
        private bool hasPlayed;

        private void Awake()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            bool anyAlive = false;

            for (int i = 0; systems != null && i < systems.Length; i++)
            {
                if (systems[i] != null && systems[i].IsAlive(true))
                {
                    anyAlive = true;
                    break;
                }
            }

            hasPlayed |= anyAlive;
            if ((!anyAlive && hasPlayed) || elapsed >= maximumLifetime)
                Destroy(gameObject);
        }
    }
}
