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
        private const float MovementSpeedThreshold = 0.1f;
        private const float RotationSpeedThreshold = 0.1f;

        [Header("Disappear")]
        [Tooltip("Realtime from the first confirmed impact until the can is removed. The can keeps moving while it fades.")]
        [SerializeField, Min(0.1f)] private float knockedDownLifetime = 1.2f;
        [Tooltip("How long the can fades for, starting at the first confirmed impact.")]
        [SerializeField, Min(0.01f)] private float disappearAnimationDuration = 0.2f;

        [Header("Physics")]
        [SerializeField, Min(0.1f)] private float maximumUpwardSpeed = 4f;

        [Header("Impact VFX")]
        [Tooltip("Spawned at bullet-can and can-can contact points.")]
        [SerializeField] private GameObject collisionVfxPrefab;
        [Tooltip("How much of the impact speed along the contact normal the bullet keeps when it bounces away.")]
        [SerializeField, Range(0f, 1f)] private float bulletRestitution = 0.65f;
        [Tooltip("How much sideways speed the bullet keeps when it glances off a can.")]
        [SerializeField, Range(0f, 1f)] private float bulletTangentRetention = 0.95f;

        private Rigidbody body;
        private Collider canCollider;
        private Vector3 velocityBeforePhysics;
        private Vector3 angularVelocityBeforePhysics;
        private Vector3 centerOfMassBeforePhysics;
        private bool hasBeenHit;
        private bool hasTriggeredDestruction;
        private bool hasReportedHidden;
        private bool hasQueuedDestroy;
        private float destroyAtRealtime;
        private readonly List<Material> fadeMaterials = new List<Material>();
        private readonly List<Color> fadeStartColors = new List<Color>();
        private readonly List<Renderer> fadeRenderers = new List<Renderer>();
        private readonly List<Material[]> originalSharedMaterials = new List<Material[]>();
        private readonly List<ShadowCastingMode> originalShadowCastingModes = new List<ShadowCastingMode>();

        public event Action<BowlingCan> KnockedDown;
        public event Action<BowlingCan> Hidden;
        public bool DangChuyenDong => body != null && !body.isKinematic &&
            (body.linearVelocity.sqrMagnitude > MovementSpeedThreshold * MovementSpeedThreshold ||
             body.angularVelocity.sqrMagnitude > RotationSpeedThreshold * RotationSpeedThreshold);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            canCollider = GetComponent<Collider>();

            body.isKinematic = true;
            body.useGravity = false;
            // The game board is on XY: keep cans in that plane and let them tip around Z.
            body.constraints |= RigidbodyConstraints.FreezePositionZ |
                                RigidbodyConstraints.FreezeRotationX |
                                RigidbodyConstraints.FreezeRotationY;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            canCollider.isTrigger = false;
        }

        private void OnDisable()
        {
            // Script recompiles also call OnDisable while the GameObject is still active.
            // Only treat an actual GameObject deactivation as hidden here.
            if (!gameObject.activeInHierarchy)
                NotifyHidden();
        }

        private void OnDestroy()
        {
            NotifyHidden();
            ReleaseFadeMaterials();
        }

        public void ResetForSpawn()
        {
            StopAllCoroutines();
            ReleaseFadeMaterials();

            if (body == null)
                body = GetComponent<Rigidbody>();
            if (canCollider == null)
                canCollider = GetComponent<Collider>();

            hasBeenHit = false;
            hasTriggeredDestruction = false;
            hasReportedHidden = false;
            hasQueuedDestroy = false;
            destroyAtRealtime = 0f;
            velocityBeforePhysics = Vector3.zero;
            angularVelocityBeforePhysics = Vector3.zero;
            centerOfMassBeforePhysics = transform.position;

            if (body != null)
            {
                // New rigidbodies start at rest; pooled bodies have already had their
                // velocities cleared in PrepareForPool. Keep the can frozen until a hit.
                body.isKinematic = true;
                body.useGravity = false;
                body.constraints |= RigidbodyConstraints.FreezePositionZ |
                                    RigidbodyConstraints.FreezeRotationX |
                                    RigidbodyConstraints.FreezeRotationY;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.position = transform.position;
                body.rotation = transform.rotation;
                body.Sleep();
            }

            if (canCollider != null)
            {
                canCollider.enabled = true;
                canCollider.isTrigger = false;
            }
        }

        public void PrepareForPool()
        {
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.useGravity = false;
                body.isKinematic = true;
            }

            if (canCollider != null)
                canCollider.enabled = false;

            ReleaseFadeMaterials();
            KnockedDown = null;
            Hidden = null;
        }

        private void FixedUpdate()
        {
            if (body == null)
                return;

            if (!body.isKinematic && body.linearVelocity.y > maximumUpwardSpeed)
            {
                Vector3 velocity = body.linearVelocity;
                velocity.y = maximumUpwardSpeed;
                body.linearVelocity = velocity;
            }

            velocityBeforePhysics = body.linearVelocity;
            angularVelocityBeforePhysics = body.angularVelocity;
            centerOfMassBeforePhysics = body.worldCenterOfMass;
        }

        private void Update()
        {
            if (hasTriggeredDestruction && !hasQueuedDestroy && Time.realtimeSinceStartup >= destroyAtRealtime)
                FinishDisappearance();
        }

        public void OnBulletHit(BulletHitContext hit)
        {
            SpawnImpactVfx(hit.Point, hit.Normal);

            bool isFirstHit = !hasBeenHit && !hasTriggeredDestruction;
            bool mustTransferImpactImpulse = isFirstHit && body != null && body.isKinematic;
            if (isFirstHit)
                EnablePhysicsAfterImpact();

            if (hit.Body != null)
                ApplyCalculatedBulletDeflection(hit, mustTransferImpactImpulse);

            if (isFirstHit)
            {
                // Register only after deflection so listeners cannot interrupt the hit response.
                hasBeenHit = true;
                RegisterKnockdown();
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
            if (otherCan == this)
                return;

            if (otherCan == null)
            {
                float minimumImpactSpeedSquared = MovementSpeedThreshold * MovementSpeedThreshold;
                bool hasMeaningfulImpact = collision.relativeVelocity.sqrMagnitude > minimumImpactSpeedSquared;
                if (hasBeenHit && hasMeaningfulImpact && collision.contactCount > 0)
                {
                    ContactPoint contact = collision.GetContact(0);
                    GameAudio.PlayCanCollision(contact.point, collision.relativeVelocity.magnitude);
                }

                return;
            }

            // Static/setup contacts between cans must not count as a hit. A can can only
            // knock another can down after a bullet or an already-hit can started the chain.
            bool isKnockdownImpact = hasBeenHit || otherCan.hasBeenHit;
            if (!isKnockdownImpact)
                return;

            // Unity sends the contact callback to both cans; spawn one effect for the pair.
            if (GetInstanceID() < otherCan.GetInstanceID() && collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                SpawnImpactVfx(contact.point, contact.normal);
                GameAudio.PlayCanCollision(contact.point, collision.relativeVelocity.magnitude);
            }

            // Let the active dynamic can handle the pair so a kinematic neighbor receives
            // the opposite of the solver impulse exactly once.
            if (body.isKinematic && !otherCan.body.isKinematic && otherCan.hasBeenHit)
                return;

            if (!body.isKinematic && hasBeenHit && otherCan.body.isKinematic && collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                Vector3 chainImpulse = -collision.impulse;
                if (chainImpulse.sqrMagnitude <= 0.000001f)
                    chainImpulse = (velocityBeforePhysics - body.linearVelocity) * body.mass;

                otherCan.ActivatePhysics(chainImpulse, contact.point);
                return;
            }

            ActivatePhysics();
            otherCan.ActivatePhysics();
        }

        private void ActivatePhysics()
        {
            ActivatePhysics(Vector3.zero, transform.position);
        }

        private void ActivatePhysics(Vector3 impactImpulse, Vector3 impactPoint)
        {
            if (hasBeenHit || hasTriggeredDestruction)
                return;

            hasBeenHit = true;
            EnablePhysicsAfterImpact();
            if (impactImpulse.sqrMagnitude > 0.000001f)
                body.AddForceAtPosition(impactImpulse, impactPoint, ForceMode.Impulse);
            RegisterKnockdown();
        }

        private void EnablePhysicsAfterImpact()
        {
            if (body == null)
                return;

            body.isKinematic = false;
            body.useGravity = true;
            body.WakeUp();
        }

        private void RegisterKnockdown()
        {
            if (hasTriggeredDestruction)
                return;

            hasTriggeredDestruction = true;
            destroyAtRealtime = Time.realtimeSinceStartup + Mathf.Max(0.01f, knockedDownLifetime);
            StartCoroutine(DisappearAfterFalling());
            KnockedDown?.Invoke(this);
        }

        private IEnumerator DisappearAfterFalling()
        {
            CacheTransparentMaterials();
            float lifetime = Mathf.Max(0.01f, knockedDownLifetime);
            float fadeDuration = Mathf.Min(Mathf.Max(0.01f, disappearAnimationDuration), lifetime);
            float fadeStartedAt = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup < destroyAtRealtime)
            {
                float elapsed = Time.realtimeSinceStartup - fadeStartedAt;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                SetFadeAlpha(1f - Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }

            FinishDisappearance();
        }

        private void FinishDisappearance()
        {
            if (hasQueuedDestroy)
                return;

            hasQueuedDestroy = true;
            SetFadeAlpha(0f);
            NotifyHidden();
            ReleaseFadeMaterials();

            GameObjectManager manager = GetComponentInParent<GameObjectManager>();
            if (manager == null || !manager.ReturnToPool(gameObject))
                Destroy(gameObject);
        }

        private void NotifyHidden()
        {
            if (hasReportedHidden)
                return;

            hasReportedHidden = true;
            Hidden?.Invoke(this);
        }

        private void ApplyCalculatedBulletDeflection(BulletHitContext hit, bool transferImpactImpulse)
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
            Vector3 outgoingBulletVelocity = outgoingContactVelocity - bulletAngularContactVelocity;

            // A kinematic can receives no solver impulse. Transfer the bullet's momentum change
            // on the first hit; later impacts use the normal dynamic Rigidbody solver response.
            if (transferImpactImpulse)
            {
                Vector3 impactImpulse = (incomingVelocity - outgoingBulletVelocity) * hit.Body.mass;
                if (impactImpulse.sqrMagnitude > 0.000001f)
                    body.AddForceAtPosition(impactImpulse, hit.Point, ForceMode.Impulse);
            }

            hit.Body.linearVelocity = outgoingBulletVelocity;
            hit.Body.WakeUp();
        }

        private void CacheTransparentMaterials()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                originalShadowCastingModes.Add(renderer.shadowCastingMode);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                originalSharedMaterials.Add(renderer.sharedMaterials);
                fadeRenderers.Add(renderer);
                Material[] materials = renderer.materials;
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
            for (int i = 0; i < fadeRenderers.Count; i++)
            {
                if (fadeRenderers[i] != null)
                {
                    if (i < originalSharedMaterials.Count)
                        fadeRenderers[i].sharedMaterials = originalSharedMaterials[i];
                    if (i < originalShadowCastingModes.Count)
                        fadeRenderers[i].shadowCastingMode = originalShadowCastingModes[i];
                }
            }

            for (int i = 0; i < fadeMaterials.Count; i++)
            {
                if (fadeMaterials[i] != null)
                    Destroy(fadeMaterials[i]);
            }

            fadeMaterials.Clear();
            fadeStartColors.Clear();
            fadeRenderers.Clear();
            originalSharedMaterials.Clear();
            originalShadowCastingModes.Clear();
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
