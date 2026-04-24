using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EntityAttacker : MonoBehaviour
{
    private IAttacker _arcPerformer;
    private IAttacker _directPerformer;
    private Unit _owner;
    private AttackModule _data;
    private Unit _currentTarget;
    private float _lastAttackTime;

    public void Setup(Unit owner, AttackModule data)
    {
        _owner = owner;
        _data = data;

        switch (data.Trajectory)
        {
            case AttackTrajectoryType.Arc:
                {
                    var arc = GetComponent<ArcAttacker>();
                    if (arc == null) arc = gameObject.AddComponent<ArcAttacker>();
                    arc.EnsureSpawnPoint("Muzzle");
                    _arcPerformer = arc;
                    break;
                }
            case AttackTrajectoryType.Direct:
                {
                    var direct = GetComponent<DirectAttacker>();
                    if (direct == null) direct = gameObject.AddComponent<DirectAttacker>();
                    _directPerformer = direct;
                    break;
                }
            default:
                {
                    var performers = GetComponents<IAttacker>();
                    _arcPerformer = performers.FirstOrDefault(p => p.GetType().Name.Contains("Arc"));
                    _directPerformer = performers.FirstOrDefault(p => p.GetType().Name.Contains("Direct"));
                    break;
                }
        }
    }

    public bool SearchAndCheckTarget()
    {
        _currentTarget = SearchBestTarget();
        return _currentTarget != null;
    }

    private void Update()
    {
        if (_owner == null || _owner.IsDead || _owner.CurrentState != UnitState.Attack) return;
        ProcessAttackCycle();
    }

    private void ProcessAttackCycle()
    {
        if (_currentTarget == null || _currentTarget.IsDead)
        {
            _owner.ChangeState(UnitState.Idle);
            return;
        }

        float interval = 1f / Mathf.Max(0.01f, _owner.StatReceiver.GetModifiedValue(SupportStatType.AttackSpeed, _data.Speed));
        if (Time.time >= _lastAttackTime + interval)
        {
            ExecuteAttack();
        }
    }

    private void ExecuteAttack()
    {
        if (_owner.Team == TeamType.Player && ResourceManager.Instance != null)
        {
            if (ResourceManager.Instance.CurrentMouse < _data.AttackCost) return;
            ResourceManager.Instance.SubtractMouseCount(_data.AttackCost);
        }

        IAttacker performer = (_data.Trajectory == AttackTrajectoryType.Arc) ? _arcPerformer : _directPerformer;
        if (performer != null && performer.TryPerformAttack(_owner, _currentTarget, _data))
        {
            _lastAttackTime = Time.deltaTime;
            _owner.ChangeState(UnitState.Idle);
        }
    }

    private Unit SearchBestTarget()
    {
        int mask = (_owner.Team == TeamType.Player) ? LayerMask.GetMask("Enemy") : LayerMask.GetMask("Ally");
        Vector2 searchOrigin = transform.position;

        var hits = Physics2D.OverlapCircleAll(searchOrigin, _data.Distance, mask);

        if (hits.Length == 0) return null;

        var candidates = hits
            .Select(h => h.GetComponentInParent<Unit>())
            .Where(u => u != null && !u.IsDead && u.Category != UnitCategory.Wheel) // Wheel 제외
            .Distinct()
            .ToList();

        if (candidates.Count == 0) return null;

        return _data.Targeting switch
        {
            TargetingPolicy.Closest =>
                candidates.OrderBy(u => Vector2.Distance(searchOrigin, u.transform.position))
                          .FirstOrDefault(),

            TargetingPolicy.TowardCore =>
                candidates.OrderByDescending(u => u.Category == UnitCategory.Core)
                          .ThenBy(u => _owner.Team == TeamType.Player
                              ? -u.transform.position.y   // 아군: Y 큰 것(음수로 역순)
                              : u.transform.position.y)   // 적: Y 작은 것(정순)
                          .FirstOrDefault(),

            TargetingPolicy.PriorityAttacker =>
                candidates.OrderByDescending(u => u.Data.CanAttack)
                          .ThenBy(u => Vector2.Distance(searchOrigin, u.transform.position))
                          .FirstOrDefault(),

            _ => candidates.FirstOrDefault()
        };
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_data == null) return;

        bool hasTarget = _currentTarget != null && !_currentTarget.IsDead;
        bool isAttacking = _owner != null && _owner.CurrentState == UnitState.Attack;

        if (isAttacking && hasTarget)
            UnityEditor.Handles.color = new Color(1f, 0.3f, 0.3f, 0.25f);
        else
            UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.15f);

        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.back, _data.Distance);
        Gizmos.color = isAttacking ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 1f, 0f);
        Gizmos.DrawWireSphere(transform.position, _data.Distance);

        if (_data.Area == AreaType.Splash && _data.RangeRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, _data.RangeRadius);
        }

        if (hasTarget)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, _currentTarget.transform.position);
            Gizmos.DrawWireSphere(_currentTarget.transform.position, 0.2f);

            string ownerState = _owner != null ? _owner.CurrentState.ToString() : "?";
            string label = $"[{ownerState}] → {_currentTarget.name}\nHP:{_currentTarget.CurrentHp:F0}";
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(_currentTarget.transform.position + Vector3.up * 0.35f, label);
        }

        if (_owner != null)
        {
            string stateLabel = $"[{_owner.name}]\nState: {_owner.CurrentState}\nTeam: {_owner.Team}";
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, stateLabel);
        }
    }
#endif
}