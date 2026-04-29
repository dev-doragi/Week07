using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 튜토리얼 상호작용 완료 모듈
/// - 지정된 InteractionId 이벤트를 구독
/// - 조건 충족 시 다음 스텝 진행
/// </summary>
public class InteractionModule : ITutorialModule
{
    private InteractionModuleConfig _config;
    private int _triggeredCount = 0;
    private bool _conditionMet = false;

    public void Initialize(TutorialStep step)
    {
        _config = step.InteractionConfig;
        _triggeredCount = 0;
        _conditionMet = false;

        EventBus.Instance?.Subscribe<TutorialInteractionTriggeredEvent>(OnTutorialInteractionTriggered);
    }

    public IEnumerator Execute()
    {
        yield return new WaitUntil(() => _conditionMet);
    }

    public void Cleanup()
    {
        EventBus.Instance?.Unsubscribe<TutorialInteractionTriggeredEvent>(OnTutorialInteractionTriggered);
    }

    private void OnTutorialInteractionTriggered(TutorialInteractionTriggeredEvent evt)
    {
        if (_config == null)
            return;

        if (!string.IsNullOrWhiteSpace(_config.InteractionId) && !string.Equals(_config.InteractionId, evt.InteractionId))
            return;

        _triggeredCount++;

        EventBus.Instance?.Publish(new TutorialProgressUpdatedEvent
        {
            CurrentProgress = _triggeredCount,
            RequiredProgress = _config.RequiredCount,
            Label = _config.ProgressLabel
        });

        if (_triggeredCount >= _config.RequiredCount)
        {
            _conditionMet = true;
        }
    }
}