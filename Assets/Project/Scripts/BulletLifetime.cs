using UnityEngine;

namespace DreamForgeTD
{
    public sealed class BulletLifetime : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float lifetime = 4f;

        private void Start() => Destroy(gameObject, lifetime);
    }
}
