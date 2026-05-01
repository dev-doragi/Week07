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

        // Early Defeat 대응: 이미 격파된 적이 있으면 카운트 반영
        int pregrindCount = TutorialManager.GetAndResetPregrindsEnemyCount();
        _enemiesDefeated += pregrindCount;

        // 초기 진행도 브로드캐스트
        if (_config != null)
        {
            EventBus.Instance?.Publish(new TutorialProgressUpdatedEvent
            {
                CurrentProgress = _enemiesDefeated,
                RequiredProgress = _config.RequiredEnemyCount,
                Label = GetLabelForConfig(_config)
            });
        }

        // 이미 조건을 충족했으면 즉시 완료 표시
        if (_config != null && _enemiesDefeated >= _config.RequiredEnemyCount)
        {
            _conditionMet = true;
            return;
        }

        // 튜토리얼 적 격파 이벤트 구독
        EventBus.Instance?.Subscribe<TutorialEnemyDefeatedEvent>(OnTutorialEnemyDefeated);
    }

    public IEnumerator Execute()
    {
        // 이미 조건을 충족했으면 즉시 반환
        if (_conditionMet)
        {
            yield break;
        }

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
            Label = GetLabelForConfig(_config)
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

    private string GetLabelForConfig(EnemyDefeatedModuleConfig config)
    {
        // 커스텀 라벨이 설정되어 있으면 그것 사용
        if (!string.IsNullOrWhiteSpace(config.DefeatLabel))
            return config.DefeatLabel;

        // 타겟에 따라 기본 라벨 반환
        return config.Target switch
        {
            TutorialEnemyDefeatTarget.CoreOnly => "코어 처치",
            TutorialEnemyDefeatTarget.NonCoreOnly => "일반 유닛 처치",
            _ => "적 격파"
        };
    }

    public bool IsConditionMet()
    {
        return _conditionMet;
    }
}
