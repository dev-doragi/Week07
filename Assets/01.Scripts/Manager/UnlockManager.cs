using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-120)]
public class UnlockManager : MonoBehaviour
{
    public static UnlockManager Instance { get; private set; }

    private readonly HashSet<int> _lockedUnitKeys = new HashSet<int>();
    private readonly HashSet<int> _unlockedUnitKeys = new HashSet<int>();
    private readonly HashSet<int> _lockedSkillIndices = new HashSet<int>();
    private readonly HashSet<int> _unlockedSkillIndices = new HashSet<int>();
    private readonly Dictionary<int, List<int>> _skillsByClearStage = new Dictionary<int, List<int>>();

    public event Action<UnitDataSO> UnitUnlocked;
    public event Action<int> SkillUnlocked;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterLockedUnits(IReadOnlyList<UnitDataSO> units)
    {
        _lockedUnitKeys.Clear();
        _unlockedUnitKeys.Clear();

        if (units == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            UnitDataSO unit = units[i];
            if (unit == null)
                continue;

            _lockedUnitKeys.Add(unit.Key);
        }
    }

    public bool RequiresUnlock(UnitDataSO unit)
    {
        return unit != null && _lockedUnitKeys.Contains(unit.Key);
    }

    public bool IsUnitUnlocked(UnitDataSO unit)
    {
        if (unit == null)
            return true;

        return !_lockedUnitKeys.Contains(unit.Key) || _unlockedUnitKeys.Contains(unit.Key);
    }

    public bool UnlockUnit(UnitDataSO unit)
    {
        if (unit == null)
            return false;

        if (!_lockedUnitKeys.Contains(unit.Key))
            return false;

        if (!_unlockedUnitKeys.Add(unit.Key))
            return false;

        UnitUnlocked?.Invoke(unit);
        return true;
    }

    public void RegisterSkillUnlocks(IReadOnlyList<StageSkillUnlockData> skillUnlocks)
    {
        _lockedSkillIndices.Clear();
        _unlockedSkillIndices.Clear();
        _skillsByClearStage.Clear();

        if (skillUnlocks == null)
            return;

        for (int i = 0; i < skillUnlocks.Count; i++)
        {
            StageSkillUnlockData unlock = skillUnlocks[i];
            if (unlock == null || unlock.SkillIndex <= 0)
                continue;

            _lockedSkillIndices.Add(unlock.SkillIndex);

            if (!_skillsByClearStage.TryGetValue(unlock.ClearStageIndex, out List<int> skills))
            {
                skills = new List<int>();
                _skillsByClearStage.Add(unlock.ClearStageIndex, skills);
            }

            if (!skills.Contains(unlock.SkillIndex))
                skills.Add(unlock.SkillIndex);
        }
    }

    public bool RequiresSkillUnlock(int skillIndex)
    {
        return _lockedSkillIndices.Contains(skillIndex);
    }

    public bool IsSkillUnlocked(int skillIndex)
    {
        return !_lockedSkillIndices.Contains(skillIndex) || _unlockedSkillIndices.Contains(skillIndex);
    }

    public void UnlockSkillsForClearedStage(int stageIndex)
    {
        if (!_skillsByClearStage.TryGetValue(stageIndex, out List<int> skills))
            return;

        for (int i = 0; i < skills.Count; i++)
            UnlockSkill(skills[i]);
    }

    public bool UnlockSkill(int skillIndex)
    {
        if (!_lockedSkillIndices.Contains(skillIndex))
            return false;

        if (!_unlockedSkillIndices.Add(skillIndex))
            return false;

        SkillUnlocked?.Invoke(skillIndex);
        return true;
    }
}
