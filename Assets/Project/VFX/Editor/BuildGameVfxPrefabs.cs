using System;
using System.Collections.Generic;
using System.IO;
using DreamForgeTD;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DreamForgeTD.Editor
{
    public static class BuildGameVfxPrefabs
    {
        private const string VfxRoot = "Assets/Project/VFX";
        private const string PrefabFolder = VfxRoot + "/Prefabs";
        private const string MaterialFolder = VfxRoot + "/Materials";
        private const string TextureFolder = VfxRoot + "/Textures";
        private const string LibraryPath = "Assets/Resources/VFX/GameVfxLibrary.asset";

        [MenuItem("DreamForge/VFX/Rebuild Starter VFX Prefabs")]
        public static void Build()
        {
            EnsureFolders();

            Texture2D glowTexture = CreateTexture("T_VFX_GlowSoft", MakeGlowTexture());
            Texture2D ringTexture = CreateTexture("T_VFX_RingPulse", MakeRingTexture());
            Texture2D streakTexture = CreateTexture("T_VFX_Streak", MakeStreakTexture());

            Material glow = CreateMaterial("MAT_VFX_GlowSoft", glowTexture);
            Material ring = CreateMaterial("MAT_VFX_RingPulse", ringTexture);
            Material streak = CreateMaterial("MAT_VFX_Streak", streakTexture);

            GameObject muzzle = BuildMuzzleFlash(glow, ring, streak);
            GameObject trail = BuildProjectileTrail(glow, streak);
            GameObject impact = BuildImpact(glow, ring, streak);
            GameObject bounce = BuildBounce(glow, ring, streak);
            GameObject victory = BuildVictory(glow, ring, streak);
            GameObject portalEnter = BuildPortal("FX_Portal_Enter", glow, ring, streak,
                new Color(0.28f, 0.96f, 1f), new Color(0.42f, 0.44f, 1f), 16);
            GameObject portalExit = BuildPortal("FX_Portal_Exit", glow, ring, streak,
                new Color(1f, 0.72f, 0.32f), new Color(0.25f, 0.93f, 1f), 22);
            GameObject charge = BuildChargeLoop(glow, streak);

            GameVfxLibrary library = AssetDatabase.LoadAssetAtPath<GameVfxLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<GameVfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.cannonMuzzleFlash = muzzle;
            library.cannonChargeLoop = charge;
            library.bulletTrail = trail;
            library.bulletImpact = impact;
            library.bulletBounce = bounce;
            library.targetVictory = victory;
            library.portalEnter = portalEnter;
            library.portalExit = portalExit;

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Starter game VFX prefabs and GameVfxLibrary were created in Assets/Project/VFX.");
        }

        private static GameObject BuildMuzzleFlash(Material glow, Material ring, Material streak)
        {
            GameObject root = CreateRoot("FX_Cannon_MuzzleFlash");
            AddSystem(root, "PS_Muzzle_CoreFlash", glow,
                Burst(1, 1, 0.07f, 0.12f, 0.38f, 0.62f, 0.2f,
                    new Color(1f, 0.97f, 0.78f), new Color(1f, 0.59f, 0.2f),
                    ParticleSystemShapeType.Sphere, 0f, 0.05f, 0f));
            AddSystem(root, "PS_Muzzle_ExpandingRing", ring,
                Burst(1, 1, 0.17f, 0.27f, 0.74f, 1.08f, 0.05f,
                    new Color(1f, 0.92f, 0.62f), new Color(1f, 0.37f, 0.1f),
                    ParticleSystemShapeType.Sphere, 0f, 0.02f, 0f, expands: true));
            AddSystem(root, "PS_Muzzle_HotSparks", streak,
                Burst(22, 30, 0.16f, 0.36f, 0.035f, 0.085f, 4.5f,
                    new Color(1f, 0.96f, 0.77f), new Color(1f, 0.42f, 0.1f),
                    ParticleSystemShapeType.Cone, 28f, 0.07f, 0.65f, stretched: true, noise: 0.12f));
            AddSystem(root, "PS_Muzzle_SootPuffs", glow,
                Burst(5, 8, 0.25f, 0.48f, 0.14f, 0.28f, 0.95f,
                    new Color(0.2f, 0.23f, 0.29f), new Color(0.52f, 0.38f, 0.29f),
                    ParticleSystemShapeType.Cone, 38f, 0.1f, 0.08f, noise: 0.3f));
            return SavePrefab(root);
        }

        private static GameObject BuildProjectileTrail(Material glow, Material streak)
        {
            GameObject root = CreateRoot("FX_Bullet_Trail");
            AddSystem(root, "PS_Trail_AetherGlow", glow,
                Loop(15f, 0.15f, 0.24f, 0.075f, 0.15f, 0.01f,
                    new Color(0.55f, 0.98f, 1f), new Color(0.21f, 0.72f, 1f),
                    ParticleSystemShapeType.Sphere, 0.025f, 0f, noise: 0.18f, worldSpace: true));
            AddSystem(root, "PS_Trail_EmberFilaments", streak,
                Loop(8f, 0.11f, 0.2f, 0.025f, 0.055f, 0.45f,
                    new Color(1f, 0.94f, 0.67f), new Color(1f, 0.49f, 0.15f),
                    ParticleSystemShapeType.Cone, 0.025f, 0.1f,
                    stretched: true, worldSpace: true));
            return SavePrefab(root);
        }

        private static GameObject BuildImpact(Material glow, Material ring, Material streak)
        {
            GameObject root = CreateRoot("FX_Bullet_Impact");
            AddSystem(root, "PS_Impact_Flash", glow,
                Burst(1, 1, 0.08f, 0.13f, 0.34f, 0.58f, 0.1f,
                    Color.white, new Color(1f, 0.75f, 0.4f),
                    ParticleSystemShapeType.Sphere, 0f, 0.03f, 0f));
            AddSystem(root, "PS_Impact_ShockRing", ring,
                Burst(1, 1, 0.18f, 0.3f, 0.34f, 0.66f, 0f,
                    new Color(1f, 0.9f, 0.68f), new Color(1f, 0.43f, 0.18f),
                    ParticleSystemShapeType.Sphere, 0f, 0.02f, 0f, expands: true));
            AddSystem(root, "PS_Impact_Sparks", streak,
                Burst(15, 22, 0.18f, 0.42f, 0.026f, 0.068f, 3.4f,
                    new Color(1f, 0.96f, 0.82f), new Color(1f, 0.52f, 0.22f),
                    ParticleSystemShapeType.Sphere, 0f, 0.035f, 1.1f, stretched: true, noise: 0.08f));
            AddSystem(root, "PS_Impact_Dust", glow,
                Burst(6, 10, 0.22f, 0.4f, 0.09f, 0.19f, 0.7f,
                    new Color(0.76f, 0.83f, 0.87f), new Color(0.32f, 0.41f, 0.51f),
                    ParticleSystemShapeType.Sphere, 0f, 0.045f, 0.18f, noise: 0.24f));
            return SavePrefab(root);
        }

        private static GameObject BuildBounce(Material glow, Material ring, Material streak)
        {
            GameObject root = CreateRoot("FX_Bullet_Bounce");
            AddSystem(root, "PS_Bounce_CyanFlash", glow,
                Burst(1, 1, 0.08f, 0.13f, 0.28f, 0.46f, 0.08f,
                    new Color(0.92f, 1f, 1f), new Color(0.15f, 0.84f, 1f),
                    ParticleSystemShapeType.Sphere, 0f, 0.025f, 0f));
            AddSystem(root, "PS_Bounce_RicochetRing", ring,
                Burst(1, 1, 0.2f, 0.31f, 0.4f, 0.72f, 0f,
                    new Color(0.6f, 1f, 1f), new Color(0.15f, 0.6f, 1f),
                    ParticleSystemShapeType.Sphere, 0f, 0.02f, 0f, expands: true));
            AddSystem(root, "PS_Bounce_ShardSparks", streak,
                Burst(12, 18, 0.2f, 0.44f, 0.025f, 0.06f, 2.9f,
                    new Color(0.84f, 1f, 1f), new Color(0.18f, 0.68f, 1f),
                    ParticleSystemShapeType.Cone, 47f, 0.035f, 0.9f, stretched: true, noise: 0.1f));
            return SavePrefab(root);
        }

        private static GameObject BuildVictory(Material glow, Material ring, Material streak)
        {
            GameObject root = CreateRoot("FX_Target_VictoryBurst");
            AddSystem(root, "PS_Victory_PrismaticCore", glow,
                Burst(1, 1, 0.2f, 0.34f, 0.72f, 1.14f, 0.08f,
                    new Color(1f, 1f, 0.9f), new Color(0.27f, 0.96f, 0.83f),
                    ParticleSystemShapeType.Sphere, 0f, 0.05f, 0f));
            AddSystem(root, "PS_Victory_DoubleHalo", ring,
                MultiBurst(new[] { (0f, 1, 1), (0.075f, 1, 1) },
                    0.3f, 0.48f, 0.56f, 0.93f, 0f,
                    new Color(1f, 0.91f, 0.53f), new Color(0.24f, 0.92f, 1f), expands: true));
            AddSystem(root, "PS_Victory_Starburst", streak,
                Burst(30, 42, 0.42f, 0.84f, 0.035f, 0.085f, 4.8f,
                    new Color(1f, 0.98f, 0.78f), new Color(0.27f, 0.98f, 0.83f),
                    ParticleSystemShapeType.Sphere, 0f, 0.04f, 0.28f, stretched: true, noise: 0.14f));
            AddSystem(root, "PS_Victory_RisingShards", glow,
                Burst(9, 14, 0.7f, 1.15f, 0.08f, 0.17f, 1.1f,
                    new Color(1f, 0.72f, 0.31f), new Color(0.21f, 0.77f, 1f),
                    ParticleSystemShapeType.Sphere, 0f, 0.07f, -0.35f, noise: 0.2f));
            return SavePrefab(root);
        }

        private static GameObject BuildPortal(string name, Material glow, Material ring, Material streak,
            Color primary, Color secondary, int sparkCount)
        {
            GameObject root = CreateRoot(name);
            AddSystem(root, "PS_Portal_CoreBloom", glow,
                Burst(1, 1, 0.14f, 0.22f, 0.44f, 0.74f, 0.12f,
                    Color.Lerp(Color.white, primary, 0.25f), primary,
                    ParticleSystemShapeType.Sphere, 0f, 0.04f, 0f));
            AddSystem(root, "PS_Portal_EnergyHoop", ring,
                MultiBurst(new[] { (0f, 1, 1), (0.055f, 1, 1) },
                    0.23f, 0.35f, 0.52f, 0.88f, 0f,
                    primary, secondary, expands: true, rotates: true));
            AddSystem(root, "PS_Portal_ArcSparks", streak,
                Burst(sparkCount, sparkCount + 6, 0.22f, 0.48f, 0.025f, 0.065f, 3.1f,
                    Color.Lerp(Color.white, primary, 0.2f), secondary,
                    ParticleSystemShapeType.Circle, 0f, 0.18f, 0.12f, stretched: true, noise: 0.18f));
            AddSystem(root, "PS_Portal_FloatingRunes", glow,
                Burst(4, 7, 0.32f, 0.58f, 0.07f, 0.13f, 0.85f,
                    primary, secondary, ParticleSystemShapeType.Circle, 0f, 0.24f, -0.12f,
                    noise: 0.2f, rotates: true));
            return SavePrefab(root);
        }

        private static GameObject BuildChargeLoop(Material glow, Material streak)
        {
            GameObject root = CreateRoot("FX_Cannon_ChargeLoop");
            AddSystem(root, "PS_Charge_OrbitingMotes", glow,
                Loop(8f, 0.38f, 0.62f, 0.055f, 0.105f, 0.28f,
                    new Color(0.38f, 0.96f, 1f), new Color(0.24f, 0.56f, 1f),
                    ParticleSystemShapeType.Circle, 0.3f, 0f, noise: 0.22f));
            AddSystem(root, "PS_Charge_ArcFilaments", streak,
                Loop(5f, 0.12f, 0.24f, 0.02f, 0.045f, 0.35f,
                    new Color(1f, 0.94f, 0.69f), new Color(0.28f, 0.86f, 1f),
                    ParticleSystemShapeType.Circle, 0.24f, 0f, stretched: true));
            return SavePrefab(root);
        }

        private static Profile Burst(int minCount, int maxCount, float lifeMin, float lifeMax,
            float sizeMin, float sizeMax, float speed, Color start, Color end,
            ParticleSystemShapeType shape, float angle, float radius, float gravity,
            bool expands = false, bool stretched = false, float noise = 0f, bool rotates = false)
        {
            var profile = new Profile
            {
                Loop = false,
                BurstCounts = new List<(float, int, int)> { (0f, minCount, maxCount) },
                LifetimeMin = lifeMin,
                LifetimeMax = lifeMax,
                SizeMin = sizeMin,
                SizeMax = sizeMax,
                SpeedMin = speed * 0.7f,
                SpeedMax = speed * 1.15f,
                Shape = shape,
                ShapeAngle = angle,
                ShapeRadius = radius,
                Gravity = gravity,
                StartColor = start,
                EndColor = end,
                Expands = expands,
                Stretched = stretched,
                Noise = noise,
                Rotates = rotates,
                MaxParticles = Mathf.Max(48, maxCount + 16)
            };
            return profile;
        }

        private static Profile MultiBurst((float time, int min, int max)[] bursts,
            float lifeMin, float lifeMax, float sizeMin, float sizeMax, float speed,
            Color start, Color end, bool expands = false, bool rotates = false)
        {
            Profile profile = Burst(1, 1, lifeMin, lifeMax, sizeMin, sizeMax, speed,
                start, end, ParticleSystemShapeType.Sphere, 0f, 0.02f, 0f,
                expands: expands, rotates: rotates);
            profile.BurstCounts = new List<(float, int, int)>(bursts);
            return profile;
        }

        private static Profile Loop(float rate, float lifeMin, float lifeMax,
            float sizeMin, float sizeMax, float speed, Color start, Color end,
            ParticleSystemShapeType shape, float radius, float gravity,
            bool stretched = false, float noise = 0f, bool worldSpace = false)
        {
            return new Profile
            {
                Loop = true,
                Rate = rate,
                LifetimeMin = lifeMin,
                LifetimeMax = lifeMax,
                SizeMin = sizeMin,
                SizeMax = sizeMax,
                SpeedMin = speed * 0.5f,
                SpeedMax = speed * 1.2f,
                Shape = shape,
                ShapeRadius = radius,
                Gravity = gravity,
                StartColor = start,
                EndColor = end,
                Stretched = stretched,
                Noise = noise,
                WorldSpace = worldSpace,
                MaxParticles = 120
            };
        }

        private static ParticleSystem AddSystem(GameObject root, string name, Material material, Profile profile)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            ParticleSystem particles = child.AddComponent<ParticleSystem>();

            var main = particles.main;
            main.duration = profile.Loop ? 1f : Mathf.Max(0.5f, profile.LifetimeMax + 0.1f);
            main.loop = profile.Loop;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(profile.LifetimeMin, profile.LifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(profile.SpeedMin, profile.SpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(profile.SizeMin, profile.SizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(profile.Gravity);
            main.simulationSpace = profile.WorldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.useUnscaledTime = true;
            main.maxParticles = profile.MaxParticles;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(profile.Loop ? profile.Rate : 0f);
            var bursts = new ParticleSystem.Burst[profile.BurstCounts.Count];
            for (int i = 0; i < bursts.Length; i++)
            {
                var item = profile.BurstCounts[i];
                bursts[i] = new ParticleSystem.Burst(item.time, (short)item.min, (short)item.max);
            }
            emission.SetBursts(bursts);

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = profile.Shape;
            shape.angle = profile.ShapeAngle;
            shape.radius = profile.ShapeRadius;
            shape.radiusThickness = profile.Shape == ParticleSystemShapeType.Sphere ? 1f : 0.08f;
            shape.arc = 360f;

            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(MakeGradient(profile.StartColor, profile.EndColor));

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, MakeSizeCurve(profile.Expands));

            var rotation = particles.rotationOverLifetime;
            rotation.enabled = profile.Rotates;
            rotation.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);

            var noise = particles.noise;
            noise.enabled = profile.Noise > 0f;
            noise.strength = new ParticleSystem.MinMaxCurve(profile.Noise);
            noise.frequency = 0.45f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = profile.Stretched
                ? ParticleSystemRenderMode.Stretch
                : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = profile.Stretched ? 2.8f : 1f;
            renderer.sortingFudge = -0.1f;

            return particles;
        }

        private static GameObject CreateRoot(string name)
        {
            GameObject root = new GameObject(name);
            root.AddComponent<VfxAutoCleanup>();
            return root;
        }

        private static GameObject SavePrefab(GameObject root)
        {
            string path = PrefabFolder + "/" + root.name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material CreateMaterial(string name, Texture2D texture)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                    shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    throw new InvalidOperationException("Could not find a URP or built-in unlit particle shader.");

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D CreateTexture(string name, Color[] pixels)
        {
            string path = TextureFolder + "/" + name + ".png";
            string absolutePath = Path.GetFullPath(path);
            Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color[] MakeGlowTexture()
        {
            const int size = 128;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
                float radiusSquared = dx * dx + dy * dy;
                float alpha = Mathf.Exp(-radiusSquared * 5.2f) * Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((Mathf.Sqrt(radiusSquared) - 0.55f) / 0.45f));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            return pixels;
        }

        private static Color[] MakeRingTexture()
        {
            const int size = 128;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Exp(-Mathf.Pow((radius - 0.66f) / 0.055f, 2f));
                float edgeFade = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((radius - 0.88f) / 0.12f));
                pixels[y * size + x] = new Color(1f, 1f, 1f, ring * edgeFade);
            }
            return pixels;
        }

        private static Color[] MakeStreakTexture()
        {
            const int size = 128;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = Mathf.Abs((x + 0.5f - size * 0.5f) / (size * 0.5f));
                float v = Mathf.Abs((y + 0.5f - size * 0.5f) / (size * 0.5f));
                float core = Mathf.Exp(-u * u * 38f) * Mathf.Pow(Mathf.Clamp01(1f - v), 1.7f);
                float glow = Mathf.Exp(-u * u * 7f) * Mathf.Pow(Mathf.Clamp01(1f - v), 3f) * 0.24f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(core + glow));
            }
            return pixels;
        }

        private static Gradient MakeGradient(Color start, Color end)
        {
            Gradient gradient = new Gradient();
            Color middle = Color.Lerp(start, end, 0.58f);
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(middle, 0.56f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.98f, 0f),
                    new GradientAlphaKey(0.78f, 0.25f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static AnimationCurve MakeSizeCurve(bool expands)
        {
            return expands
                ? new AnimationCurve(new Keyframe(0f, 0.16f), new Keyframe(0.18f, 0.68f), new Keyframe(1f, 1f))
                : new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 0.58f), new Keyframe(1f, 0f));
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(Path.GetFullPath(VfxRoot));
            Directory.CreateDirectory(Path.GetFullPath(PrefabFolder));
            Directory.CreateDirectory(Path.GetFullPath(MaterialFolder));
            Directory.CreateDirectory(Path.GetFullPath(TextureFolder));
            Directory.CreateDirectory(Path.GetFullPath("Assets/Project/VFX/Editor"));
            Directory.CreateDirectory(Path.GetFullPath("Assets/Resources/VFX"));
            AssetDatabase.Refresh();
        }

        private sealed class Profile
        {
            public bool Loop;
            public float Rate;
            public List<(float time, int min, int max)> BurstCounts = new List<(float, int, int)>();
            public float LifetimeMin;
            public float LifetimeMax;
            public float SizeMin;
            public float SizeMax;
            public float SpeedMin;
            public float SpeedMax;
            public float Gravity;
            public float ShapeAngle;
            public float ShapeRadius;
            public float Noise;
            public bool Expands;
            public bool Stretched;
            public bool Rotates;
            public bool WorldSpace;
            public int MaxParticles;
            public Color StartColor;
            public Color EndColor;
            public ParticleSystemShapeType Shape;
        }
    }
}
