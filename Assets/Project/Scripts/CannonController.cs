using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DreamForgeTD
{
    public class CannonController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Camera cam;
        [Tooltip("Bù góc nếu nòng bị lệch hướng (vd: -90, 0, 90, 180)")]
        [SerializeField] private float angleOffset = 0f;

        private Vector3 initialPosition;
        private float fixedX;
        private float fixedY;

        private void Start()
        {
            if (cam == null)
            {
                Debug.LogError("CannonController: Assign Camera in the Inspector.", this);
                enabled = false;
                return;
            }

            // Lưu cố định vị trí và trục X, Y ban đầu
            initialPosition = transform.position;
            fixedX = transform.localEulerAngles.x;
            fixedY = transform.localEulerAngles.y;
        }

        private void LateUpdate()
        {
            // Cố định tuyệt đối vị trí Transform
            transform.position = initialPosition;

            // Chỉ click và GIỮ chuột trái thì mới xoay
            if (IsHoldingMouse())
            {
                RotateTowardsMouse();
            }
        }

        private void RotateTowardsMouse()
        {
            if (cam == null) return;

            // Đổi vị trí vật thể sang tọa độ màn hình
            Vector3 screenPos = cam.WorldToScreenPoint(transform.position);

            // Hướng từ vật thể đến chuột
            Vector2 mousePos = GetMousePosition();
            Vector2 dir = mousePos - (Vector2)screenPos;

            // Tính góc và chỉ xoay duy nhất trục Z
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
            // Quả cầu vàng nhỏ tại tâm xoay để quan sát trên Scene view
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
    }
}
