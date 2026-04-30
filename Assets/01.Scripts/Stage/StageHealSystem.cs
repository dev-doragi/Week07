using UnityEngine;
using System.Collections.Generic;

public class StageHealSystem : MonoBehaviour
{
    [SerializeField] private GridManager _playerGrid;

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<WaveEndedEvent>(OnWaveEnded);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<WaveEndedEvent>(OnWaveEnded);
    }

    private void OnWaveEnded(WaveEndedEvent evt)
    {
        if(!evt.IsWin) return;
        HealAllPlayerUnits();
    }

    private void HealAllPlayerUnits()
    {
        GridManager grid = _playerGrid != null ? _playerGrid : GridManager.Instance;
        if(grid == null)
        {
            return;  
        } 

        List<Unit> units = grid.GetAllLivingUnits();

        foreach(Unit unit in units)
        {
            if(unit == null || unit.IsDead) continue;
            unit.RestoreFullHp();
        }
    }
}
