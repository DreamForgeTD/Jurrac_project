using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MovingObstacle : MonoBehaviour, ILevelResettable
    {
        [Tooltip("World-space displacement from the initial position.")]
        [SerializeField] private Vector3 travelOffset = new Vector3(3f, 0f, 0f);
        [SerializeField, Min(0.01f)] private float duration = 2f;
        private Rigidbody body;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float elapsed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            startPosition = body.position;
            startRotation = body.rotation;
        }

        private void FixedUpdate()
        {
            elapsed = Mathf.Repeat(elapsed + Time.fixedDeltaTime, Mathf.Max(0.01f, duration) * 2f);
            float progress = Mathf.PingPong(elapsed / Mathf.Max(0.01f, duration), 1f);
            body.MovePosition(startPosition + travelOffset * progress);
        }

        public void ResetState()
        {
            elapsed = 0f;
            body.position = startPosition;
            body.rotation = startRotation;
        }
    }
}
