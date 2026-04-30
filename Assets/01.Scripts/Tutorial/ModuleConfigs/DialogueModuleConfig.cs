using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TutorialInlineDialogueData
{
    [SerializeField] private string _speakerName = "Tutorial NPC";
    [SerializeField] [TextArea(3, 8)] private string _message;
    [SerializeField] private Sprite _portraitSprite;

    public string SpeakerName => string.IsNullOrWhiteSpace(_speakerName) ? "Tutorial NPC" : _speakerName;
    public string Message => _message ?? string.Empty;
    public Sprite PortraitSprite => _portraitSprite;
    public bool HasContent => !string.IsNullOrWhiteSpace(_message);
}

/// <summary>
/// 대사 모듈 설정
/// - 인라인 대사 데이터를 우선 사용
/// - 기존 TutorialDialogueDataSO는 레거시 호환용으로 유지
/// - 애니메이션/UI 동작 및 초상화 오버라이드는 여기서 제어
/// </summary>
[System.Serializable]
public class DialogueModuleConfig
{
    [Header("Dialogue Content")]
    [SerializeField] private TutorialInlineDialogueData _inlineDialogue = new TutorialInlineDialogueData();

    [Header("Legacy Data Source")]
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

    public TutorialInlineDialogueData InlineDialogue => _inlineDialogue;
    public TutorialDialogueDataSO DialogueDataSO => _dialogueDataSO;
    public bool MovePortraitLeft => _movePortraitLeft;
    public float PortraitMoveOffset => _portraitMoveOffset;
    public float PortraitMoveDuration => _portraitMoveDuration;
    public RectTransform PortraitTarget => _portraitTarget;
    public bool MoveDialogUp => _moveDialogUp;
    public float DialogMoveOffset => _dialogMoveOffset;
    public float DialogMoveDuration => _dialogMoveDuration;
    public bool HasDialogue => (_inlineDialogue != null && _inlineDialogue.HasContent) || _dialogueDataSO != null;
}
