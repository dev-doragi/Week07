using System;
using UnityEngine;

[Serializable]
public class StageMapReward
{
    [SerializeField] private StageMapRewardType _type;
    [SerializeField] private string _rewardId;
    [SerializeField] private int _amount = 1;
    [SerializeField] private Sprite _icon;
    [SerializeField] private UnitDataSO _unitUnlock;

    public StageMapRewardType Type => _type;
    public string RewardId => _rewardId;
    public int Amount => Mathf.Max(0, _amount);
    public Sprite Icon => _icon;
    public UnitDataSO UnitUnlock => _unitUnlock;

    public static StageMapReward ProductionFacility(int amount = 1)
    {
        return new StageMapReward
        {
            _type = StageMapRewardType.ProductionFacility,
            _rewardId = "ProductionFacility",
            _amount = Mathf.Max(1, amount)
        };
    }

    public static StageMapReward RatTowerUnlock(UnitDataSO unit)
    {
        return new StageMapReward
        {
            _type = StageMapRewardType.RatTowerUnlock,
            _rewardId = unit != null ? unit.Key.ToString() : string.Empty,
            _amount = unit != null ? 1 : 0,
            _icon = unit != null ? unit.Icon : null,
            _unitUnlock = unit
        };
    }
}
