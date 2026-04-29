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
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_button == null)
            return;

        // 튜토리얼에서는 무조건 활성화
        if (StageLoadContext.IsTutorial)
        {
            _button.interactable = true;        
            return;
        }

        StageManager stageManager = StageManager.Instance;
        GameFlowManager gameFlowManager = GameFlowManager.Instance;
        bool canStart = (stageManager != null && stageManager.IsWaitingForWaveStart)
            || (gameFlowManager != null && gameFlowManager.IsWaitingForNextWave);
        _button.interactable = canStart;
    }
}
