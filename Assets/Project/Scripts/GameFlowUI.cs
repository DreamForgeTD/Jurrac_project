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
        [SerializeField] private TMP_Text bulletCountText;

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

        private const float ResultAnimationDuration = 0.38f;
        private const float ElementStartScale = 0.72f;

        private GameManager subscribedManager;
        private CannonShooter subscribedShooter;
        private Coroutine resultAnimationRoutine;
        private Image animatedBackground;
        private Color animatedBackgroundBaseColor;
        private Transform animatedLogoTransform;
        private Vector3 animatedLogoBaseScale = Vector3.one;
        private Transform animatedButtonTransform;
        private Vector3 animatedButtonBaseScale = Vector3.one;

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
                    HandleBulletCountChanged(subscribedShooter.RemainingBulletCount,
                        subscribedShooter.StartingBulletCount);
                return;
            }

            UnbindFromCannonShooter();
            subscribedShooter = shooter;
            if (subscribedShooter == null)
                return;

            subscribedShooter.BulletCountChanged += HandleBulletCountChanged;
            HandleBulletCountChanged(subscribedShooter.RemainingBulletCount,
                subscribedShooter.StartingBulletCount);
        }

        private void UnbindFromCannonShooter()
        {
            if (subscribedShooter == null)
                return;

            subscribedShooter.BulletCountChanged -= HandleBulletCountChanged;
            subscribedShooter = null;
        }

        private void HandleBulletCountChanged(int remainingBullets, int totalBullets)
        {
            if (bulletCountText != null)
                bulletCountText.text = remainingBullets.ToString();
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
                Color transparent = animatedBackgroundBaseColor;
                transparent.a = 0f;
                animatedBackground.color = transparent;
            }

            if (logo != null)
            {
                animatedLogoTransform = logo.transform;
                animatedLogoBaseScale = animatedLogoTransform.localScale;
                animatedLogoTransform.localScale = animatedLogoBaseScale * ElementStartScale;
            }
            if (actionButton != null)
            {
                animatedButtonTransform = actionButton.transform;
                animatedButtonBaseScale = animatedButtonTransform.localScale;
                animatedButtonTransform.localScale = animatedButtonBaseScale * ElementStartScale;
            }

            if (animatedBackground == null && animatedLogoTransform == null && animatedButtonTransform == null)
                return;

            resultAnimationRoutine = StartCoroutine(AnimateResultRoutine());
        }

        private IEnumerator AnimateResultRoutine()
        {
            float elapsed = 0f;
            while (elapsed < ResultAnimationDuration)
            {
                float progress = Mathf.Clamp01(elapsed / ResultAnimationDuration);
                float eased = EaseOutBack(progress);

                if (animatedBackground != null)
                {
                    Color transparent = animatedBackgroundBaseColor;
                    transparent.a = 0f;
                    animatedBackground.color = Color.Lerp(transparent, animatedBackgroundBaseColor,
                        Mathf.SmoothStep(0f, 1f, progress));
                }
                if (animatedLogoTransform != null)
                    animatedLogoTransform.localScale = animatedLogoBaseScale *
                        Mathf.LerpUnclamped(ElementStartScale, 1f, eased);
                if (animatedButtonTransform != null)
                    animatedButtonTransform.localScale = animatedButtonBaseScale *
                        Mathf.LerpUnclamped(ElementStartScale, 1f, eased);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            ResetAnimationVisuals();
            resultAnimationRoutine = null;
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
            if (animatedLogoTransform != null)
                animatedLogoTransform.localScale = animatedLogoBaseScale;
            if (animatedButtonTransform != null)
                animatedButtonTransform.localScale = animatedButtonBaseScale;
            animatedBackground = null;
            animatedLogoTransform = null;
            animatedButtonTransform = null;
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
