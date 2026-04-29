using System.Collections;
using UnityEngine;

/// <summary>
/// 튜토리얼 적 격파 모듈
/// - 튜토리얼 적 격파 개수 추적
/// - 지정된 수량의 적이 격파될 때까지 대기
/// </summary>
public class EnemyDefeatedModule : ITutorialModule
{
    private EnemyDefeatedModuleConfig _config;
    private int _enemiesDefeated = 0;
    private bool _conditionMet = false;

    public void Initialize(TutorialStep step)
    {
        _config = step.EnemyDefeatedConfig;
        _enemiesDefeated = 0;
        _conditionMet = false;

        // 튜토리얼 적 격파 이벤트 구독
        EventBus.Instance?.Subscribe<TutorialEnemyDefeatedEvent>(OnTutorialEnemyDefeated);
    }

    public IEnumerator Execute()
    {
        // 필요한 수량의 적이 격파될 때까지 대기
        yield return new WaitUntil(() => _conditionMet);
    }

    public void Cleanup()
    {
        // 튜토리얼 적 격파 이벤트 구독 해제
        EventBus.Instance?.Unsubscribe<TutorialEnemyDefeatedEvent>(OnTutorialEnemyDefeated);
    }

    private void OnTutorialEnemyDefeated(TutorialEnemyDefeatedEvent evt)
    {
        if (_config == null) return;

        if (!IsCountable(evt))
        {
            return;
        }

        _enemiesDefeated++;

        // 진행도 브로드캐스트
        EventBus.Instance?.Publish(new TutorialProgressUpdatedEvent
        {
            CurrentProgress = _enemiesDefeated,
            RequiredProgress = _config.RequiredEnemyCount,
            Label = "적 격파"
        });

        // 조건 달성 확인
        if (_enemiesDefeated >= _config.RequiredEnemyCount)
        {
            _conditionMet = true;
        }
    }

    private bool IsCountable(TutorialEnemyDefeatedEvent evt)
    {
        switch (_config.Target)
        {
            case TutorialEnemyDefeatTarget.CoreOnly:
                return evt.Category == UnitCategory.Core;

            case TutorialEnemyDefeatTarget.NonCoreOnly:
                return evt.Category != UnitCategory.Core;

            default:
                return true;
        }
    }

    public bool IsConditionMet()
    {
        return _conditionMet;
    }
}
