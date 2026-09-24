using UnityEngine;
using UnityEngine.Serialization;

namespace DreamForgeTD
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Bullet : MonoBehaviour
    {
        [Tooltip("Impulse applied once at launch; Rigidbody mass affects resulting velocity.")]
        [FormerlySerializedAs("speed"), SerializeField, Min(0f)] private float launchImpulse = 20f;

        private Rigidbody body;

        public float LaunchImpulse => launchImpulse;

        private void Awake() => body = GetComponent<Rigidbody>();

        private void Start()
        {
            // The cannon barrel points along local +Y; Rigidbody handles motion after this impulse.
            body.AddForce(transform.up * launchImpulse, ForceMode.Impulse);
        }
    }
}
