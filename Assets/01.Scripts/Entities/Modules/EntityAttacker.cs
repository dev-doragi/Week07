using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EntityAttacker : MonoBehaviour
{
    private static float _doctrineDamageMultiplier = 1f;

    private IAttacker _arcPerformer;
    private IAttacker _directPerformer;
    private Unit _owner;
    private AttackModule _data;
    private Unit _currentTarget;
    private float _lastAttackTime;
    private float _attackCooldown = 0f;

    public static void SetDoctrineDamageMultiplier(float multiplier)
    {
        _doctrineDamageMultiplier = Mathf.Max(0.01f, multiplier);
        Debug.Log($"[EntityAttacker] Doctrine damage multiplier set: x{_doctrineDamageMultiplier:0.##}");
    }

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
        
        _attackCooldown -= Time.deltaTime;
        ProcessAttackCycle();
    }

    private void ProcessAttackCycle()
    {
        if (_currentTarget == null || _currentTarget.IsDead)
        {
            _currentTarget = SearchBestTarget();
            if(_currentTarget == null)
            {
                _owner.ChangeState(UnitState.Idle);
                return;    
            }
        }

        if (_attackCooldown <= 0f)
        {
            ExecuteAttack();
        }
    }

    private void ExecuteAttack()
    {
        if (_owner.Team == TeamType.Player && ResourceManager.Instance != null)
        {
            if (ResourceManager.Instance.CurrentMouse < _data.AttackCost)
            {
                Debug.Log($"[EntityAttacker] {_owner.name} 공격 비용 부족 | 필요: {_data.AttackCost}");
                return;
            }
            ResourceManager.Instance.SubtractMouseCount(_data.AttackCost);
        }

        IAttacker performer = (_data.Trajectory == AttackTrajectoryType.Arc) ? _arcPerformer : _directPerformer;
        if (performer != null)
        {
            AttackModule attackDataForShot = BuildAttackDataForShot();
            if (performer.TryPerformAttack(_owner, _currentTarget, attackDataForShot))
            {
                        
                float baseAttackInterval = Mathf.Max(0.01f, _data.Speed);
                float baseAttacksPerSecond = 1f / baseAttackInterval;
                float modifiedAttacksPerSecond = _owner.StatReceiver.GetModifiedValue(SupportStatType.AttackSpeed, baseAttacksPerSecond);
                float attackInterval = 1f / Mathf.Max(0.1f, modifiedAttacksPerSecond);
                
                _attackCooldown = attackInterval;
                var animator = _owner.GetComponent<UnitAnimator>();
                animator?.PlayAttack(_data.Speed > 0 ? 1f / _data.Speed : 0f);
                //_owner.ChangeState(UnitState.Idle);
                
                Debug.Log($"[EntityAttacker] {_owner.name} 공격 실행 | 다음 공격까지: {attackInterval:F2}초");
            }
            else
            {
                Debug.LogWarning($"[EntityAttacker] {_owner.name} 공격 실패 | Target: {_currentTarget.name}");
            }
        }
        else
        {
            Debug.LogError($"[EntityAttacker] Performer가 설정되지 않음 | Trajectory: {_data.Trajectory}");
        }
    }

    private AttackModule BuildAttackDataForShot()
    {
        float baseDamage = _data.Damage;
        float modifiedDamage = _owner != null && _owner.StatReceiver != null
            ? _owner.StatReceiver.GetModifiedValue(SupportStatType.AttackDamage, baseDamage)
            : baseDamage;
        modifiedDamage *= _doctrineDamageMultiplier;

        return new AttackModule
        {
            Damage = modifiedDamage,
            Speed = _data.Speed,
            Distance = _data.Distance,
            AttackCost = _data.AttackCost,
            Trajectory = _data.Trajectory,
            Targeting = _data.Targeting,
            Area = _data.Area,
            RangeRadius = _data.RangeRadius,
            PiercingCount = _data.PiercingCount,
            Penetration = _data.Penetration,
            PiercingDecay = _data.PiercingDecay,
            ProjectilePrefab = _data.ProjectilePrefab
        };
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
