using System.Collections.Generic;
using UnityEngine;

namespace DreamForgeTD
{
    internal sealed class TrajectoryDotRenderer
    {
        private const int SortingOrder = 1000;

        private readonly Transform owner;
        private readonly Sprite sprite;
        private readonly Material material;
        private readonly int maxDots;
        private readonly List<SpriteRenderer> pool;

        private float spacing;
        private float distanceToNextDot;
        private Vector3 previousPoint;
        private bool hasPreviousPoint;
        private int usedDotCount;

        public TrajectoryDotRenderer(Transform owner, Sprite sprite, Material material, int maxDots)
        {
            this.owner = owner;
            this.sprite = sprite;
            this.material = material;
            this.maxDots = Mathf.Clamp(maxDots, 1, 128);
            pool = new List<SpriteRenderer>(this.maxDots);
            EnsurePool();
        }

        public void Draw(
            IReadOnlyList<TrajectoryPoint> points,
            float dotSpacing,
            Color color,
            float dotSize,
            Vector3 worldOffset)
        {
            spacing = Mathf.Max(0.05f, dotSpacing);
            usedDotCount = 0;
            distanceToNextDot = 0f;
            hasPreviousPoint = false;

            for (int i = 0; i < points.Count && usedDotCount < maxDots; i++)
            {
                TrajectoryPoint point = points[i];
                if (point.StartsNewSegment)
                    BeginSegment();

                AddPoint(point.Position + worldOffset);
            }

            Finish(color, dotSize);
        }

        public void Clear()
        {
            usedDotCount = 0;
            distanceToNextDot = 0f;
            hasPreviousPoint = false;

            for (int i = 0; i < pool.Count; i++)
                SetActive(pool[i], false);
        }

        public void Dispose()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                SpriteRenderer renderer = pool[i];
                if (renderer != null)
                    DestroyObject(renderer.gameObject);
            }

            pool.Clear();
        }

        private void BeginSegment()
        {
            hasPreviousPoint = false;
            distanceToNextDot = 0f;
        }

        private void AddPoint(Vector3 point)
        {
            if (!hasPreviousPoint)
            {
                Place(point);
                previousPoint = point;
                hasPreviousPoint = true;
                distanceToNextDot = spacing;
                return;
            }

            Vector3 segment = point - previousPoint;
            float segmentDistance = segment.magnitude;
            if (segmentDistance <= 0.0001f)
                return;

            float distanceAlongSegment = 0f;
            float remainingDistance = segmentDistance;
            while (usedDotCount < maxDots && remainingDistance + 0.0001f >= distanceToNextDot)
            {
                distanceAlongSegment += distanceToNextDot;
                Place(Vector3.Lerp(previousPoint, point, distanceAlongSegment / segmentDistance));
                remainingDistance = segmentDistance - distanceAlongSegment;
                distanceToNextDot = spacing;
            }

            distanceToNextDot = Mathf.Max(0f, distanceToNextDot - remainingDistance);
            previousPoint = point;
        }

        private void Place(Vector3 position)
        {
            if (usedDotCount >= maxDots)
                return;

            SpriteRenderer renderer = pool[usedDotCount];
            SetActive(renderer, true);
            renderer.transform.position = position;
            usedDotCount++;
        }

        private void Finish(Color color, float dotSize)
        {
            float fadeDenominator = Mathf.Max(1, usedDotCount - 1);
            for (int i = usedDotCount; i < pool.Count; i++)
                SetActive(pool[i], false);

            for (int i = 0; i < usedDotCount; i++)
            {
                float fade = i / fadeDenominator;
                float alpha = color.a * Mathf.Lerp(1f, 0.04f, fade);
                float size = dotSize * Mathf.Lerp(1f, 0.78f, fade);
                SpriteRenderer renderer = pool[i];
                renderer.color = new Color(color.r, color.g, color.b, alpha);
                renderer.transform.localScale = new Vector3(size, size, 1f);
            }
        }

        private void EnsurePool()
        {
            while (pool.Count < maxDots)
            {
                GameObject dotObject = new GameObject("Trajectory Preview Dot " + pool.Count.ToString("00"));
                dotObject.layer = 2;
                dotObject.transform.SetParent(owner, false);
                SpriteRenderer renderer = dotObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = material;
                renderer.sortingOrder = SortingOrder;
                dotObject.SetActive(false);
                pool.Add(renderer);
            }
        }

        private static void SetActive(SpriteRenderer renderer, bool active)
        {
            if (renderer != null && renderer.gameObject.activeSelf != active)
                renderer.gameObject.SetActive(active);
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
