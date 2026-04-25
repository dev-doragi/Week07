using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EntitySupporter : MonoBehaviour
{
    private Unit _owner;
    private SupportModule _data;
    private AltarConnector _altar;
    private float _scanInterval = 0.5f;
    private Coroutine _supportRoutineCo;
    private Coroutine _healRoutineCo;
    private bool _isHealer;
    private float _healPerTick;
    private float _healInterval;
    private float _healRadius;

    // Tracks units that were buffed by this supporter in the previous scan.
    private HashSet<Unit> _previouslyBuffedUnits = new HashSet<Unit>();

    public void Setup(Unit owner, SupportModule data)
    {
        _owner = owner;
        _data = data;
        _altar = GetComponent<AltarConnector>();

        ConfigureHealer();
        Debug.Log($"[HEAL_SETUP] unit={_owner.name}, canHeal={_owner.Data.CanHeal}, category={_owner.Data.Category}, damage={_owner.Data.Attack?.Damage}, speed={_owner.Data.Attack?.Speed}, distance={_owner.Data.Attack?.Distance}");

        if (_supportRoutineCo != null) StopCoroutine(_supportRoutineCo);
        _supportRoutineCo = StartCoroutine(SupportRoutine());

        if (_isHealer)
        {
            if (_healRoutineCo != null) StopCoroutine(_healRoutineCo);
            _healRoutineCo = StartCoroutine(HealRoutine());
        }
    }

    private IEnumerator SupportRoutine()
    {
        while (_owner != null && !_owner.IsDead)
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

        int allyLayer = GetAllyMask();

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

    private IEnumerator HealRoutine()
    {
        while (_owner != null && !_owner.IsDead)
        {
            if (_altar == null || _altar.IsAltarActive)
            {
                ApplyAreaHeal();
            }

            yield return new WaitForSecondsRealtime(_healInterval);
        }
    }

    private void ConfigureHealer()
    {
        _isHealer = false;
        _healPerTick = 0f;
        _healInterval = 0f;
        _healRadius = 0f;

        if (_owner == null || _owner.Data == null) return;
        if (!_owner.Data.CanHeal) return;

        _isHealer = true;
        _healPerTick = Mathf.Abs(_owner.Data.Attack.Damage);
        _healInterval = Mathf.Max(0.01f, _owner.Data.Attack.Speed);
        _healRadius = _owner.Data.Attack.Distance;
    }

    private void ApplyAreaHeal()
    {
        if (!_isHealer || _healPerTick <= 0f || _healRadius <= 0f) return;

        Collider2D[] allies = Physics2D.OverlapCircleAll(transform.position, _healRadius, GetAllyMask());
        HashSet<Unit> healedUnits = new HashSet<Unit>();

        foreach (var col in allies)
        {
            Unit ally = col.GetComponentInParent<Unit>();
            if (ally == null || ally.IsDead) continue;
            if (!healedUnits.Add(ally)) continue;

            ally.Heal(_healPerTick);
            Debug.Log($"[HEAL] {_owner.name} -> {ally.name} +{_healPerTick:F1} | HP: {ally.CurrentHp:F1}/{ally.Data.MaxHp:F1}");
        }
    }

    private int GetAllyMask()
    {
        return (_owner.Team == TeamType.Player)
            ? LayerMask.GetMask("Ally")
            : LayerMask.GetMask("Enemy");
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
        if (_supportRoutineCo != null) StopCoroutine(_supportRoutineCo);
        if (_healRoutineCo != null) StopCoroutine(_healRoutineCo);
        _supportRoutineCo = null;
        _healRoutineCo = null;
        ClearAllAppliedBuffs();
    }

    private void OnDestroy()
    {
        if (_supportRoutineCo != null) StopCoroutine(_supportRoutineCo);
        if (_healRoutineCo != null) StopCoroutine(_healRoutineCo);
        _supportRoutineCo = null;
        _healRoutineCo = null;
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
