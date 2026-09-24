using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DreamForgeTD
{
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

        [Tooltip("Khoảng cách kéo lùi xuống dưới tối thiểu (pixel) để bắt đầu tích lực và kích hoạt bắn.")]
        [SerializeField, Min(10f)] private float minPullDistance = 40f;

        [Tooltip("Khoảng cách kéo lùi xuống dưới tối đa (pixel) để đạt 100% lực bắn cực đại.")]
        [SerializeField, Min(50f)] private float maxPullDistance = 280f;

        [Tooltip("Lực bắn tối thiểu (khi vừa chạm ngưỡng kéo tối thiểu).")]
        [SerializeField, Min(1f)] private float minForce = 12f;

        [Tooltip("Lực bắn tối đa (khi kéo kịch tầm tối đa).")]
        [SerializeField, Min(1f)] private float maxForce = 35f;

        [Tooltip("Góc xoay tối đa sang 2 bên trái/phải (độ).")]
        [SerializeField, Range(15f, 90f)] private float maxAimAngle = 75f;

        [Tooltip("Tốc độ xoay nòng mượt mà (độ/giây).")]
        [SerializeField, Min(60f)] private float aimRotateSpeed = 1080f;

        // Trạng thái công khai cho BulletTrajectoryPreview và UI truy xuất
        public bool IsPulling => isPulling;
        public float CurrentLaunchForce => currentLaunchForce;
        public float PullRatio => pullRatio;

        private Vector3 initialPosition;
        private float fixedX;
        private float fixedY;
        private float baseAngleZ;
        private CannonShooter shooter;

        private bool isPulling;
        private Vector2 dragStartPos;
        private float pullRatio;
        private float currentLaunchForce;

        private void Awake()
        {
            shooter = GetComponent<CannonShooter>();
            currentLaunchForce = minForce;
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

            // Lưu cố định vị trí và góc gốc ban đầu
            initialPosition = transform.position;
            fixedX = transform.localEulerAngles.x;
            fixedY = transform.localEulerAngles.y;
            baseAngleZ = transform.localEulerAngles.z;
        }

        private void LateUpdate()
        {
            // Cố định tuyệt đối vị trí Transform
            transform.position = initialPosition;

            if (enablePullBack)
            {
                HandlePullBack();
            }
            else
            {
                // Chế độ ngắm trực tiếp theo chuột cũ
                if (IsHoldingMouse())
                {
                    RotateTowardsMouse();
                }
            }
        }

        private void HandlePullBack()
        {
            if (cam == null) return;

            Vector2 mousePos = GetMousePosition();
            Vector3 screenPos3D = cam.WorldToScreenPoint(transform.position);
            Vector2 cannonScreenPos = new Vector2(screenPos3D.x, screenPos3D.y);

            // 1. Chạm/Click chuột xuống vùng pháo: Chỉ ghi nhận điểm bắt đầu chạm (không xoay, không đổi lực)
            if (IsMousePressedThisFrame())
            {
                float distToCannon = Vector2.Distance(mousePos, cannonScreenPos);
                if (distToCannon <= activationRadius)
                {
                    isPulling = true;
                    dragStartPos = mousePos;
                    pullRatio = 0f;
                    currentLaunchForce = minForce;
                }
            }

            // 2. Di chuyển kéo tay
            if (isPulling && IsHoldingMouse())
            {
                // Vector kéo lùi (tương tự pullX, pullY trong prototype)
                // Kéo xuống dưới -> mousePos.y < dragStartPos.y -> pullY dương
                // Kéo sang trái -> mousePos.x < dragStartPos.x -> pullX dương (hướng bắn sang phải)
                float pullX = dragStartPos.x - mousePos.x;
                float pullY = dragStartPos.y - mousePos.y;

                // Lực chỉ tăng khi kéo tay di chuyển XUỐNG PHÍA DƯỚI
                float pullDownDistance = Mathf.Max(0f, pullY);

                pullRatio = Mathf.Clamp01((pullDownDistance - minPullDistance) / (maxPullDistance - minPullDistance));
                currentLaunchForce = Mathf.Lerp(minForce, maxForce, pullRatio);

                // Tính góc xoay nòng:
                // Mặc định nòng giữ thẳng tuyệt đối (0 độ)
                float targetAngle = baseAngleZ + angleOffset;

                // CHỈ xoay nòng khi kéo sang ngang 2 bên trái/phải MẠNH (vượt qua horizontalAimThreshold)
                float absX = Mathf.Abs(pullX);
                if (absX >= horizontalAimThreshold && pullDownDistance > 10f)
                {
                    // Lực kéo ngang hiệu dụng (trừ đi ngưỡng ban đầu để góc chuyển êm mượt)
                    float effectiveX = (absX - horizontalAimThreshold) * Mathf.Sign(pullX);

                    // Kéo sang trái (pullX > 0) -> nòng nghiêng sang phải (góc âm trong Unity)
                    // Kéo sang phải (pullX < 0) -> nòng nghiêng sang trái (góc dương trong Unity)
                    float steerAngle = -Mathf.Atan2(effectiveX, pullDownDistance) * Mathf.Rad2Deg;
                    steerAngle = Mathf.Clamp(steerAngle, -maxAimAngle, maxAimAngle);

                    targetAngle += steerAngle;
                }

                // Xoay nòng mượt mà về targetAngle
                float currentAngle = transform.localEulerAngles.z;
                float smoothedAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, aimRotateSpeed * Time.deltaTime);
                transform.localEulerAngles = new Vector3(fixedX, fixedY, smoothedAngle);
            }

            // 3. Thả tay/Nhả chuột ra -> Bắn đạn nếu kéo xuống đủ ngưỡng lực
            if (isPulling && IsMouseReleasedThisFrame())
            {
                float pullY = dragStartPos.y - mousePos.y;
                float pullDownDistance = Mathf.Max(0f, pullY);

                if (pullDownDistance >= minPullDistance && shooter != null)
                {
                    shooter.ShootWithForce(currentLaunchForce);
                }

                isPulling = false;
                pullRatio = 0f;
                currentLaunchForce = minForce;
            }
        }

        private void RotateTowardsMouse()
        {
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(transform.position);
            Vector2 mousePos = GetMousePosition();
            Vector2 dir = mousePos - (Vector2)screenPos;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + angleOffset;
            transform.localEulerAngles = new Vector3(fixedX, fixedY, angle);
        }

        private bool IsHoldingMouse()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
            return Input.GetMouseButton(0);
#endif
        }

        private bool IsMousePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        private bool IsMouseReleasedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
            return Input.GetMouseButtonUp(0);
#endif
        }

        private Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
    }
}
