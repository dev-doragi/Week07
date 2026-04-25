using System;
using UnityEngine;

[Serializable]
public class StageMapReward
{
    [SerializeField] private StageMapRewardType _type;
    [SerializeField] private string _rewardId;
    [SerializeField] private int _amount = 1;
    [SerializeField] private Sprite _icon;

    public StageMapRewardType Type => _type;
    public string RewardId => _rewardId;
    public int Amount => Mathf.Max(0, _amount);
    public Sprite Icon => _icon;
}
