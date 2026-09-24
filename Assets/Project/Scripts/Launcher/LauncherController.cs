using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DreamForgeTD.PhysicsPuzzle
{
    // Attach to a raycastable UI aim area. EventSystem handles mouse/touch ownership.
    public sealed class LauncherController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Transform barrel;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Projectile projectile;
        [SerializeField] private TrajectoryPreview preview;
        [SerializeField, Min(0.01f)] private float maxDragDistance = 3f;
        [SerializeField, Min(0f)] private float minDragDistance = 0.15f;
        [SerializeField, Min(0.01f)] private float minPower = 3f;
        [SerializeField, Min(0.01f)] private float maxPower = 18f;

        private bool canFire;
        private bool aiming;
        private int pointerId;
        private Vector3 dragStart;
        private Vector3 velocity;
        private Quaternion initialRotation;
        private Plane aimPlane;
        public Projectile Projectile => projectile;
        public bool IsConfigured => aimCamera != null && barrel != null && muzzle != null && projectile != null;
        public event Action Fired;

        private void Awake()
        {
            if (!IsConfigured || maxDragDistance <= minDragDistance || maxPower < minPower)
            {
                Debug.LogError($"{name}: Assign camera, barrel, muzzle, projectile; check drag/power ranges.", this);
                enabled = false;
                return;
            }
            initialRotation = barrel.rotation;
        }

        public void SetCanFire(bool value)
        {
            canFire = value;
            if (!value) CancelAim();
        }

        public void PrepareShot(bool resetRotation = false)
        {
            CancelAim();
            if (resetRotation) barrel.rotation = initialRotation;
            projectile.Prepare(muzzle.position, muzzle.rotation);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!enabled || !canFire || aiming || eventData.button != PointerEventData.InputButton.Left) return;
            aimPlane = new Plane(Vector3.forward, muzzle.position);
            if (!TryGetWorld(eventData.position, out dragStart)) return;
            pointerId = eventData.pointerId;
            aiming = true;
            velocity = Vector3.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (aiming && pointerId == eventData.pointerId) UpdateAim(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!aiming || pointerId != eventData.pointerId) return;
            UpdateAim(eventData.position);
            Vector3 launchVelocity = velocity;
            CancelAim();
            if (!canFire || launchVelocity.sqrMagnitude == 0f) return;
            projectile.Prepare(muzzle.position, muzzle.rotation);
            if (!projectile.Launch(launchVelocity)) return;
            canFire = false;
            Fired?.Invoke();
        }

        private void UpdateAim(Vector2 screenPosition)
        {
            velocity = Vector3.zero;
            if (!TryGetWorld(screenPosition, out Vector3 current)) { preview?.Hide(); return; }
            Vector3 drag = dragStart - current;
            float distance = drag.magnitude;
            if (distance < minDragDistance || distance < 0.0001f) { preview?.Hide(); return; }
            float power = Mathf.Lerp(minPower, maxPower, Mathf.InverseLerp(minDragDistance, maxDragDistance, distance));
            velocity = drag / distance * Mathf.Min(power, projectile.MaxSpeed);
            // Barrel artwork points along local +Y; physics takes place in world XY.
            barrel.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(drag.y, drag.x) * Mathf.Rad2Deg - 90f);
            projectile.Prepare(muzzle.position, muzzle.rotation);
            Vector3 gravity = projectile.Rigidbody.useGravity ? Physics.gravity : Vector3.zero;
            preview?.Show(muzzle.position, velocity, gravity, projectile.MaxSpeed);
        }

        private bool TryGetWorld(Vector2 screenPosition, out Vector3 position)
        {
            Ray ray = aimCamera.ScreenPointToRay(screenPosition);
            if (aimPlane.Raycast(ray, out float distance)) { position = ray.GetPoint(distance); return true; }
            position = default;
            return false;
        }

        private void CancelAim()
        {
            aiming = false;
            velocity = Vector3.zero;
            preview?.Hide();
        }

        private void OnDisable() => CancelAim();
        private void OnApplicationFocus(bool focused) { if (!focused) CancelAim(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelAim(); }
    }
}
