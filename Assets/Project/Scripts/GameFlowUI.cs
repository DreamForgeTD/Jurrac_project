using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DreamForgeTD
{
    /// <summary>
    /// Connects the existing Canvas controls to GameManager's level events.
    /// Assign the Win and Lose backgrounds, logos, and buttons in the Inspector;
    /// this component never creates or replaces UI objects.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class GameFlowUI : MonoBehaviour
    {
        [SerializeField, HideInInspector] private GameManager gameManager;

        [Header("HUD")]
        [Tooltip("Kéo TextMeshPro của bộ đếm đạn vào đây.")]
        [FormerlySerializedAs("bulletCountText")]
        [SerializeField] private TMP_Text txtSoDan;

        [Tooltip("Kéo TextMeshPro hiển thị số màn hiện tại vào đây.")]
        [SerializeField] private TMP_Text txtSoMan;

        [FormerlySerializedAs("inGameRestartButton")]
        [Tooltip("Nút chơi lại trong lúc đang chơi.")]
        [SerializeField] private Button replayButton;

        [Header("WIN PANEL")]
        [Tooltip("Kéo Image nền của panel Win vào đây.")]
        [SerializeField] private Image winBackground;
        [SerializeField] private Image winLogo;
        [FormerlySerializedAs("nextButton")]
        [SerializeField] private Button winButton;
        [Tooltip("Để trống nếu chưa có hiệu ứng thắng.")]
        [SerializeField] private ParticleSystem winParticleEffect;

        [Header("LOSE PANEL")]
        [Tooltip("Kéo Image nền của panel Lose vào đây.")]
        [SerializeField] private Image loseBackground;
        [SerializeField] private Image loseLogo;
        [FormerlySerializedAs("restartButton")]
        [Tooltip("Nút này chơi lại màn hiện tại.")]
        [SerializeField] private Button loseButton;

        // Retain old scene references while the existing Canvas is migrated to separate panels.
        [FormerlySerializedAs("resultPanel")]
        [SerializeField, HideInInspector] private GameObject legacyResultPanel;
        [FormerlySerializedAs("resultPanelImage")]
        [SerializeField, HideInInspector] private Image legacyResultPanelImage;

        private const float ResultAnimationDuration = 0.28f;
        private const float LogoAnimationDelay = ResultAnimationDuration;
        private const float ButtonAnimationDelay = ResultAnimationDuration * 2f;
        private const float BackgroundStartScale = 0.97f;
        private const float ElementStartScale = 0.90f;

        private GameManager subscribedManager;
        private CannonShooter subscribedShooter;
        private int lastDisplayedBulletCount = -1;
        private Coroutine resultAnimationRoutine;
        private Image animatedBackground;
        private Color animatedBackgroundBaseColor;
        private Transform animatedBackgroundTransform;
        private Vector3 animatedBackgroundBaseScale = Vector3.one;
        private Transform animatedLogoTransform;
        private Vector3 animatedLogoBaseScale = Vector3.one;
        private CanvasGroup animatedLogoCanvasGroup;
        private float animatedLogoBaseAlpha = 1f;
        private Transform animatedButtonTransform;
        private Vector3 animatedButtonBaseScale = Vector3.one;
        private CanvasGroup animatedButtonCanvasGroup;
        private float animatedButtonBaseAlpha = 1f;
        private bool animatedButtonBaseInteractable = true;
        private bool animatedButtonBaseBlocksRaycasts = true;

        private void OnEnable()
        {
            ResolveReferences();
            if (!Application.isPlaying)
                return;

            PrepareWinParticleEffect();
            RegisterButtons();
            BindToGameManager();
            ShowResultPanels(false, false);
        }

        private void Start()
        {
            BindToGameManager();
            BindToCannonShooter();

            if (subscribedManager == null)
                return;

            CapNhatSoMan(subscribedManager.CurrentLevelIndex);

            if (subscribedManager.IsLevelWon)
                HandleLevelWon();
            else if (subscribedManager.IsLevelLost)
                HandleLevelLost();
            else
                HandleLevelLoaded(subscribedManager.CurrentLevelIndex, subscribedManager.CurrentLevelId,
                    subscribedManager.CurrentLevelName);
        }

        private void OnDisable()
        {
            StopResultAnimationAndRestore();
            UnregisterButtons();
            UnbindFromCannonShooter();
            UnbindFromGameManager();
        }

        private void Update()
        {
            if (subscribedShooter != null)
                CapNhatSoDan(subscribedShooter.RemainingBulletCount);
        }

        private void ResolveReferences()
        {
            if (!Application.isPlaying)
                return;

            if (loseButton == null)
                loseButton = FindButton("Btn_retry");
            if (winButton == null)
                winButton = FindButton("Btn_Next");
            if (replayButton == null)
                replayButton = FindButton("Btn_replay");
        }

        private Image GetWinBackground()
        {
            if (winBackground != null)
                return winBackground;
            if (legacyResultPanelImage != null)
                return legacyResultPanelImage;
            return legacyResultPanel != null ? legacyResultPanel.GetComponent<Image>() : null;
        }

        private Image GetLoseBackground()
        {
            if (loseBackground != null)
                return loseBackground;
            if (legacyResultPanelImage != null)
                return legacyResultPanelImage;
            return legacyResultPanel != null ? legacyResultPanel.GetComponent<Image>() : null;
        }

        private void RegisterButtons()
        {
            if (loseButton != null)
                loseButton.onClick.AddListener(RestartLevel);
            if (winButton != null && winButton != loseButton)
                winButton.onClick.AddListener(NextLevel);
            if (replayButton != null && replayButton != loseButton)
                replayButton.onClick.AddListener(RestartLevel);
        }

        private void UnregisterButtons()
        {
            if (loseButton != null)
                loseButton.onClick.RemoveListener(RestartLevel);
            if (winButton != null && winButton != loseButton)
                winButton.onClick.RemoveListener(NextLevel);
            if (replayButton != null && replayButton != loseButton)
                replayButton.onClick.RemoveListener(RestartLevel);
        }

        private void BindToGameManager()
        {
            GameManager manager = gameManager != null ? gameManager : GameManager.Instance;
            if (manager == null)
                manager = FindFirstObjectByType<GameManager>();

            if (manager == subscribedManager)
                return;

            UnbindFromGameManager();
            subscribedManager = manager;
            if (subscribedManager == null)
            {
                Debug.LogWarning("[GameFlowUI] No GameManager was found in the gameplay scene.", this);
                return;
            }

            subscribedManager.UseManualUIFlow();
            subscribedManager.LevelLoaded += HandleLevelLoaded;
            subscribedManager.LevelWon += HandleLevelWon;
            subscribedManager.LevelLost += HandleLevelLost;
            subscribedManager.AllLevelsCompleted += HandleAllLevelsCompleted;
        }

        private void UnbindFromGameManager()
        {
            if (subscribedManager == null)
                return;

            subscribedManager.LevelLoaded -= HandleLevelLoaded;
            subscribedManager.LevelWon -= HandleLevelWon;
            subscribedManager.LevelLost -= HandleLevelLost;
            subscribedManager.AllLevelsCompleted -= HandleAllLevelsCompleted;
            subscribedManager = null;
        }

        private void HandleLevelLoaded(int levelIndex, string levelId, string displayName)
        {
            CapNhatSoMan(levelIndex);
            BindToCannonShooter();
            StopWinParticleEffect();
            if (loseButton != null)
                loseButton.gameObject.SetActive(false);
            if (winButton != null)
                winButton.gameObject.SetActive(false);
            if (replayButton != null)
                replayButton.gameObject.SetActive(true);

            ShowResultPanels(false, false);
        }

        private void BindToCannonShooter()
        {
            CannonShooter shooter = FindFirstObjectByType<CannonShooter>();
            if (shooter == subscribedShooter)
            {
                if (subscribedShooter != null)
                    CapNhatSoDan(subscribedShooter.RemainingBulletCount);
                return;
            }

            UnbindFromCannonShooter();
            subscribedShooter = shooter;
            lastDisplayedBulletCount = -1;
            if (subscribedShooter == null)
                return;

            CapNhatSoDan(subscribedShooter.RemainingBulletCount);
        }

        private void UnbindFromCannonShooter()
        {
            if (subscribedShooter == null)
                return;

            subscribedShooter = null;
            lastDisplayedBulletCount = -1;
        }

        private void CapNhatSoDan(int soDanCon)
        {
            if (txtSoDan == null || lastDisplayedBulletCount == soDanCon)
                return;

            txtSoDan.text = $"Đạn còn: {soDanCon}";
            lastDisplayedBulletCount = soDanCon;
        }

        private void CapNhatSoMan(int chiSoMan)
        {
            if (txtSoMan == null)
                return;

            txtSoMan.text = chiSoMan >= 0 ? (chiSoMan + 1).ToString() : string.Empty;
        }

        private void HandleLevelWon()
        {
            if (winButton != null)
                winButton.gameObject.SetActive(true);
            if (loseButton != null)
                loseButton.gameObject.SetActive(false);
            if (replayButton != null)
                replayButton.gameObject.SetActive(false);

            ShowResultPanels(true, false);
            AnimateResult(GetWinBackground(), winLogo, winButton);
            if (winParticleEffect != null)
                winParticleEffect.Play(true);
        }

        private void HandleLevelLost()
        {
            if (winButton != null)
                winButton.gameObject.SetActive(false);
            if (loseButton != null)
                loseButton.gameObject.SetActive(true);
            if (replayButton != null)
                replayButton.gameObject.SetActive(false);

            ShowResultPanels(false, true);
            AnimateResult(GetLoseBackground(), loseLogo, loseButton);
        }

        private void HandleAllLevelsCompleted()
        {
            if (winButton != null)
                winButton.gameObject.SetActive(false);
            if (loseButton != null)
                loseButton.gameObject.SetActive(false);
            if (replayButton != null)
                replayButton.gameObject.SetActive(false);

            ShowResultPanels(true, false);
            AnimateResult(GetWinBackground(), winLogo, null);
        }

        private void ShowResultPanels(bool showWin, bool showLose)
        {
            bool visible = showWin || showLose;
            if (!visible)
            {
                StopResultAnimationAndRestore();
            }

            Image resolvedWinBackground = GetWinBackground();
            Image resolvedLoseBackground = GetLoseBackground();
            if (resolvedWinBackground != null && resolvedWinBackground == resolvedLoseBackground)
            {
                resolvedWinBackground.gameObject.SetActive(visible);
            }
            else
            {
                if (resolvedWinBackground != null)
                    resolvedWinBackground.gameObject.SetActive(showWin);
                if (resolvedLoseBackground != null)
                    resolvedLoseBackground.gameObject.SetActive(showLose);
            }

            if (winLogo != null)
                winLogo.gameObject.SetActive(showWin);
            if (loseLogo != null)
                loseLogo.gameObject.SetActive(showLose);

            if (replayButton != null)
                replayButton.gameObject.SetActive(!visible);
        }

        private void AnimateResult(Image background, Image logo, Button actionButton)
        {
            if (!Application.isPlaying)
            {
                ResetAnimationVisuals();
                return;
            }

            StopResultAnimationAndRestore();

            animatedBackground = background;
            if (animatedBackground != null)
            {
                animatedBackgroundBaseColor = animatedBackground.color;
                animatedBackgroundTransform = animatedBackground.transform;
                animatedBackgroundBaseScale = animatedBackgroundTransform.localScale;
                Color transparent = animatedBackgroundBaseColor;
                transparent.a = 0f;
                animatedBackground.color = transparent;
                animatedBackgroundTransform.localScale = animatedBackgroundBaseScale * BackgroundStartScale;
            }

            if (logo != null)
            {
                animatedLogoTransform = logo.transform;
                animatedLogoBaseScale = animatedLogoTransform.localScale;
                animatedLogoTransform.localScale = animatedLogoBaseScale * ElementStartScale;
                animatedLogoCanvasGroup = GetOrAddCanvasGroup(logo.gameObject);
                animatedLogoBaseAlpha = animatedLogoCanvasGroup.alpha;
                animatedLogoCanvasGroup.alpha = 0f;
            }
            if (actionButton != null)
            {
                animatedButtonTransform = actionButton.transform;
                animatedButtonBaseScale = animatedButtonTransform.localScale;
                animatedButtonTransform.localScale = animatedButtonBaseScale * ElementStartScale;
                animatedButtonCanvasGroup = GetOrAddCanvasGroup(actionButton.gameObject);
                animatedButtonBaseAlpha = animatedButtonCanvasGroup.alpha;
                animatedButtonBaseInteractable = animatedButtonCanvasGroup.interactable;
                animatedButtonBaseBlocksRaycasts = animatedButtonCanvasGroup.blocksRaycasts;
                animatedButtonCanvasGroup.alpha = 0f;
                animatedButtonCanvasGroup.interactable = false;
                animatedButtonCanvasGroup.blocksRaycasts = false;
            }

            if (animatedBackground == null && animatedLogoCanvasGroup == null && animatedButtonCanvasGroup == null)
                return;

            resultAnimationRoutine = StartCoroutine(AnimateResultRoutine());
        }

        private IEnumerator AnimateResultRoutine()
        {
            float elapsed = 0f;
            float totalDuration = ResultAnimationDuration;
            if (animatedLogoCanvasGroup != null)
                totalDuration = Mathf.Max(totalDuration, LogoAnimationDelay + ResultAnimationDuration);
            if (animatedButtonCanvasGroup != null)
                totalDuration = Mathf.Max(totalDuration, ButtonAnimationDelay + ResultAnimationDuration);

            while (elapsed < totalDuration)
            {
                float backgroundProgress = Mathf.Clamp01(elapsed / ResultAnimationDuration);
                float backgroundEased = EaseOutBack(backgroundProgress);

                if (animatedBackground != null)
                {
                    Color transparent = animatedBackgroundBaseColor;
                    transparent.a = 0f;
                    animatedBackground.color = Color.Lerp(transparent, animatedBackgroundBaseColor,
                        Mathf.SmoothStep(0f, 1f, backgroundProgress));
                }
                if (animatedBackgroundTransform != null)
                    animatedBackgroundTransform.localScale = animatedBackgroundBaseScale *
                        Mathf.LerpUnclamped(BackgroundStartScale, 1f, backgroundEased);

                AnimateResultElement(
                    animatedLogoTransform,
                    animatedLogoBaseScale,
                    animatedLogoCanvasGroup,
                    animatedLogoBaseAlpha,
                    elapsed - LogoAnimationDelay);
                AnimateResultElement(
                    animatedButtonTransform,
                    animatedButtonBaseScale,
                    animatedButtonCanvasGroup,
                    animatedButtonBaseAlpha,
                    elapsed - ButtonAnimationDelay);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            ResetAnimationVisuals();
            resultAnimationRoutine = null;
        }

        private static void AnimateResultElement(
            Transform elementTransform,
            Vector3 baseScale,
            CanvasGroup canvasGroup,
            float baseAlpha,
            float elapsed)
        {
            if (canvasGroup == null || elapsed < 0f)
                return;

            float progress = Mathf.Clamp01(elapsed / ResultAnimationDuration);
            canvasGroup.alpha = Mathf.Lerp(0f, baseAlpha, Mathf.SmoothStep(0f, 1f, progress));
            if (elementTransform != null)
            {
                elementTransform.localScale = baseScale *
                    Mathf.LerpUnclamped(ElementStartScale, 1f, EaseOutBack(progress));
            }
        }

        private void StopResultAnimationAndRestore()
        {
            if (resultAnimationRoutine != null)
            {
                StopCoroutine(resultAnimationRoutine);
                resultAnimationRoutine = null;
            }

            ResetAnimationVisuals();
        }

        private void ResetAnimationVisuals()
        {
            if (animatedBackground != null)
                animatedBackground.color = animatedBackgroundBaseColor;
            if (animatedBackgroundTransform != null)
                animatedBackgroundTransform.localScale = animatedBackgroundBaseScale;
            if (animatedLogoCanvasGroup != null)
                animatedLogoCanvasGroup.alpha = animatedLogoBaseAlpha;
            if (animatedLogoTransform != null)
                animatedLogoTransform.localScale = animatedLogoBaseScale;
            if (animatedButtonCanvasGroup != null)
            {
                animatedButtonCanvasGroup.alpha = animatedButtonBaseAlpha;
                animatedButtonCanvasGroup.interactable = animatedButtonBaseInteractable;
                animatedButtonCanvasGroup.blocksRaycasts = animatedButtonBaseBlocksRaycasts;
            }
            if (animatedButtonTransform != null)
                animatedButtonTransform.localScale = animatedButtonBaseScale;
            animatedBackground = null;
            animatedBackgroundTransform = null;
            animatedLogoTransform = null;
            animatedLogoCanvasGroup = null;
            animatedButtonTransform = null;
            animatedButtonCanvasGroup = null;
        }

        private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
        {
            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            return canvasGroup != null ? canvasGroup : target.AddComponent<CanvasGroup>();
        }

        private static float EaseOutBack(float progress)
        {
            const float overshoot = 1.70158f;
            float shifted = progress - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
        }

        private void PrepareWinParticleEffect()
        {
            if (winParticleEffect == null)
                return;

            ParticleSystem.MainModule main = winParticleEffect.main;
            main.playOnAwake = false;
            StopWinParticleEffect();
        }

        private void StopWinParticleEffect()
        {
            if (winParticleEffect != null)
                winParticleEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void RestartLevel()
        {
            GameAudio.PlayButtonClick();
            if (subscribedManager != null)
                subscribedManager.RestartLevel();
        }

        private void NextLevel()
        {
            GameAudio.PlayButtonClick();
            if (subscribedManager != null)
                subscribedManager.NextLevel();
        }

        private Button FindButton(string objectName)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == objectName)
                    return buttons[i];
            }

            return null;
        }
    }
}
