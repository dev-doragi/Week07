using UnityEngine;

public struct StageMapNodeSelectedEvent
{
    public string NodeId;
    public int StageIndex;
}

public struct StageMapRewardAppliedEvent
{
    public string NodeId;
    public StageMapRewardType RewardType;
    public string RewardId;
    public int Amount;
    public string DisplayName;
    public Sprite Icon;
    public string ChoiceSource;
    public int ChoiceIndex;
    public string ChoiceKey;
    public string ChoiceLabel;
}

public struct StageMapVisibilityChangedEvent
{
    public bool IsVisible;
}

public struct DoctrineSelectionConfirmedEvent
{
    public string NodeId;
    public int RowIndex;
}
