public struct StageMapNodeSelectedEvent
{
    public string NodeId;
    public int StageIndex;
    public int WaveIndex;
}

public struct StageMapRewardAppliedEvent
{
    public string NodeId;
    public StageMapRewardType RewardType;
    public string RewardId;
    public int Amount;
}
