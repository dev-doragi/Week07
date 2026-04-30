using System.Collections;
using UnityEngine;

/// <summary>
/// 튜토리얼 대사 모듈
/// - TutorialDialogueDataSO에서 스텝 인덱스로 대사 데이터 조회
/// - SO의 기본 스피커명/초상화 사용 (효율성)
/// - 초상화 이동 애니메이션 및 UI 동작 처리
/// </summary>
public class DialogueModule : ITutorialModule
{
    private DialogueModuleConfig _config;
    private TutorialDialoguePresenter _presenter;
    private int _currentStepIndex;

    public DialogueModule(TutorialDialoguePresenter presenter)
    {
        _presenter = presenter;
    }

    public void Initialize(TutorialStep step)
    {
        _config = step.DialogueConfig;
    }

    public void SetStepIndex(int stepIndex)
    {
        _currentStepIndex = stepIndex;
    }

    public IEnumerator Execute()
    {
        if (_presenter == null || _config == null)
        {
            Debug.LogError("DialogueModule: Presenter 또는 Config가 할당되지 않았습니다.");
            yield break;
        }

        if (!TryResolveDialogue(out string speakerName, out string fullDialogue, out Sprite portraitSprite))
        {
            Debug.LogWarning($"DialogueModule: 스텝 {_currentStepIndex}에 대한 대사 데이터를 찾을 수 없습니다.");
            yield break;
        }

        bool dialogMovesUp = _config.EnableDialogMove && _config.DialogMoveOffset > 0f;
        if (_config.EnablePortraitMove)
        {
            float portraitYOffset = dialogMovesUp ? Mathf.Max(0f, _config.DialogMoveOffset - 100f) : 0f;
            _presenter.StartPortraitMove(
                _config.PortraitTarget,
                _config.PortraitMoveOffset,
                _config.PortraitMoveDuration,
                portraitYOffset
            );
        }
        else
        {
            _presenter.ResetPortraitPosition(_config.PortraitMoveDuration);
        }

        if (_config.EnableDialogMove)
        {
            _presenter.StartDialogPanelMove(
                _config.DialogMoveOffset,
                _config.DialogMoveDuration
            );
        }
        else
        {
            _presenter.ResetDialogPanelPosition(_config.DialogMoveDuration);
        }

        yield return _presenter.ShowDialogueWithTyping(speakerName, fullDialogue, portraitSprite);
    }

    public void Cleanup()
    {

    }

    private bool TryResolveDialogue(out string speakerName, out string fullDialogue, out Sprite portraitSprite)
    {
        var inlineDialogue = _config.InlineDialogue;
        if (inlineDialogue != null && inlineDialogue.HasContent)
        {
            speakerName = inlineDialogue.SpeakerName;
            fullDialogue = inlineDialogue.Message;
            portraitSprite = inlineDialogue.PortraitSprite;
            return true;
        }

        var dialogueDataSO = _config.DialogueDataSO;
        if (dialogueDataSO != null && dialogueDataSO.TryGetDialogue(_currentStepIndex, out var dialogueData))
        {
            speakerName = dialogueDataSO.GetSpeakerName();
            fullDialogue = string.Join("\n", dialogueData.Lines);
            portraitSprite = dialogueDataSO.GetPortraitSprite(_currentStepIndex);
            return true;
        }

        speakerName = string.Empty;
        fullDialogue = string.Empty;
        portraitSprite = null;
        return false;
    }
}