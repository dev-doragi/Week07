using UnityEngine;

public class StageMapRewardApplier : MonoBehaviour
{
    public void Apply(string nodeId, StageMapReward reward, int ritualPoints)
    {
        if (ritualPoints > 0)
        {
            Debug.Log($"[StageMapReward] Ritual points +{ritualPoints} (pending persistent ritual point system).");
        }

        if (reward == null || reward.Type == StageMapRewardType.None || reward.Amount <= 0)
            return;

        switch (reward.Type)
        {
            case StageMapRewardType.ProductionFacility:
                IncomeInventory inventory = FindFirstObjectByType<IncomeInventory>(FindObjectsInactive.Include);
                if (inventory != null)
                {
                    for (int i = 0; i < reward.Amount; i++)
                        inventory.AcquireRandomBlock();
                }
                else
                {
                    Debug.LogWarning("[StageMapReward] IncomeInventory not found. Production block reward was not applied.");
                }
                break;
            case StageMapRewardType.RatTowerUnlock:
                Debug.Log($"[StageMapReward] Rat tower unlock queued: {reward.RewardId}");
                break;
        }

        EventBus.Instance?.Publish(new StageMapRewardAppliedEvent
        {
            NodeId = nodeId,
            RewardType = reward.Type,
            RewardId = reward.RewardId,
            Amount = reward.Amount
        });
    }
}
