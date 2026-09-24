using System.Collections.Generic;
using UnityEngine;
using PrimeTween;

namespace DreamForgeTD
{
    public class BulletPortalPair : MonoBehaviour
    {
        [Header("Portal Endpoints")]
        [SerializeField] private BulletPortal entryPoint;
        [SerializeField] private BulletPortal exitPoint;

        [Header("Exit Spawn Objects")]
        [Tooltip("Spawn position and local -Y exit direction used when a bullet exits through Entry Point. The direction is projected onto XY; the bullet keeps its current Z position.")]
        [SerializeField] private GameObject spawnObjectAtEntry;
        [Tooltip("Spawn position and local -Y exit direction used when a bullet exits through Exit Point. The direction is projected onto XY; the bullet keeps its current Z position.")]
        [SerializeField] private GameObject spawnObjectAtExit;

        [Header("Scene Gizmos")]
        [SerializeField, Min(0.1f)] private float gizmoDirectionLength = 1f;

        [Header("Portal Animation")]
        [SerializeField, Min(0f)] private float transferDelay = 0.5f;
        [SerializeField, Min(0.05f)] private float retractDuration = 0.15f;
        [SerializeField, Range(0.1f, 1f)] private float retractedScale = 0.75f;
        [SerializeField, Min(0.05f)] private float popDuration = 0.12f;
        [SerializeField, Min(1f)] private float popScale = 1.15f;

        private const float DirectionEpsilon = 0.000001f;
        private readonly HashSet<Rigidbody> transferringBullets = new HashSet<Rigidbody>();

        private void Awake()
        {
            if (entryPoint == null || exitPoint == null || entryPoint == exitPoint)
            {
                Debug.LogError("Assign two different portal endpoints to Entry Point and Exit Point.", this);
                enabled = false;
                return;
            }

            if (spawnObjectAtEntry == null || spawnObjectAtExit == null)
            {
                Debug.LogError("Assign a spawn object for both Entry and Exit.", this);
                enabled = false;
                return;
            }

            entryPoint.SetPortalPair(this);
            exitPoint.SetPortalPair(this);
        }

        public void HandlePortalHit(BulletPortal source, Rigidbody body, Vector3 incomingVelocity)
        {
            if (body == null || transferringBullets.Contains(body) || !TryGetPredictedExit(
                    source,
                    body.position,
                    incomingVelocity,
                    out Vector3 exitPosition,
                    out Vector3 exitVelocity,
                    out Quaternion exitRotation))
            {
                return;
            }

            BulletPortal destination = source == entryPoint ? exitPoint : entryPoint;
            destination.IgnoreBulletUntilExit(body);

            transferringBullets.Add(body);
            bool wasKinematic = body.isKinematic;
            if (!wasKinematic)
                body.linearVelocity = Vector3.zero;
            body.isKinematic = true;

            Transform sourceTransform = source.transform;
            Transform destinationTransform = destination.transform;
            Vector3 sourceScale = sourceTransform.localScale;
            Vector3 destinationScale = destinationTransform.localScale;

            Sequence transferAnimation = Sequence.Create()
                .Group(Tween.Scale(sourceTransform, sourceScale * retractedScale, retractDuration, Ease.InBack))
                .Group(Tween.Scale(destinationTransform, destinationScale * retractedScale, retractDuration, Ease.InBack));

            float remainingDelay = transferDelay - retractDuration;
            if (remainingDelay > 0f)
                transferAnimation = transferAnimation.Chain(Tween.Delay(remainingDelay));

            transferAnimation.ChainCallback(() =>
            {
                if (body != null)
                {
                    body.position = exitPosition;
                    body.rotation = exitRotation;
                    body.isKinematic = wasKinematic;
                    if (!wasKinematic)
                    {
                        body.linearVelocity = exitVelocity;
                        body.WakeUp();
                    }
                }

                transferringBullets.Remove(body);

                Sequence.Create()
                    .Group(Tween.Scale(sourceTransform, sourceScale, popDuration, Ease.OutBack))
                    .Group(Tween.Scale(destinationTransform, destinationScale * popScale, popDuration, Ease.OutBack))
                    .Chain(Tween.Scale(destinationTransform, destinationScale, Mathf.Max(0.05f, popDuration * 0.7f), Ease.OutSine));
            });
        }

        public bool TryGetPredictedExit(
            BulletPortal source,
            Vector3 incomingPosition,
            Vector3 incomingVelocity,
            out Vector3 exitPosition,
            out Vector3 outgoingVelocity,
            out Quaternion exitRotation)
        {
            if (source == null || (source != entryPoint && source != exitPoint))
            {
                exitPosition = Vector3.zero;
                outgoingVelocity = Vector3.zero;
                exitRotation = Quaternion.identity;
                return false;
            }

            BulletPortal destination = source == entryPoint ? exitPoint : entryPoint;
            GameObject spawnObject = destination == entryPoint ? spawnObjectAtEntry : spawnObjectAtExit;
            if (spawnObject == null)
            {
                exitPosition = Vector3.zero;
                outgoingVelocity = Vector3.zero;
                exitRotation = Quaternion.identity;
                return false;
            }

            Transform spawnTransform = spawnObject.transform;
            if (!TryGetPlanarExitDirection(spawnTransform, out Vector3 exitDirection))
            {
                exitPosition = Vector3.zero;
                outgoingVelocity = Vector3.zero;
                exitRotation = Quaternion.identity;
                return false;
            }

            exitPosition = new Vector3(spawnTransform.position.x, spawnTransform.position.y, incomingPosition.z);
            outgoingVelocity = exitDirection * incomingVelocity.magnitude;
            exitRotation = Quaternion.FromToRotation(Vector3.up, exitDirection);
            return true;
        }

        private void OnDrawGizmos()
        {
            DrawSpawnDirection(spawnObjectAtEntry, new Color(0.2f, 0.9f, 1f), "Spawn at Entry");
            DrawSpawnDirection(spawnObjectAtExit, new Color(1f, 0.55f, 0.2f), "Spawn at Exit");
        }

        private void DrawSpawnDirection(GameObject spawnObject, Color color, string label)
        {
            if (spawnObject == null)
                return;

            Transform spawnTransform = spawnObject.transform;
            Vector3 origin = spawnTransform.position;
            if (!TryGetPlanarExitDirection(spawnTransform, out Vector3 direction))
            {
                DrawInvalidDirection(origin, color);
                return;
            }

            Vector3 tip = origin + direction * gizmoDirectionLength;
            Vector3 arrowBase = tip - direction * Mathf.Min(0.2f, gizmoDirectionLength * 0.25f);
            Vector3 side = Vector3.Cross(Vector3.forward, direction).normalized;
            float arrowWidth = Mathf.Min(0.1f, gizmoDirectionLength * 0.12f);

            Gizmos.color = color;
            Gizmos.DrawSphere(origin, 0.055f);
            Gizmos.DrawLine(origin, tip);
            Gizmos.DrawLine(tip, arrowBase + side * arrowWidth);
            Gizmos.DrawLine(tip, arrowBase - side * arrowWidth);

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(tip, $"{label} (-Y to XY)");
#endif
        }

        private static bool TryGetPlanarExitDirection(Transform spawnTransform, out Vector3 direction)
        {
            direction = Vector3.ProjectOnPlane(-spawnTransform.up, Vector3.forward);
            if (direction.sqrMagnitude < DirectionEpsilon)
                return false;

            direction.Normalize();
            return true;
        }

        private static void DrawInvalidDirection(Vector3 origin, Color color)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin + Vector3.left * 0.12f, origin + Vector3.right * 0.12f);
            Gizmos.DrawLine(origin + Vector3.down * 0.12f, origin + Vector3.up * 0.12f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.Label(origin, "-Y has no XY direction");
#endif
        }
    }
}
