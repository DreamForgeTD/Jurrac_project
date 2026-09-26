using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamForgeTD
{
    /// <summary>
    /// Shows a short drag-and-release tutorial only while level_01 is active.
    /// The overlay is created at runtime so it does not need a separate UI prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class LevelOneTutorial : MonoBehaviour
    {
        [SerializeField] private string tutorialLevelId = "level_01";
        [SerializeField, Min(80f)] private float spotlightRadiusAtReferenceWidth = 168f;
        [SerializeField, Min(0.1f)] private float handDragDuration = 0.72f;
        [SerializeField, Min(0.1f)] private float tutorialFadeDuration = 0.45f;

        private const int SpotlightTextureWidth = 540;
        private const float SpotlightIntroDuration = 0.7f;
        private const float SpotlightIntroRadiusMultiplier = 2.25f;
        private const float HandStartOffset = 52f;
        private const float HandDragDistance = 128f;

        private Canvas canvas;
        private RectTransform overlayRect;
        private CanvasGroup overlayGroup;
        private RawImage spotlightImage;
        private TextMeshProUGUI instructionText;
        private Image handImage;
        private Image arrowShaft;
        private Image arrowHead;

        private Texture2D spotlightTexture;
        private Texture2D handTexture;
        private Texture2D whiteTexture;
        private Texture2D arrowTexture;
        private Sprite handSprite;
        private Sprite whiteSprite;
        private Sprite arrowSprite;

        private GameManager subscribedManager;
        private CannonController cannon;
        private CannonShooter shooter;
        private Vector2 lastSpotlightCenter = new Vector2(float.NaN, float.NaN);
        private float lastSpotlightRadius = float.NaN;
        private Vector2Int lastScreenSize;
        private float tutorialStartTime;
        private float fadeElapsed;
        private int startingShotSequence;
        private bool tutorialVisible;

        private void Awake()
        {
            BuildOverlay();
        }

        private void OnEnable()
        {
            BindToGameManager();
        }

        private void Start()
        {
            BindToGameManager();
            if (subscribedManager != null && !string.IsNullOrEmpty(subscribedManager.CurrentLevelId))
            {
                HandleLevelLoaded(subscribedManager.CurrentLevelIndex, subscribedManager.CurrentLevelId,
                    subscribedManager.CurrentLevelName);
            }
        }

        private void OnDestroy()
        {
            UnbindFromGameManager();
            if (overlayRect != null)
                DestroyRuntimeObject(overlayRect.gameObject);
            DestroyRuntimeObject(handSprite);
            DestroyRuntimeObject(whiteSprite);
            DestroyRuntimeObject(arrowSprite);
            DestroyRuntimeObject(spotlightTexture);
            DestroyRuntimeObject(handTexture);
            DestroyRuntimeObject(whiteTexture);
            DestroyRuntimeObject(arrowTexture);
        }

        private void LateUpdate()
        {
            if (!tutorialVisible)
                return;

            ResolveGameplayReferences();

            if (shooter != null && shooter.ShotSequence > startingShotSequence)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                overlayGroup.alpha = 1f - Mathf.Clamp01(fadeElapsed / tutorialFadeDuration);
                if (fadeElapsed >= tutorialFadeDuration)
                    HideTutorial();
                return;
            }

            UpdateOverlayVisuals();
        }

        private void BuildOverlay()
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null)
                return;

            GameObject overlayObject = new GameObject("Level 1 Tutorial Overlay",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            Canvas tutorialCanvas = overlayObject.GetComponent<Canvas>();
            tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            tutorialCanvas.overrideSorting = true;
            tutorialCanvas.sortingOrder = 100;

            CanvasScaler scaler = overlayObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            overlayGroup = overlayObject.GetComponent<CanvasGroup>();
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = false;

            GameObject maskObject = new GameObject("Spotlight Mask", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            maskObject.transform.SetParent(overlayRect, false);
            RectTransform maskRect = maskObject.GetComponent<RectTransform>();
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = Vector2.zero;
            maskRect.offsetMax = Vector2.zero;
            spotlightImage = maskObject.GetComponent<RawImage>();
            spotlightImage.color = Color.white;
            spotlightImage.raycastTarget = false;

            whiteSprite = CreateWhiteSprite(out whiteTexture);
            arrowSprite = CreateArrowSprite(out arrowTexture);
            handSprite = CreateHandSprite(out handTexture);

            arrowShaft = CreateImage("Aim Direction", overlayRect, whiteSprite,
                new Color(0.67f, 0.94f, 1f, 0.95f), false);
            arrowHead = CreateImage("Aim Arrow Head", overlayRect, arrowSprite,
                new Color(0.78f, 0.97f, 1f, 1f), true);
            handImage = CreateImage("Dragging Hand", overlayRect, handSprite, Color.white, true);
            handImage.rectTransform.sizeDelta = new Vector2(92f, 116f);

            instructionText = CreateInstructionText(overlayRect);
            overlayObject.SetActive(false);
        }

        private void BindToGameManager()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
                manager = FindFirstObjectByType<GameManager>();
            if (manager == subscribedManager)
                return;

            UnbindFromGameManager();
            subscribedManager = manager;
            if (subscribedManager != null)
                subscribedManager.LevelLoaded += HandleLevelLoaded;
        }

        private void UnbindFromGameManager()
        {
            if (subscribedManager == null)
                return;

            subscribedManager.LevelLoaded -= HandleLevelLoaded;
            subscribedManager = null;
        }

        private void HandleLevelLoaded(int levelIndex, string levelId, string displayName)
        {
            if (!string.Equals(levelId, tutorialLevelId, StringComparison.OrdinalIgnoreCase))
            {
                HideTutorial();
                return;
            }

            if (overlayRect == null)
                BuildOverlay();
            if (overlayRect == null)
                return;

            cannon = FindFirstObjectByType<CannonController>();
            shooter = FindFirstObjectByType<CannonShooter>();
            startingShotSequence = shooter != null ? shooter.ShotSequence : 0;
            tutorialStartTime = Time.unscaledTime;
            fadeElapsed = 0f;
            tutorialVisible = true;
            overlayGroup.alpha = 1f;
            overlayRect.gameObject.SetActive(true);
            lastSpotlightCenter = new Vector2(float.NaN, float.NaN);
            lastSpotlightRadius = float.NaN;
            lastScreenSize = Vector2Int.zero;
            UpdateOverlayVisuals();
        }

        private void ResolveGameplayReferences()
        {
            if (cannon == null)
                cannon = FindFirstObjectByType<CannonController>();

            if (shooter == null)
            {
                shooter = FindFirstObjectByType<CannonShooter>();
                if (shooter != null)
                    startingShotSequence = shooter.ShotSequence;
            }
        }

        private void UpdateOverlayVisuals()
        {
            Vector2 cannonScreenPosition = GetCannonScreenPosition();
            float finalSpotlightRadius = GetSpotlightRadius();
            float spotlightRadius = GetAnimatedSpotlightRadius(finalSpotlightRadius);
            Vector2 clampedCenter = ClampSpotlightCenter(cannonScreenPosition, finalSpotlightRadius);

            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (screenSize != lastScreenSize ||
                float.IsNaN(lastSpotlightCenter.x) ||
                (clampedCenter - lastSpotlightCenter).sqrMagnitude > 1f ||
                float.IsNaN(lastSpotlightRadius) ||
                Mathf.Abs(spotlightRadius - lastSpotlightRadius) > 0.5f)
            {
                RebuildSpotlightTexture(clampedCenter, spotlightRadius);
                lastSpotlightCenter = clampedCenter;
                lastSpotlightRadius = spotlightRadius;
                lastScreenSize = screenSize;
            }

            UpdateAimArrow();
            UpdateHand(cannonScreenPosition);

            bool isPulling = cannon != null && cannon.IsPulling;
            instructionText.text = isPulling
                ? "HƯỚNG LÊN MỤC TIÊU\nTHẢ TAY ĐỂ BẮN"
                : "KÉO XUỐNG ĐỂ NGẮM LÊN\nTHẢ TAY ĐỂ BẮN";
            handImage.enabled = !isPulling;
            arrowShaft.enabled = cannon != null;
            arrowHead.enabled = cannon != null;
        }

        private Vector2 GetCannonScreenPosition()
        {
            if (cannon == null)
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.22f);

            Camera camera = Camera.main;
            if (camera == null)
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.22f);

            Vector3 screen = camera.WorldToScreenPoint(cannon.transform.position);
            return new Vector2(screen.x, screen.y);
        }

        private Vector2 ClampSpotlightCenter(Vector2 center, float radius)
        {
            float margin = Mathf.Min(radius * 0.72f, Mathf.Min(Screen.width, Screen.height) * 0.16f);
            return new Vector2(
                Mathf.Clamp(center.x, margin, Mathf.Max(margin, Screen.width - margin)),
                Mathf.Clamp(center.y, margin, Mathf.Max(margin, Screen.height - margin)));
        }

        private float GetSpotlightRadius()
        {
            float scaled = spotlightRadiusAtReferenceWidth * Screen.width / 1080f;
            float maxRadius = Mathf.Min(Screen.width, Screen.height) * 0.28f;
            return Mathf.Clamp(scaled, Mathf.Min(72f, maxRadius), maxRadius);
        }

        private float GetAnimatedSpotlightRadius(float finalRadius)
        {
            float progress = Mathf.Clamp01((Time.unscaledTime - tutorialStartTime) / SpotlightIntroDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            return Mathf.Lerp(finalRadius * SpotlightIntroRadiusMultiplier, finalRadius, easedProgress);
        }

        private void RebuildSpotlightTexture(Vector2 center, float radius)
        {
            int width = SpotlightTextureWidth;
            int height = Mathf.Max(1, Mathf.RoundToInt(width * Screen.height / (float)Mathf.Max(1, Screen.width)));
            if (spotlightTexture == null || spotlightTexture.width != width || spotlightTexture.height != height)
            {
                DestroyRuntimeObject(spotlightTexture);
                spotlightTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                {
                    name = "Level 1 Tutorial Spotlight",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                spotlightImage.texture = spotlightTexture;
            }

            float pixelScale = width / (float)Mathf.Max(1, Screen.width);
            float centerX = center.x * pixelScale;
            float centerY = center.y * height / (float)Mathf.Max(1, Screen.height);
            float radiusInTexture = radius * pixelScale;
            float clearRadius = Mathf.Max(1f, radiusInTexture - 5f * pixelScale);
            float rimRadius = radiusInTexture + 3f * pixelScale;
            float glowRadius = radiusInTexture + 24f * pixelScale;
            float clearRadiusSq = clearRadius * clearRadius;
            float rimRadiusSq = rimRadius * rimRadius;
            float glowRadiusSq = glowRadius * glowRadius;

            Color32[] pixels = new Color32[width * height];
            Color32 transparent = new Color32(0, 0, 0, 0);
            Color32 darkMask = new Color32(0, 0, 0, 220);
            Color32 rim = new Color32(190, 244, 255, 245);
            Color32 glow = new Color32(69, 147, 183, 105);

            for (int y = 0; y < height; y++)
            {
                float dy = y - centerY;
                for (int x = 0; x < width; x++)
                {
                    float dx = x - centerX;
                    float distanceSq = dx * dx + dy * dy;
                    Color32 pixel;
                    if (distanceSq <= clearRadiusSq)
                    {
                        pixel = transparent;
                    }
                    else if (distanceSq <= rimRadiusSq)
                    {
                        float distance = Mathf.Sqrt(distanceSq);
                        float t = Mathf.InverseLerp(clearRadius, radiusInTexture + 2f * pixelScale, distance);
                        pixel = new Color32(rim.r, rim.g, rim.b,
                            (byte)Mathf.RoundToInt(Mathf.Lerp(18f, rim.a, t)));
                    }
                    else if (distanceSq <= glowRadiusSq)
                    {
                        float distance = Mathf.Sqrt(distanceSq);
                        float t = Mathf.InverseLerp(rimRadius, glowRadius, distance);
                        pixel = new Color32(
                            (byte)Mathf.Lerp(glow.r, 0f, t),
                            (byte)Mathf.Lerp(glow.g, 0f, t),
                            (byte)Mathf.Lerp(glow.b, 0f, t),
                            (byte)Mathf.Lerp(glow.a, darkMask.a, t));
                    }
                    else
                    {
                        pixel = darkMask;
                    }

                    pixels[y * width + x] = pixel;
                }
            }

            spotlightTexture.SetPixels32(pixels);
            spotlightTexture.Apply(false, false);
        }

        private void UpdateAimArrow()
        {
            if (cannon == null || Camera.main == null)
                return;

            Vector3 direction = cannon.transform.up;
            Vector3 fromWorld = cannon.transform.position + direction * 0.3f;
            Vector3 toWorld = cannon.transform.position + direction * 2.15f;
            Vector3 from3D = Camera.main.WorldToScreenPoint(fromWorld);
            Vector3 to3D = Camera.main.WorldToScreenPoint(toWorld);
            Vector2 from = new Vector2(from3D.x, from3D.y);
            Vector2 to = new Vector2(to3D.x, to3D.y);
            Vector2 directionOnScreen = to - from;
            if (directionOnScreen.sqrMagnitude < 4f)
                directionOnScreen = Vector2.up;
            directionOnScreen.Normalize();

            Vector2 shaftStart = from + directionOnScreen * 28f;
            Vector2 shaftEnd = to - directionOnScreen * 25f;
            Vector2 shaftDelta = shaftEnd - shaftStart;
            float shaftLength = shaftDelta.magnitude;
            float rotation = Mathf.Atan2(directionOnScreen.y, directionOnScreen.x) * Mathf.Rad2Deg - 90f;

            arrowShaft.rectTransform.sizeDelta = new Vector2(8f, Mathf.Max(0f, shaftLength));
            arrowShaft.rectTransform.rotation = Quaternion.Euler(0f, 0f, rotation);
            SetScreenPosition(arrowShaft.rectTransform, (shaftStart + shaftEnd) * 0.5f);

            arrowHead.rectTransform.sizeDelta = new Vector2(40f, 40f);
            arrowHead.rectTransform.rotation = Quaternion.Euler(0f, 0f, rotation);
            SetScreenPosition(arrowHead.rectTransform, to);
        }

        private void UpdateHand(Vector2 cannonScreenPosition)
        {
            if (cannon == null || cannon.IsPulling)
                return;

            float period = handDragDuration + 0.62f;
            float phase = Mathf.Repeat(Time.unscaledTime - tutorialStartTime, period);
            float progress;
            if (phase < handDragDuration)
            {
                progress = Mathf.SmoothStep(0f, 1f, phase / handDragDuration);
            }
            else if (phase < handDragDuration + 0.2f)
            {
                progress = 1f;
            }
            else
            {
                progress = 0f;
            }

            Vector2 handCenter = cannonScreenPosition + Vector2.down *
                (HandStartOffset + HandDragDistance * progress);
            SetScreenPosition(handImage.rectTransform, handCenter);
        }

        private void HideTutorial()
        {
            tutorialVisible = false;
            if (overlayRect != null)
                overlayRect.gameObject.SetActive(false);
        }

        private void SetScreenPosition(RectTransform rectTransform, Vector2 screenPosition)
        {
            if (overlayRect == null || rectTransform == null)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    overlayRect, screenPosition, null, out Vector2 localPosition))
            {
                rectTransform.anchoredPosition = localPosition;
            }
        }

        private Image CreateImage(string objectName, Transform parent, Sprite sprite, Color color, bool preserveAspect)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        private TextMeshProUGUI CreateInstructionText(Transform parent)
        {
            GameObject textObject = new GameObject("Tutorial Instructions",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.07f, 0.81f);
            rectTransform.anchorMax = new Vector2(0.93f, 0.94f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;
            text.text = "KÉO XUỐNG ĐỂ NGẮM LÊN\nTHẢ TAY ĐỂ BẮN";
            text.fontSize = 45f;
            text.fontSizeMin = 26f;
            text.fontSizeMax = 45f;
            text.enableAutoSizing = true;
            text.enableWordWrapping = false;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite CreateWhiteSprite(out Texture2D texture)
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
            {
                name = "Tutorial UI White Pixel",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(new[]
            {
                new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255),
                new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255)
            });
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateArrowSprite(out Texture2D texture)
        {
            const int size = 48;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Tutorial Aim Arrow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float halfWidth = (size - 1 - y) * 0.48f;
                for (int x = 0; x < size; x++)
                {
                    if (Mathf.Abs(x - (size - 1) * 0.5f) <= halfWidth)
                        pixels[y * size + x] = new Color32(255, 255, 255, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateHandSprite(out Texture2D texture)
        {
            const int width = 128;
            const int height = 160;
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "Tutorial Dragging Hand",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inside = IsInsideHand(x, y, 0f);
                    bool outline = !inside && IsInsideHand(x, y, 2.3f);
                    if (outline)
                        pixels[y * width + x] = new Color32(18, 35, 53, 255);
                    else if (inside)
                        pixels[y * width + x] = new Color32(255, 249, 231, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool IsInsideHand(float x, float y, float padding)
        {
            return IsNearSegment(x, y, 64f, 48f, 64f, 143f, 11f + padding) ||
                   IsInsideEllipse(x, y, 64f, 44f, 33f + padding, 31f + padding) ||
                   IsNearSegment(x, y, 39f, 45f, 18f, 30f, 10f + padding) ||
                   IsNearSegment(x, y, 44f, 19f, 78f, 19f, 9f + padding);
        }

        private static bool IsInsideEllipse(float x, float y, float centerX, float centerY, float radiusX, float radiusY)
        {
            float dx = (x - centerX) / radiusX;
            float dy = (y - centerY) / radiusY;
            return dx * dx + dy * dy <= 1f;
        }

        private static bool IsNearSegment(float x, float y, float ax, float ay, float bx, float by, float radius)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float lengthSq = dx * dx + dy * dy;
            float t = lengthSq > 0f ? Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / lengthSq) : 0f;
            float nearestX = ax + dx * t;
            float nearestY = ay + dy * t;
            float offsetX = x - nearestX;
            float offsetY = y - nearestY;
            return offsetX * offsetX + offsetY * offsetY <= radius * radius;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null)
                return;

            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
    }
}
