using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamForgeTD.PhysicsPuzzle
{
    public sealed class GameplayUI : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [SerializeField] private TMP_Text shotsLabel;
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject failPanel;
        [SerializeField] private Button retryButton;

        private void Awake()
        {
            if (level != null && shotsLabel != null && winPanel != null && failPanel != null && retryButton != null) return;
            Debug.LogError($"{name}: Assign Level, Shots Label, Win Panel, Fail Panel and Retry Button.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            level.RemainingShotsChanged += UpdateShots;
            level.StateChanged += UpdateState;
            retryButton.onClick.AddListener(Retry);
            UpdateShots(level.RemainingShots);
            UpdateState(level.State);
        }

        private void OnDisable()
        {
            if (level == null || retryButton == null) return;
            level.RemainingShotsChanged -= UpdateShots;
            level.StateChanged -= UpdateState;
            retryButton.onClick.RemoveListener(Retry);
        }

        private void Retry() => level.Restart();
        private void UpdateShots(int count) => shotsLabel.SetText("Shots: {0}", (float)count);
        private void UpdateState(LevelState state)
        {
            winPanel.SetActive(state == LevelState.Win);
            failPanel.SetActive(state == LevelState.Fail);
        }
    }
}
