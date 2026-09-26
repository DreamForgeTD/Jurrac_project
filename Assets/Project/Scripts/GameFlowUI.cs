using UnityEngine;
using UnityEngine.UI;

namespace DreamForgeTD
{
    /// <summary>
    /// Connects the existing Canvas controls to GameManager's level events.
    /// Assign the existing result panel and buttons in the Inspector; this component
    /// never creates or replaces UI objects.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class GameFlowUI : MonoBehaviour
    {
        [Header("Existing Canvas objects")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button inGameRestartButton;

        [Header("Optional result art and text")]
        [SerializeField] private Image resultPanelImage;
        [SerializeField] private Sprite winPanelSprite;
        [SerializeField] private Sprite losePanelSprite;
        [SerializeField] private Text resultTitle;
        [SerializeField] private Text resultMessage;

        private GameManager subscribedManager;

        private void OnEnable()
        {
            ResolveReferences();
            if (!Application.isPlaying)
                return;

            RegisterButtons();
            BindToGameManager();
            ShowResult(false);
        }

        private void Start()
        {
            BindToGameManager();

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
            UnregisterButtons();
            UnbindFromGameManager();
        }

        private void ResolveReferences()
        {
            if (resultPanel == null)
                resultPanel = FindChild("BG_den");
            if (restartButton == null)
                restartButton = FindButton("Btn_retry");
            if (nextButton == null)
                nextButton = FindButton("Btn_Next");
            if (inGameRestartButton == null)
                inGameRestartButton = FindButton("Btn_replay");
            if (resultPanelImage == null && resultPanel != null)
                resultPanelImage = resultPanel.GetComponent<Image>();
        }

        private void RegisterButtons()
        {
            if (restartButton != null)
                restartButton.onClick.AddListener(RestartLevel);
            if (nextButton != null && nextButton != restartButton)
                nextButton.onClick.AddListener(NextLevel);
            if (inGameRestartButton != null && inGameRestartButton != restartButton)
                inGameRestartButton.onClick.AddListener(RestartLevel);
        }

        private void UnregisterButtons()
        {
            if (restartButton != null)
                restartButton.onClick.RemoveListener(RestartLevel);
            if (nextButton != null && nextButton != restartButton)
                nextButton.onClick.RemoveListener(NextLevel);
            if (inGameRestartButton != null && inGameRestartButton != restartButton)
                inGameRestartButton.onClick.RemoveListener(RestartLevel);
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
            if (restartButton != null)
                restartButton.gameObject.SetActive(false);
            if (nextButton != null)
                nextButton.gameObject.SetActive(false);
            if (inGameRestartButton != null)
                inGameRestartButton.gameObject.SetActive(true);

            ShowResult(false);
        }

        private void HandleLevelWon()
        {
            if (resultTitle != null)
                resultTitle.text = "CHIẾN THẮNG";
            if (resultMessage != null && subscribedManager != null)
                resultMessage.text = $"{subscribedManager.CurrentLevelName}\nBạn đã hoàn thành mục tiêu.";
            if (nextButton != null)
                nextButton.gameObject.SetActive(true);
            if (restartButton != null)
                restartButton.gameObject.SetActive(false);
            if (inGameRestartButton != null)
                inGameRestartButton.gameObject.SetActive(false);

            ApplyResultSprite(winPanelSprite);
            ShowResult(true);
        }

        private void HandleLevelLost()
        {
            if (resultTitle != null)
                resultTitle.text = "THẤT BẠI";
            if (resultMessage != null && subscribedManager != null)
                resultMessage.text = $"{subscribedManager.CurrentLevelName}\nChơi lại và thử lần nữa.";
            if (nextButton != null)
                nextButton.gameObject.SetActive(false);
            if (restartButton != null)
                restartButton.gameObject.SetActive(true);
            if (inGameRestartButton != null)
                inGameRestartButton.gameObject.SetActive(false);

            ApplyResultSprite(losePanelSprite);
            ShowResult(true);
        }

        private void HandleAllLevelsCompleted()
        {
            if (resultTitle != null)
                resultTitle.text = "HOÀN THÀNH TẤT CẢ";
            if (resultMessage != null)
                resultMessage.text = "Bạn đã vượt qua mọi màn.";
            if (nextButton != null)
                nextButton.gameObject.SetActive(false);
            if (restartButton != null)
                restartButton.gameObject.SetActive(false);
            if (inGameRestartButton != null)
                inGameRestartButton.gameObject.SetActive(false);

            ApplyResultSprite(winPanelSprite);
            ShowResult(true);
        }

        private void ShowResult(bool visible)
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(visible);
            }

            if (inGameRestartButton != null)
                inGameRestartButton.gameObject.SetActive(!visible);
        }

        private void ApplyResultSprite(Sprite sprite)
        {
            if (resultPanelImage != null && sprite != null)
                resultPanelImage.sprite = sprite;
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

        private GameObject FindChild(string childName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == childName)
                    return children[i].gameObject;
            }

            return null;
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
