using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛의 공격 AI, 타겟 탐색, 쿨다운 계산 및 발사 명령을 담당하는 모듈입니다.
/// (기존 RatAttackHandler 및 RatTargetFinder 로직 통합)
/// </summary>
public class EntityAttacker : MonoBehaviour
{
    [Header("Components")]
    [Tooltip("공격 모션이 있는 경우 할당하세요.")]
    [SerializeField] private Animator _animator;

    private IAttackPerformer _arcPerformer;
    private IAttackPerformer _directPerformer;

    private Unit _owner;
    private AttackModule _data;
    private Unit _currentTarget;
    private float _lastAttackTime;

    public Unit CurrentTarget => _currentTarget;

    public void Setup(Unit owner, AttackModule data)
    {
        _owner = owner;
        _data = data;

        IAttackPerformer[] performers = GetComponents<IAttackPerformer>();
        foreach (var p in performers)
        {
            if (p.GetType().Name.Contains("Arc")) _arcPerformer = p;
            else if (p.GetType().Name.Contains("Direct")) _directPerformer = p;
        }
    }

    private void Update()
    {
        if (_owner == null || _owner.IsDead || _data == null) return;
        ProcessAutoAttack();
    }

    private void ProcessAutoAttack()
    {
        // 타겟이 없거나 죽었거나 사거리를 벗어났다면 새 타겟 탐색
        if (_currentTarget == null || _currentTarget.IsDead || !IsTargetInAttackDistance(_currentTarget))
        {
            _currentTarget = SearchBestTarget();

            // 새 타겟이 없다면 애니메이션 중지
            if (_currentTarget == null && _animator != null)
            {
                _animator.SetBool("OnAttack", false);
                return;
            }
        }

        if (_currentTarget == null) return;

        // 쿨다운 검사 및 공격 트리거
        if (Time.time >= _lastAttackTime + GetFinalAttackInterval())
        {
            TriggerAttack();
        }
    }

    /// <summary>
    /// [병합된 로직] 사거리 내 적 중 '코어'를 최우선으로, 그 외엔 '가장 가까운 적'을 탐색합니다.
    /// </summary>
    private Unit SearchBestTarget()
    {
        int targetLayerMask = (_owner.Team == E_TeamType.Player) ? LayerMask.GetMask("Enemy") : LayerMask.GetMask("Ally");

        Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(transform.position, _data.Distance, targetLayerMask);

        Unit nearestNonCore = null;
        float minDistance = float.MaxValue;

        foreach (var col in potentialTargets)
        {
            Unit unit = col.GetComponent<Unit>();
            if (unit == null) unit = col.GetComponentInParent<Unit>();

            if (unit == null || unit.IsDead) continue;

            // [1순위] 사거리 내 적 코어가 발견되면 즉시 반환 (기존 RatTargetFinder 로직)
            if (unit.Data.Category == E_UnitCategory.Core)
            {
                return unit;
            }

            // [2순위] 가장 가까운 유닛 후보군 저장
            float dist = Vector2.Distance(transform.position, unit.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearestNonCore = unit;
            }
        }

        return nearestNonCore;
    }

    private void TriggerAttack()
    {
        if (_animator != null) _animator.SetBool("OnAttack", true);
        else ExecuteAttack();
    }

    public void ExecuteAttack()
    {
        if (_currentTarget == null || _currentTarget.IsDead)
        {
            if (_animator != null) _animator.SetBool("OnAttack", false);
            return;
        }

        IAttackPerformer performer = ResolveAttackPerformer();
        if (performer != null)
        {
            if (performer.TryPerformAttack(_owner, _currentTarget, _data))
            {
                _lastAttackTime = Time.time;
            }
        }

        if (_animator != null) _animator.SetBool("OnAttack", false);
    }

    private bool IsTargetInAttackDistance(Unit target)
    {
        float actualDist = Vector2.Distance(transform.position, target.transform.position);
        float myRadius = Mathf.Max(_owner.Data.Size.x, _owner.Data.Size.y) * 0.5f;
        float targetRadius = Mathf.Max(target.Data.Size.x, target.Data.Size.y) * 0.5f;
        return (actualDist - myRadius - targetRadius) <= _data.Distance;
    }

    private float GetFinalAttackInterval()
    {
        float finalSpeed = _data.Speed;
        // TODO: StatReceiver 완성 시 공속 버프 연동
        if (finalSpeed <= 0.01f) finalSpeed = 0.01f;
        return 1f / finalSpeed;
    }

    private IAttackPerformer ResolveAttackPerformer()
    {
        if (_data.Trajectory == E_AttackTrajectoryType.Arc && _arcPerformer != null) return _arcPerformer;
        if (_data.Trajectory == E_AttackTrajectoryType.Direct && _directPerformer != null) return _directPerformer;
        return _directPerformer ?? _arcPerformer;
    }
}