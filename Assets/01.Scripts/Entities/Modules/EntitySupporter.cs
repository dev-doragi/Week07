using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 유닛 주변 아군에게 버프 및 지원 효과를 부여하는 모듈입니다.
/// </summary>
public class EntitySupporter : MonoBehaviour
{
    private Unit _owner;
    private SupportModule _data;
    private AltarConnector _altar;
    private float _scanInterval = 0.5f;

    // 이전 틱에 버프를 적용했던 대상 추적
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

        // 지원 유닛 사망 시 기존 버프 해제
        foreach (var unit in _previouslyBuffedUnits)
        {
            if (unit != null && !unit.IsDead)
                unit.StatReceiver.RemoveModifier(this);
        }
        _previouslyBuffedUnits.Clear();
    }

    private void ScanAndApplyBuffs()
    {
        if (_owner == null || _data == null) return;
        if (_altar != null && !_altar.IsAltarActive)
        {
            // 제단 비활성화 시 기존 버프 해제
            foreach (var unit in _previouslyBuffedUnits)
            {
                if (unit != null && !unit.IsDead)
                    unit.StatReceiver.RemoveModifier(this);
            }
            _previouslyBuffedUnits.Clear();
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

        // 범위에서 벗어난 유닛의 버프 제거
        foreach (var unit in _previouslyBuffedUnits)
        {
            if (!currentUnits.Contains(unit) && unit != null && !unit.IsDead)
                unit.StatReceiver.RemoveModifier(this);
        }

        // 현재 범위 유닛에 버프 재적용 (덮어쓰기)
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
}