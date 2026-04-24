using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EntitySupporter : MonoBehaviour
{
    private Unit _owner;
    private SupportModule _data;
    private AltarConnector _altar;
    private float _scanInterval = 0.5f;

    // Tracks units that were buffed by this supporter in the previous scan.
    private HashSet<Unit> _previouslyBuffedUnits = new HashSet<Unit>();

    public void Setup(Unit owner, SupportModule data)
    {
        _owner = owner;
        _data = data;
        _altar = GetComponent<AltarConnector>();
        StartCoroutine(SupportRoutine());
    }

    private IEnumerator SupportRoutine()
    {
        while (!_owner.IsDead)
        {
            ScanAndApplyBuffs();
            yield return new WaitForSecondsRealtime(_scanInterval);
        }

        ClearAllAppliedBuffs();
    }

    private void ScanAndApplyBuffs()
    {
        if (_owner == null || _data == null) return;
        if (_altar != null && !_altar.IsAltarActive)
        {
            ClearAllAppliedBuffs();
            return;
        }

        int allyLayer = (_owner.Team == TeamType.Player)
            ? LayerMask.GetMask("Ally")
            : LayerMask.GetMask("Enemy");

        Collider2D[] allies = Physics2D.OverlapCircleAll(transform.position, _data.Radius, allyLayer);

        HashSet<Unit> currentUnits = new HashSet<Unit>();

        foreach (var col in allies)
        {
            if (col.TryGetComponent(out Unit ally) && !ally.IsDead && ally != _owner)
            {
                foreach (var effect in _data.Effects)
                {
                    if (IsTargetRoleMatch(ally, effect.TargetRoleType))
                    {
                        currentUnits.Add(ally);
                    }
                }
            }
        }

        // Remove buffs from units that are no longer in support range.
        foreach (var unit in _previouslyBuffedUnits)
        {
            if (!currentUnits.Contains(unit) && unit != null && !unit.IsDead)
                unit.StatReceiver.RemoveModifier(this);
        }

        // Apply or refresh buffs for current units in range.
        foreach (var ally in currentUnits)
        {
            foreach (var effect in _data.Effects)
            {
                if (IsTargetRoleMatch(ally, effect.TargetRoleType))
                    ally.StatReceiver.SetModifier(this, effect);
            }
        }

        _previouslyBuffedUnits = currentUnits;
    }

    private bool IsTargetRoleMatch(Unit ally, SupportTargetRoleType targetCategory)
    {
        switch (targetCategory)
        {
            case SupportTargetRoleType.All: return true;
            case SupportTargetRoleType.Attack: return ally.Data.CanAttack;
            case SupportTargetRoleType.Defense: return ally.Data.CanCollide;
            default: return false;
        }
    }

    private void OnDisable()
    {
        ClearAllAppliedBuffs();
    }

    private void OnDestroy()
    {
        ClearAllAppliedBuffs();
    }

    private void ClearAllAppliedBuffs()
    {
        foreach (var unit in _previouslyBuffedUnits)
        {
            if (unit != null && !unit.IsDead)
                unit.StatReceiver.RemoveModifier(this);
        }
        _previouslyBuffedUnits.Clear();
    }
}
