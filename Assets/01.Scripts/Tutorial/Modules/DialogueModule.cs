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
        if (_presenter == null || _config == null || _config.DialogueDataSO == null)
        {
            Debug.LogError("DialogueModule: Presenter, Config, 또는 DialogueDataSO가 할당되지 않았습니다.");
            yield break;
        }

        var dialogueDataSO = _config.DialogueDataSO;

        // SO에서 현재 스텝의 대사 데이터 조회
        if (!dialogueDataSO.TryGetDialogue(_currentStepIndex, out var dialogueData))
        {
            Debug.LogWarning($"DialogueModule: 스텝 {_currentStepIndex}에 대한 대사 데이터를 찾을 수 없습니다.");
            yield break;
        }

        // SO의 기본 스피커명 + 대사 라인 (개별 할당 불필요)
        string speakerName = dialogueDataSO.GetSpeakerName();
        string fullDialogue = string.Join("\n", dialogueData.Lines);
        Sprite portraitSprite = dialogueDataSO.GetPortraitSprite(_currentStepIndex);

        // 대사 표시 및 타이핑 애니메이션
        yield return _presenter.ShowDialogueWithTyping(speakerName, fullDialogue, portraitSprite);

        // 초상화 이동 애니메이션
        if (_config.MovePortraitLeft && _config.PortraitTarget != null)
        {
            yield return _presenter.MovePortrait(
                _config.PortraitTarget,
                _config.PortraitMoveOffset,
                _config.PortraitMoveDuration
            );
        }

        // 대사판 이동 애니메이션
        if (_config.MoveDialogUp)
        {
            yield return _presenter.MoveDialogPanel(
                _config.DialogMoveOffset,
                _config.DialogMoveDuration
            );
        }
    }

    public void Cleanup()
    {
        if (_presenter != null)
        {
            _presenter.HideDialogue();
        }
    }
}