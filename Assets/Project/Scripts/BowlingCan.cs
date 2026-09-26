using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DreamForgeTD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class BowlingCan : MonoBehaviour, IBulletMechanic, IBulletTrajectoryRule
    {
        [Header("Knockdown detection")]
        [SerializeField, Min(0.05f)] private float knockedDistance = 0.65f;
        [SerializeField, Range(5f, 90f)] private float knockedTiltAngle = 35f;
        [SerializeField, Min(0f)] private float knockedDropDistance = 0.35f;

        [Header("Disappear")]
        [Tooltip("Total realtime from knockdown until the can is removed, including its alpha fade.")]
        [SerializeField, Min(0.1f)] private float knockedDownLifetime = 0.7f;
        [SerializeField, Min(0.01f)] private float disappearAnimationDuration = 0.2f;

        [Header("Impact VFX")]
        [Tooltip("Spawned at bullet-can and can-can contact points.")]
        [SerializeField] private GameObject collisionVfxPrefab;
        [Tooltip("How much of the impact speed along the contact normal the bullet keeps when it bounces away.")]
        [SerializeField, Range(0f, 1f)] private float bulletRestitution = 0.65f;
        [Tooltip("How much sideways speed the bullet keeps when it glances off a can.")]
        [SerializeField, Range(0f, 1f)] private float bulletTangentRetention = 0.95f;

        private Rigidbody body;
        private Collider canCollider;
        private Vector3 startingPosition;
        private Quaternion startingRotation;
        private Vector3 velocityBeforePhysics;
        private Vector3 angularVelocityBeforePhysics;
        private Vector3 centerOfMassBeforePhysics;
        private bool hasBeenHit;
        private bool hasBeenKnocked;
        private readonly List<Material> fadeMaterials = new List<Material>();
        private readonly List<Color> fadeStartColors = new List<Color>();

        public event Action<BowlingCan> KnockedDown;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            canCollider = GetComponent<Collider>();

            body.useGravity = false;
            // The game board is on XY: keep cans in that plane and let them tip around Z.
            body.constraints |= RigidbodyConstraints.FreezePositionZ |
                                RigidbodyConstraints.FreezeRotationX |
                                RigidbodyConstraints.FreezeRotationY;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            canCollider.isTrigger = false;
        }

        private void Start()
        {
            // GameObjectManager applies the level transform after Instantiate/Awake.
            startingPosition = transform.position;
            startingRotation = transform.rotation;
        }

        private void OnDestroy()
        {
            ReleaseFadeMaterials();
        }

        private void FixedUpdate()
        {
            velocityBeforePhysics = body.linearVelocity;
            angularVelocityBeforePhysics = body.angularVelocity;
            centerOfMassBeforePhysics = body.worldCenterOfMass;

            if (hasBeenKnocked || !hasBeenHit)
                return;

            Vector3 displacement = transform.position - startingPosition;
            // Compare against the authored pose, which may itself be rotated to align the model.
            float tiltAngle = Quaternion.Angle(startingRotation, transform.rotation);
            bool isDisplaced = displacement.sqrMagnitude >= knockedDistance * knockedDistance;
            bool isTilted = tiltAngle >= knockedTiltAngle;
            bool hasFallen = displacement.y <= -knockedDropDistance;

            if (isDisplaced || isTilted || hasFallen)
                RegisterKnockdown();
        }

        public void OnBulletHit(BulletHitContext hit)
        {
            SpawnImpactVfx(hit.Point, hit.Normal);
            if (hit.Body != null)
            {
                ApplyCalculatedBulletDeflection(hit);
                ActivatePhysics();
            }
        }

        public BulletTrajectoryResponse PredictTrajectory(BulletTrajectoryHit hit)
        {
            Vector3 normal = hit.Normal;
            if (normal.sqrMagnitude < 0.0001f)
                return BulletTrajectoryResponse.Stop(hit.BulletPosition);
            normal.Normalize();

            Vector3 canVelocityAtContact = body != null
                ? body.GetPointVelocity(hit.ContactPoint)
                : Vector3.zero;
            Vector3 relativeVelocity = hit.IncomingVelocity - canVelocityAtContact;
            if (Vector3.Dot(relativeVelocity, normal) > 0f)
                normal = -normal;

            float incomingNormalSpeed = Vector3.Dot(relativeVelocity, normal);
            if (incomingNormalSpeed >= -0.001f)
                return BulletTrajectoryResponse.Stop(hit.BulletPosition);

            Vector3 tangentVelocity = relativeVelocity - normal * incomingNormalSpeed;
            Vector3 outgoingVelocity = canVelocityAtContact +
                tangentVelocity * bulletTangentRetention -
                normal * incomingNormalSpeed * bulletRestitution;
            Vector3 nextPosition = hit.BulletPosition + normal * hit.CollisionSkin;
            return BulletTrajectoryResponse.Continue(nextPosition, outgoingVelocity);
        }

        private void OnCollisionEnter(Collision collision)
        {
            BowlingCan otherCan = collision.collider.GetComponentInParent<BowlingCan>();
            if (otherCan == null || otherCan == this)
                return;

            // Ignore resting contacts at spawn; moving contacts start a bowling chain.
            bool movingCollision = collision.relativeVelocity.sqrMagnitude > 0.04f;
            if (!hasBeenHit && !otherCan.hasBeenHit && !movingCollision)
                return;

            // Unity sends the contact callback to both cans; spawn one effect for the pair.
            if (GetInstanceID() < otherCan.GetInstanceID() && collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                SpawnImpactVfx(contact.point, contact.normal);
            }

            ActivatePhysics();
            otherCan.ActivatePhysics();
        }

        private void ActivatePhysics()
        {
            if (hasBeenKnocked)
                return;

            hasBeenHit = true;
            body.useGravity = true;
            body.WakeUp();
        }

        private void RegisterKnockdown()
        {
            if (hasBeenKnocked)
                return;

            hasBeenKnocked = true;
            KnockedDown?.Invoke(this);
            StartCoroutine(DisappearAfterFalling());
        }

        private IEnumerator DisappearAfterFalling()
        {
            float fadeDuration = Mathf.Min(disappearAnimationDuration, knockedDownLifetime);
            float fallingDuration = Mathf.Max(0f, knockedDownLifetime - fadeDuration);
            if (fallingDuration > 0f)
                yield return new WaitForSecondsRealtime(fallingDuration);

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            if (canCollider != null)
                canCollider.enabled = false;

            CacheTransparentMaterials();
            float duration = Mathf.Max(0.01f, fadeDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                SetFadeAlpha(1f - Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }

            SetFadeAlpha(0f);
            ReleaseFadeMaterials();
            Destroy(gameObject);
        }

        private void ApplyCalculatedBulletDeflection(BulletHitContext hit)
        {
            if (body.isKinematic || hit.Body.isKinematic || hit.Body == body)
                return;

            Vector3 normal = hit.Normal;
            if (normal.sqrMagnitude < 0.0001f)
                return;
            normal.Normalize();

            Vector3 incomingVelocity = hit.IncomingVelocity.sqrMagnitude > 0.0001f
                ? hit.IncomingVelocity
                : hit.Body.linearVelocity;
            Vector3 bulletVelocityAtContact = incomingVelocity +
                Vector3.Cross(hit.IncomingAngularVelocity, hit.Point - hit.IncomingCenterOfMass);
            Vector3 canVelocityAtContact = velocityBeforePhysics +
                Vector3.Cross(angularVelocityBeforePhysics, hit.Point - centerOfMassBeforePhysics);
            Vector3 relativeVelocity = bulletVelocityAtContact - canVelocityAtContact;

            if (Vector3.Dot(relativeVelocity, normal) > 0f)
                normal = -normal;

            float incomingNormalSpeed = Vector3.Dot(relativeVelocity, normal);
            if (incomingNormalSpeed >= -0.001f)
                return;

            // Replace the solver's near-zero-restitution result with a visible, contact-based
            // ricochet. A centered hit reverses the bullet along the impact line; an off-center
            // hit also changes its direction according to the can's surface normal and motion.
            Vector3 tangentVelocity = relativeVelocity - normal * incomingNormalSpeed;
            Vector3 outgoingRelativeVelocity = tangentVelocity * bulletTangentRetention -
                                               normal * incomingNormalSpeed * bulletRestitution;
            Vector3 outgoingContactVelocity = canVelocityAtContact + outgoingRelativeVelocity;
            Vector3 bulletAngularContactVelocity = Vector3.Cross(
                hit.IncomingAngularVelocity, hit.Point - hit.IncomingCenterOfMass);
            hit.Body.linearVelocity = outgoingContactVelocity - bulletAngularContactVelocity;
            hit.Body.WakeUp();
        }

        private void CacheTransparentMaterials()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                renderers[rendererIndex].shadowCastingMode = ShadowCastingMode.Off;
                Material[] materials = renderers[rendererIndex].materials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                        continue;

                    Color startColor = GetMaterialColor(material);
                    ConfigureTransparentMaterial(material);
                    fadeMaterials.Add(material);
                    fadeStartColors.Add(startColor);
                }
            }
        }

        private static Color GetMaterialColor(Material material)
        {
            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color"))
                return material.GetColor("_Color");
            return Color.white;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);

            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (!material.HasProperty("_Surface") && material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 2f);
                material.EnableKeyword("_ALPHABLEND_ON");
            }
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private void SetFadeAlpha(float alpha)
        {
            for (int i = 0; i < fadeMaterials.Count; i++)
            {
                Material material = fadeMaterials[i];
                if (material == null)
                    continue;

                Color color = fadeStartColors[i];
                color.a *= alpha;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            }
        }

        private void ReleaseFadeMaterials()
        {
            for (int i = 0; i < fadeMaterials.Count; i++)
            {
                if (fadeMaterials[i] != null)
                    Destroy(fadeMaterials[i]);
            }

            fadeMaterials.Clear();
            fadeStartColors.Clear();
        }

        private void SpawnImpactVfx(Vector3 position, Vector3 normal)
        {
            if (collisionVfxPrefab == null)
                return;

            SpawnVfx(collisionVfxPrefab, position, normal);
        }

        private static void SpawnVfx(GameObject prefab, Vector3 position, Vector3 direction)
        {
            Quaternion rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.FromToRotation(Vector3.forward, direction.normalized)
                : Quaternion.identity;
            GameObject effect = Instantiate(prefab, position, rotation);
            ParticleSystem[] particleSystems = effect.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i] != null)
                    particleSystems[i].Play(true);
            }

            if (effect.GetComponent<VfxAutoCleanup>() == null)
                effect.AddComponent<VfxAutoCleanup>();
        }
    }
}
