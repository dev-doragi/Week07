using UnityEngine;
using System.Collections.Generic;

public class StageMapRewardApplier : MonoBehaviour
{
    public void Apply(
        string nodeId,
        StageMapReward reward,
        int ritualPoints,
        string choiceSource = null,
        int choiceIndex = -1,
        string choiceKey = null,
        string choiceLabel = null)
    {
        if (ritualPoints > 0)
            AddDoctrinePoint(ritualPoints);

        if (reward == null || reward.Type == StageMapRewardType.None || reward.Amount <= 0)
            return;

        string displayName = string.Empty;
        Sprite displayIcon = reward.Icon;

        switch (reward.Type)
        {
            case StageMapRewardType.ProductionFacility:
                displayName = ApplyProductionFacilityReward(reward.Amount);
                break;
            case StageMapRewardType.RatTowerUnlock:
                UnitDataSO unit = reward.UnitUnlock;
                UnlockManager unlockManager = FindFirstObjectByType<UnlockManager>(FindObjectsInactive.Include);
                bool unlocked = unlockManager != null && unlockManager.UnlockUnit(unit);
                displayName = unit != null ? $"쥐 해금: {unit.UnitName}" : $"쥐 해금: {reward.RewardId}";
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
            Icon = displayIcon,
            ChoiceSource = choiceSource,
            ChoiceIndex = choiceIndex,
            ChoiceKey = choiceKey,
            ChoiceLabel = choiceLabel
        });
    }

    public void AddDoctrinePoint(int amount)
    {
        if (amount <= 0)
            return;

        DoctrineManager doctrineManager = FindFirstObjectByType<DoctrineManager>(FindObjectsInactive.Include);
        if (doctrineManager == null)
        {
            Debug.LogWarning("[StageMapReward] DoctrineManager not found. Doctrine point reward skipped.");
            return;
        }

        doctrineManager.AddDoctrinePoint(amount);
    }

    private static string ApplyProductionFacilityReward(int amount)
    {
        IncomeInventory inventory = FindFirstObjectByType<IncomeInventory>(FindObjectsInactive.Include);
        if (inventory == null)
        {
            Debug.LogWarning("[StageMapReward] IncomeInventory not found. Production block reward was not applied.");
            return "생산 시설 블록 획득";
        }

        var blockNames = new List<string>();
        for (int i = 0; i < amount; i++)
        {
            IncomeBlockPiece piece = inventory.AcquireRandomBlock();
            if (piece != null)
                blockNames.Add(GetBlockDisplayName(piece.BlockType));
        }

        return blockNames.Count > 0
            ? $"생산 시설 블록 획득: {string.Join(", ", blockNames)}"
            : "생산 시설 블록 획득";
    }

    private static string GetBlockDisplayName(IncomeBlockType type)
    {
        return type switch
        {
            IncomeBlockType.I => "I형",
            IncomeBlockType.J => "J형",
            IncomeBlockType.L => "L형",
            IncomeBlockType.O => "O형",
            IncomeBlockType.S => "S형",
            IncomeBlockType.T => "T형",
            IncomeBlockType.Z => "Z형",
            IncomeBlockType.CoreCross => "코어 십자형",
            IncomeBlockType.CoreSquare => "코어 사각형",
            _ => type.ToString()
        };
    }
}
