using System;
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
        [Tooltip("Thời gian chờ từ lúc đạn vào cổng Entry đến khi cổng Exit bắt đầu co lại và bắn đạn ra.")]
        [SerializeField, Min(0f)] private float transferDelay = 0.5f;
        [Tooltip("Thời gian co lại của cổng.")]
        [SerializeField, Min(0.05f)] private float retractDuration = 0.15f;
        [Tooltip("Tỉ lệ co lại của cổng khi hút hoặc chuẩn bị bắn đạn.")]
        [SerializeField, Range(0.1f, 1f)] private float retractedScale = 0.75f;
        [Tooltip("Thời gian nảy nở bung ra khi bắn viên đạn.")]
        [SerializeField, Min(0.05f)] private float popDuration = 0.12f;
        [Tooltip("Tỉ lệ phóng to cực đại khi bắn viên đạn.")]
        [SerializeField, Min(1f)] private float popScale = 1.15f;

        [Header("Exit Velocity Settings (Tăng lực đầu ra)")]
        [Tooltip("Hệ số nhân tốc độ đầu ra của đạn (1.0 = giữ nguyên, 1.5 = tăng 50%, 2.0 = gấp đôi lực bắn).")]
        [SerializeField, Min(0.1f)] private float exitSpeedMultiplier = 1.2f;
        [Tooltip("Lực/tốc độ cộng thêm trực tiếp vào đầu ra (mặc định = 0).")]
        [SerializeField, Min(0f)] private float extraExitSpeed = 0f;

        private const float DirectionEpsilon = 0.000001f;
        private const int PreallocatedJobCount = 8;

        // Lưu trữ scale chuẩn ban đầu để tránh bị lệch scale tích lũy
        private Vector3 entryInitialScale;
        private Vector3 exitInitialScale;

        // Tránh GC Alloc: Tái sử dụng Job Pool và static delegate
        private readonly List<PortalTransferJob> jobPool = new List<PortalTransferJob>(PreallocatedJobCount);
        private readonly HashSet<Rigidbody> transferringBullets = new HashSet<Rigidbody>(PreallocatedJobCount);

        private static readonly Action<PortalTransferJob> onReleaseBullet = ReleaseBullet;
        private static readonly Action<PortalTransferJob> onJobComplete = CompleteJob;

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

            entryInitialScale = entryPoint.transform.localScale;
            exitInitialScale = exitPoint.transform.localScale;

            entryPoint.SetPortalPair(this);
            exitPoint.SetPortalPair(this);

            // Pre-allocate pool (0 GC tại runtime khi bắn qua cổng)
            for (int i = 0; i < PreallocatedJobCount; i++)
            {
                jobPool.Add(new PortalTransferJob());
            }
        }

        private void OnDestroy()
        {
            jobPool.Clear();
            transferringBullets.Clear();
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

            GameAudio.PlayPortalEnter(source.transform.position);
            GameVfx.PlayPortalEnter(source.transform.position, source.transform.up);
            BulletPortal destination = source == entryPoint ? exitPoint : entryPoint;
            destination.IgnoreBulletUntilExit(body);

            transferringBullets.Add(body);
            bool wasKinematic = body.isKinematic;
            if (!wasKinematic)
                body.linearVelocity = Vector3.zero;
            body.isKinematic = true;

            Transform sourceTransform = source.transform;
            Transform destinationTransform = destination.transform;
            Vector3 sourceScale = source == entryPoint ? entryInitialScale : exitInitialScale;
            Vector3 destinationScale = destination == entryPoint ? entryInitialScale : exitInitialScale;

            // Thuê job từ Pool (không cấp phát heap)
            PortalTransferJob job = RentJob();
            job.Pair = this;
            job.Body = body;
            job.BulletTransform = body.transform;
            job.BulletOriginalScale = body.transform.localScale;
            job.ExitPosition = exitPosition;
            job.ExitVelocity = exitVelocity;
            job.ExitRotation = exitRotation;
            job.WasKinematic = wasKinematic;
            job.Destination = destination;
            job.DestinationTransform = destinationTransform;
            job.DestinationScale = destinationScale;

            // Chuỗi diễn hoạt mượt mà, tuần tự (Không đồng thời):
            // 1. Cổng vào (source) co lại hút đạn vào + viên đạn thu nhỏ lại biến mất (không bị khựng đơ trên màn hình).
            // 2. Cổng vào hồi phục lại kích thước ban đầu.
            // 3. Chờ đúng khoảng thời gian transferDelay (0.5s).
            // 4. Cổng ra (destination) lúc này MỚI co lại lấy đà.
            // 5. Cổng ra bung to (pop) ra và BẮN viên đạn ra ngoài với vận tốc đích.
            // 6. Cổng ra thu về kích thước ban đầu và hoàn tất.
            Sequence.Create()
                .Group(Tween.Scale(sourceTransform, sourceScale * retractedScale, retractDuration, Ease.InBack))
                .Group(Tween.Scale(job.BulletTransform, Vector3.zero, retractDuration, Ease.InBack))
                .Chain(Tween.Scale(sourceTransform, sourceScale, retractDuration, Ease.OutBack))
                .Chain(Tween.Delay(transferDelay))
                .Chain(Tween.Scale(destinationTransform, destinationScale * retractedScale, retractDuration, Ease.InBack))
                .ChainCallback(job, onReleaseBullet)
                .Chain(Tween.Scale(destinationTransform, destinationScale * popScale, popDuration, Ease.OutBack))
                .Chain(Tween.Scale(destinationTransform, destinationScale, Mathf.Max(0.05f, popDuration * 0.7f), Ease.OutSine))
                .ChainCallback(job, onJobComplete);
        }

        private static void ReleaseBullet(PortalTransferJob job)
        {
            if (job.Body != null)
            {
                job.Body.position = job.ExitPosition;
                job.Body.rotation = job.ExitRotation;

                if (job.BulletTransform != null)
                {
                    job.BulletTransform.localScale = job.BulletOriginalScale;
                }

                job.Body.isKinematic = job.WasKinematic;
                if (!job.WasKinematic)
                {
                    job.Body.linearVelocity = job.ExitVelocity;
                    job.Body.WakeUp();
                }

                GameAudio.PlayPortalExit(job.ExitPosition);
                GameVfx.PlayPortalExit(job.ExitPosition, job.ExitVelocity);
            }
        }

        private static void CompleteJob(PortalTransferJob job)
        {
            if (job.Pair != null)
            {
                if (job.Body != null)
                {
                    job.Pair.transferringBullets.Remove(job.Body);
                }

                job.Pair.ReturnJob(job);
            }
        }

        private PortalTransferJob RentJob()
        {
            int count = jobPool.Count;
            if (count > 0)
            {
                PortalTransferJob job = jobPool[count - 1];
                jobPool.RemoveAt(count - 1);
                return job;
            }

            return new PortalTransferJob();
        }

        private void ReturnJob(PortalTransferJob job)
        {
            job.Pair = null;
            job.Body = null;
            job.BulletTransform = null;
            job.Destination = null;
            job.DestinationTransform = null;
            jobPool.Add(job);
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
            float speed = incomingVelocity.magnitude * exitSpeedMultiplier + extraExitSpeed;
            outgoingVelocity = exitDirection * speed;
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

        /// <summary>
        /// Context data cho mỗi lần chuyển đạn, được tái sử dụng qua Pool để không cấp phát bộ nhớ rác (Zero GC Alloc).
        /// </summary>
        private sealed class PortalTransferJob
        {
            public BulletPortalPair Pair;
            public Rigidbody Body;
            public Transform BulletTransform;
            public Vector3 BulletOriginalScale;
            public Vector3 ExitPosition;
            public Vector3 ExitVelocity;
            public Quaternion ExitRotation;
            public bool WasKinematic;
            public BulletPortal Destination;
            public Transform DestinationTransform;
            public Vector3 DestinationScale;
        }
    }
}
