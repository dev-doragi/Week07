using UnityEngine;

/// <summary>
/// 대사 모듈 설정
/// - TutorialDialogueDataSO에서 텍스트 데이터를 로드
/// - 애니메이션/UI 동작 및 초상화 오버라이드는 여기서 제어
/// </summary>
[System.Serializable]
public class DialogueModuleConfig
{
    [Header("Data Source")]
    [SerializeField] private TutorialDialogueDataSO _dialogueDataSO;

    [Header("Portrait Animation")]
    [SerializeField] private bool _movePortraitLeft = false;
    [SerializeField] private float _portraitMoveOffset = 150f;
    [SerializeField] private float _portraitMoveDuration = 0.5f;
    [SerializeField] private RectTransform _portraitTarget;

    [Header("UI Motion")]
    [SerializeField] private bool _moveDialogUp = false;
    [SerializeField] private float _dialogMoveOffset = 300f;
    [SerializeField] private float _dialogMoveDuration = 0.5f;

    public TutorialDialogueDataSO DialogueDataSO => _dialogueDataSO;
    public bool MovePortraitLeft => _movePortraitLeft;
    public float PortraitMoveOffset => _portraitMoveOffset;
    public float PortraitMoveDuration => _portraitMoveDuration;
    public RectTransform PortraitTarget => _portraitTarget;
    public bool MoveDialogUp => _moveDialogUp;
    public float DialogMoveOffset => _dialogMoveOffset;
    public float DialogMoveDuration => _dialogMoveDuration;
}
