using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-120)]
public class UnlockManager : MonoBehaviour
{
    public static UnlockManager Instance { get; private set; }

    private readonly HashSet<int> _lockedUnitKeys = new HashSet<int>();
    private readonly HashSet<int> _unlockedUnitKeys = new HashSet<int>();

    public event Action<UnitDataSO> UnitUnlocked;

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
}
