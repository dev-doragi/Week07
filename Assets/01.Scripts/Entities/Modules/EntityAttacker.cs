using System.Linq;
using UnityEngine;

/// <summary>
/// 유닛의 공격 행동을 담당하며, 기획된 타겟팅 정책에 따라 발사를 수행합니다.
/// </summary>
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

        var performers = GetComponents<IAttacker>();
        _arcPerformer = performers.FirstOrDefault(p => p.GetType().Name.Contains("Arc"));
        _directPerformer = performers.FirstOrDefault(p => p.GetType().Name.Contains("Direct"));
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

        // 공격 속도에 따른 간격 계산
        float interval = 1f / Mathf.Max(0.01f, _owner.StatReceiver.GetModifiedValue(SupportStatType.AttackSpeed, _data.Speed));

        if (Time.time >= _lastAttackTime + interval)
        {
            ExecuteAttack();
        }
    }

    private void ExecuteAttack()
    {
        // 쥐 자원 체크
        if (_owner.Team == TeamType.Player && ResourceManager.Instance != null)
        {
            if (ResourceManager.Instance.CurrentMouse < _data.AttackCost) return;
            ResourceManager.Instance.SubtractMouseCount(_data.AttackCost);
        }

        IAttacker performer = (_data.Trajectory == AttackTrajectoryType.Arc) ? _arcPerformer : _directPerformer;
        if (performer != null && performer.TryPerformAttack(_owner, _currentTarget, _data))
        {
            _lastAttackTime = Time.time;
            // 공격 1회 후 다시 Idle로
            _owner.ChangeState(UnitState.Idle);
        }
    }

    private Unit SearchBestTarget()
    {
        int mask = (_owner.Team == TeamType.Player) ? LayerMask.GetMask("Enemy") : LayerMask.GetMask("Ally");

        var hits = Physics2D.OverlapCircleAll(transform.position, _data.Distance, mask);
        return hits.Select(h => h.GetComponent<Unit>())
                   .Where(u => u != null && !u.IsDead)
                   .OrderBy(u => u.transform.position.y)
                   .ThenBy(u => Vector2.Distance(transform.position, u.transform.position))
                   .FirstOrDefault();
    }
}