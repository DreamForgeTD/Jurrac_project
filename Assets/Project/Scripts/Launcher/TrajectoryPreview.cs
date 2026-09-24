using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TrajectoryPreview : MonoBehaviour
    {
        [SerializeField, Range(2, 64)] private int pointCount = 24;
        [SerializeField, Min(0.01f)] private float timeStep = 0.05f;
        private LineRenderer line;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = pointCount;
            line.enabled = false;
        }

        public void Show(Vector3 origin, Vector3 velocity, Vector3 gravity, float maxSpeed)
        {
            if (line == null) return;
            line.enabled = true;
            Vector3 position = origin;
            float elapsed = 0f;
            // Approximate free flight, using the same velocity cap and fixed timestep.
            for (int i = 0; i < pointCount; i++)
            {
                float sampleTime = i * timeStep;
                while (elapsed < sampleTime)
                {
                    float step = Mathf.Min(Time.fixedDeltaTime, sampleTime - elapsed);
                    velocity = Vector3.ClampMagnitude(velocity, maxSpeed) + gravity * step;
                    position += velocity * step;
                    elapsed += step;
                }
                line.SetPosition(i, position);
            }
        }

        public void Hide()
        {
            if (line != null) line.enabled = false;
        }
    }
}
