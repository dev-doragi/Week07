using UnityEngine;
using System.Collections.Generic;

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

        string displayName = string.Empty;
        Sprite displayIcon = reward.Icon;

        switch (reward.Type)
        {
            case StageMapRewardType.ProductionFacility:
                IncomeInventory inventory = FindFirstObjectByType<IncomeInventory>(FindObjectsInactive.Include);
                if (inventory != null)
                {
                    var blockNames = new List<string>();
                    for (int i = 0; i < reward.Amount; i++)
                    {
                        IncomeBlockPiece piece = inventory.AcquireRandomBlock();
                        if (piece != null)
                            blockNames.Add(piece.BlockType.ToString());
                    }

                    displayName = blockNames.Count > 0
                        ? $"생산 블록 획득: {string.Join(", ", blockNames)}"
                        : "생산 블록 획득";
                }
                else
                {
                    Debug.LogWarning("[StageMapReward] IncomeInventory not found. Production block reward was not applied.");
                }
                break;
            case StageMapRewardType.RatTowerUnlock:
                UnitDataSO unit = reward.UnitUnlock;
                UnlockManager unlockManager = FindFirstObjectByType<UnlockManager>(FindObjectsInactive.Include);
                bool unlocked = unlockManager != null && unlockManager.UnlockUnit(unit);
                displayName = unit != null ? $"쥐 타워 해금: {unit.UnitName}" : $"쥐 타워 해금: {reward.RewardId}";
                displayIcon = unit != null && unit.Icon != null ? unit.Icon : displayIcon;
                Debug.Log($"[StageMapReward] Rat tower unlock {(unlocked ? "applied" : "queued/skipped")}: {displayName}");
                break;
        }

        EventBus.Instance?.Publish(new StageMapRewardAppliedEvent
        {
            NodeId = nodeId,
            RewardType = reward.Type,
            RewardId = reward.RewardId,
            Amount = reward.Amount,
            DisplayName = displayName,
            Icon = displayIcon
        });
    }
}
