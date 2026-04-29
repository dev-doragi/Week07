using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class WaveStartButtonController : MonoBehaviour
{
    [SerializeField] private Button _button;

    private void Awake()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        if (_button != null)
            _button.onClick.AddListener(LogWaveStartButtonClicked);
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(LogWaveStartButtonClicked);
    }

    private void Refresh()
    {
        if (_button == null)
            return;

        StageManager stageManager = StageManager.Instance;
        GameFlowManager gameFlowManager = GameFlowManager.Instance;
        bool canStart = (stageManager != null && stageManager.IsWaitingForWaveStart)
            || (gameFlowManager != null && gameFlowManager.IsWaitingForNextWave);
        _button.interactable = canStart;
    }

    private void LogWaveStartButtonClicked()
    {
        GameCsvLogger.Instance.LogEvent(
            GameLogEventType.ButtonClicked,
            actor: gameObject,
            metadata: new System.Collections.Generic.Dictionary<string, object> { { "button", "WaveStart" } });
    }
}
