using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 선택 씬에서 버튼(또는 버튼 프리팹)에 붙여서 해당 스테이지 인덱스의 잠금/해금을 자동으로 처리합니다.
/// - StageIndex: 0부터 시작
/// - LockedOverlay: 잠금 표시(예: 어두운 패널, 자물쇠 아이콘). 해금 시 비활성화.
/// </summary>
public class StageButtonUnlocker : MonoBehaviour
{
    [Tooltip("해당 버튼이 가리키는 스테이지 인덱스 (0부터)")]
    public int StageIndex = 0;

    [Tooltip("잠김 상태를 표시할 오버레이(해금 시 비활성화)")]
    public GameObject LockedOverlay;

    [Tooltip("버튼(선택 시 호출되는) - optional")]
    public Button StageButton;

    private void Start()
    {
        ApplyLockState();

        // 진행도가 바뀌면 UI 갱신
        EventBus.Instance?.Subscribe<StageProgressUpdatedEvent>(OnProgressUpdated);
    }

    private void OnDestroy()
    {
        EventBus.Instance?.Unsubscribe<StageProgressUpdatedEvent>(OnProgressUpdated);
    }

    private void OnProgressUpdated(StageProgressUpdatedEvent evt)
    {
        ApplyLockState();
    }

    private void ApplyLockState()
    {
        bool unlocked = ProgressManager.Instance != null && ProgressManager.Instance.IsStageUnlocked(StageIndex);

        if (LockedOverlay != null) LockedOverlay.SetActive(!unlocked);

        if (StageButton != null)
        {
            StageButton.interactable = unlocked;
        }
    }
}