using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DreamForgeTD
{
    [DefaultExecutionOrder(-100)]
    public class CannonController : MonoBehaviour
    {
        [Header("Camera & Visuals")]
        [SerializeField] private Camera cam;
        [Tooltip("Bù góc nếu nòng bị lệch hướng (mặc định 0)")]
        [SerializeField] private float angleOffset = 0f;

        [Header("Pull-Back Mechanic (Kéo lùi tăng lực & ngắm)")]
        [Tooltip("Bật chế độ kéo lùi để tăng lực và ngắm bắn (giống prototype).")]
        [SerializeField] private bool enablePullBack = true;

        [Tooltip("Bán kính tối đa (pixel màn hình) quanh tâm pháo để nhận diện chạm vào pháo.")]
        [SerializeField, Min(10f)] private float activationRadius = 200f;

        [Tooltip("Ngưỡng kéo ngang tối thiểu (pixel) để bắt đầu xoay nòng. Kéo thẳng xuống sẽ giữ thẳng nòng, chỉ khi kéo lệch ngang 2 bên trái/phải mạnh mới xoay.")]
        [SerializeField, Min(5f)] private float horizontalAimThreshold = 35f;

        [Tooltip("Ngưỡng kéo rất nhỏ (pixel) để phân biệt nhả sau khi kéo với một lần chạm. Lực vẫn tăng liên tục từ 0.")]
        [SerializeField, Min(0f)] private float minPullDistance = 1f;

        [Tooltip("Khoảng cách kéo lùi xuống dưới tối đa (pixel) để đạt 100% lực bắn cực đại.")]
        [SerializeField, Min(50f)] private float maxPullDistance = 280f;

        [Tooltip("Lực bắn tại điểm bắt đầu kéo. Đặt 0 để lực tăng liên tục từ trạng thái đứng yên.")]
        [SerializeField, Min(0f)] private float minForce = 0f;

        [Tooltip("Lực bắn tối đa (khi kéo kịch tầm tối đa).")]
        [SerializeField, Min(1f)] private float maxForce = 35f;

        [Tooltip("Góc xoay tối đa sang 2 bên trái/phải (độ).")]
        [SerializeField, Range(15f, 90f)] private float maxAimAngle = 75f;

        [Tooltip("Tốc độ xoay nòng mượt mà (độ/giây).")]
        [SerializeField, Min(60f)] private float aimRotateSpeed = 1080f;

        [Tooltip("Thời gian làm mượt góc ngắm. Giá trị nhỏ giúp pháo bám tay nhanh mà không giật.")]
        [SerializeField, Min(0.01f)] private float aimSmoothTime = 0.07f;

        // Trạng thái công khai cho BulletTrajectoryPreview và UI truy xuất
        public bool IsPulling => isPulling;
        public float CurrentLaunchForce => currentLaunchForce;
        public float PullRatio => pullRatio;

        public float GetLaunchForceRatio(float force)
        {
            return Mathf.InverseLerp(minForce, maxForce, force);
        }

        private Vector3 initialPosition;
        private Vector3 authoredPosition;
        private Quaternion authoredRotation;
        private float fixedX;
        private float fixedY;
        private float baseAngleZ;
        private CannonShooter shooter;

        private bool isPulling;
        private bool hasCapturedAuthoredPose;
        private Vector2 dragStartPos;
        private float pullRatio;
        private float currentLaunchForce;
        private float aimAngularVelocity;

        private void Awake()
        {
            shooter = GetComponent<CannonShooter>();
            currentLaunchForce = minForce;
            CaptureAuthoredPose();
        }

        private void Start()
        {
            if (cam == null)
            {
                cam = Camera.main;
            }

            if (cam == null)
            {
                Debug.LogError("CannonController: Assign Camera in the Inspector.", this);
                enabled = false;
                return;
            }

            if (!hasCapturedAuthoredPose)
                CaptureAuthoredPose();
        }

        private void CaptureAuthoredPose()
        {
            initialPosition = transform.position;
            authoredPosition = transform.position;
            authoredRotation = transform.rotation;
            fixedX = transform.localEulerAngles.x;
            fixedY = transform.localEulerAngles.y;
            baseAngleZ = transform.localEulerAngles.z;
            hasCapturedAuthoredPose = true;
        }

        public void ApplyLevelPlacement(Vector3 worldPosition, Quaternion worldRotation)
        {
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            initialPosition = worldPosition;
            Vector3 localAngles = transform.localEulerAngles;
            fixedX = localAngles.x;
            fixedY = localAngles.y;
            baseAngleZ = localAngles.z;
        }

        public void ResetLevelPlacement()
        {
            transform.SetPositionAndRotation(authoredPosition, authoredRotation);
            initialPosition = authoredPosition;
            Vector3 localAngles = transform.localEulerAngles;
            fixedX = localAngles.x;
            fixedY = localAngles.y;
            baseAngleZ = localAngles.z;
        }

        private void Update()
        {
            if (enablePullBack)
            {
                HandlePullBack();
            }
            else
            {
                // Chế độ ngắm trực tiếp theo chuột cũ
                if (IsPointerHeld())
                {
                    RotateTowardsMouse();
                }
            }
        }

        private void LateUpdate()
        {
            // Giữ vị trí pháo ổn định nếu có hệ thống khác tác động lên Transform.
            transform.position = initialPosition;
        }

        private void HandlePullBack()
        {
            if (cam == null) return;

            Vector2 mousePos = GetPointerPosition();
            Vector3 screenPos3D = cam.WorldToScreenPoint(transform.position);
            Vector2 cannonScreenPos = new Vector2(screenPos3D.x, screenPos3D.y);
            float screenScale = GetScreenScale();

            // 1. Chạm/Click chuột xuống vùng pháo: Chỉ ghi nhận điểm bắt đầu chạm (không xoay, không đổi lực)
            if (IsPointerPressedThisFrame())
            {
                float distToCannon = Vector2.Distance(mousePos, cannonScreenPos);
                if (distToCannon <= activationRadius * screenScale)
                {
                    isPulling = true;
                    dragStartPos = mousePos;
                    pullRatio = 0f;
                    currentLaunchForce = minForce;
                    aimAngularVelocity = 0f;
                }
            }

            // Cập nhật cả frame nhả để lực và góc bắn khớp vị trí con trỏ cuối cùng.
            bool pointerReleased = IsPointerReleasedThisFrame();
            if (isPulling && (IsPointerHeld() || pointerReleased))
            {
                float pullX = dragStartPos.x - mousePos.x;
                float pullY = dragStartPos.y - mousePos.y;
                float pullDownDistance = Mathf.Max(0f, pullY);
                float scaledMaxPullDistance = maxPullDistance * screenScale;

                pullRatio = Mathf.Clamp01(pullDownDistance / Mathf.Max(1f, scaledMaxPullDistance));
                currentLaunchForce = Mathf.Lerp(minForce, maxForce, pullRatio);

                float targetAngle = baseAngleZ + angleOffset;
                float absX = Mathf.Abs(pullX);
                if (absX >= horizontalAimThreshold * screenScale && pullDownDistance > 10f * screenScale)
                {
                    float effectiveX = (absX - horizontalAimThreshold * screenScale) * Mathf.Sign(pullX);
                    float steerAngle = -Mathf.Atan2(effectiveX, pullDownDistance) * Mathf.Rad2Deg;
                    steerAngle = Mathf.Clamp(steerAngle, -maxAimAngle, maxAimAngle);
                    targetAngle += steerAngle;
                }

                float currentAngle = transform.localEulerAngles.z;
                float smoothedAngle = pointerReleased
                    ? targetAngle
                    : Mathf.SmoothDampAngle(currentAngle, targetAngle, ref aimAngularVelocity,
                        aimSmoothTime, aimRotateSpeed, Time.deltaTime);
                transform.localEulerAngles = new Vector3(fixedX, fixedY, smoothedAngle);
            }

            if (isPulling && pointerReleased)
            {
                float pullDownDistance = Mathf.Max(0f, dragStartPos.y - mousePos.y);

                if (pullDownDistance >= minPullDistance * screenScale && shooter != null)
                {
                    shooter.RequestShotWithForce(currentLaunchForce);
                }

                isPulling = false;
                pullRatio = 0f;
                currentLaunchForce = minForce;
            }
        }

        private static float GetScreenScale()
        {
            return Mathf.Max(0.5f, Screen.height / 1080f);
        }

        private void RotateTowardsMouse()
        {
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(transform.position);
            Vector2 mousePos = GetPointerPosition();
            Vector2 dir = mousePos - (Vector2)screenPos;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + angleOffset;
            transform.localEulerAngles = new Vector3(fixedX, fixedY, angle);
        }

        private bool IsPointerHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return true;
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
            if (Input.touchCount > 0)
            {
                TouchPhase phase = Input.GetTouch(0).phase;
                if (phase == TouchPhase.Began || phase == TouchPhase.Moved || phase == TouchPhase.Stationary)
                    return true;
            }
            return Input.GetMouseButton(0);
#endif
        }

        private bool IsPointerPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                return true;
            return Input.GetMouseButtonDown(0);
#endif
        }

        private bool IsPointerReleasedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
                return true;
            return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
            if (Input.touchCount > 0)
            {
                TouchPhase phase = Input.GetTouch(0).phase;
                if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
                    return true;
            }
            return Input.GetMouseButtonUp(0);
#endif
        }

        private Vector2 GetPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null &&
                (Touchscreen.current.primaryTouch.press.isPressed ||
                 Touchscreen.current.primaryTouch.press.wasReleasedThisFrame))
                return Touchscreen.current.primaryTouch.position.ReadValue();
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            if (Input.touchCount > 0)
                return Input.GetTouch(0).position;
            return Input.mousePosition;
#endif
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                CancelPull();
        }

        private void OnDisable()
        {
            CancelPull();
        }

        private void CancelPull()
        {
            isPulling = false;
            pullRatio = 0f;
            currentLaunchForce = minForce;
            aimAngularVelocity = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
    }
}
