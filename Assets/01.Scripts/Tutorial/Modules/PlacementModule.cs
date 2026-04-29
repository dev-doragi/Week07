using System.Collections;
using UnityEngine;
using System;

/// <summary>
/// 튜토리얼 파트 배치 모듈
/// - 필요한 파트 그룹이 지정된 수량만큼 배치될 때까지 대기
/// - PartPlacedEvent를 구독하여 진행도 추적
/// </summary>
public class PlacementModule : ITutorialModule
{
    private PlacementModuleConfig _config;
    private int _partsPlaced = 0;
    private bool _conditionMet = false;

    private const int DEFENSE_PART_MIN = 1;
    private const int DEFENSE_PART_MAX = 3;
    private const int ATTACK_PART_MIN = 4;
    private const int ATTACK_PART_MAX = 6;

    public void Initialize(TutorialStep step)
    {
        _config = step.PlacementConfig;
        _partsPlaced = 0;
        _conditionMet = false;

        // 파트 배치 이벤트 구독
        EventBus.Instance?.Subscribe<PartPlacedEvent>(OnPartPlaced);
    }

    public IEnumerator Execute()
    {
        // 필요한 수량의 파트가 배치될 때까지 대기
        yield return new WaitUntil(() => _conditionMet);
    }

    public void Cleanup()
    {
        // 파트 배치 이벤트 구독 해제
        EventBus.Instance?.Unsubscribe<PartPlacedEvent>(OnPartPlaced);
    }

    private void OnPartPlaced(PartPlacedEvent evt)
    {
        if (_config == null) return;

        // 필요한 파트 그룹 확인
        if (!IsPartKeyMatchingGroup(evt.PartKey, _config.RequiredGroup, _config.RequiredPartKeys))
        {
            return;
        }

        _partsPlaced++;

        // 진행도 브로드캐스트
        EventBus.Instance?.Publish(new TutorialProgressUpdatedEvent
        {
            CurrentProgress = _partsPlaced,
            RequiredProgress = _config.RequiredAmount,
            Label = GetLabelForConfig(_config)
        });

        // 조건 달성 확인
        if (_partsPlaced >= _config.RequiredAmount)
        {
            _conditionMet = true;
        }
    }

    private bool IsPartKeyMatchingGroup(int partKey, RequiredPartGroup group, int[] customKeys)
    {
        switch (group)
        {
            case RequiredPartGroup.Any:
                return true;
            case RequiredPartGroup.Defense:
                return partKey >= DEFENSE_PART_MIN && partKey <= DEFENSE_PART_MAX;
            case RequiredPartGroup.Attack:
                return partKey >= ATTACK_PART_MIN && partKey <= ATTACK_PART_MAX;
            case RequiredPartGroup.Custom:
                if (customKeys == null || customKeys.Length == 0) return false;
                foreach (var key in customKeys)
                {
                    if (key == partKey) return true;
                }
                return false;
            default:
                return false;
        }
    }

    private string GetLabelForConfig(PlacementModuleConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.PlacementLabel))
            return config.PlacementLabel;

        return config.RequiredGroup switch
        {
            RequiredPartGroup.Attack => "공격 유닛",
            RequiredPartGroup.Defense => "방어 유닛",
            RequiredPartGroup.Custom => "선택 유닛",
            _ => "유닛"
        };
    }
}
