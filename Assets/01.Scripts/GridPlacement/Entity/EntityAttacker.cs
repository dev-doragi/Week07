using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛의 공격 AI, 타겟 탐색, 쿨다운 계산 및 발사 명령을 담당하는 모듈입니다.
/// (기존 RatAttackHandler 완벽 대체)
/// </summary>
public class EntityAttacker : MonoBehaviour
{
    [Header("Components")]
    [Tooltip("공격 모션이 있는 경우 할당하세요.")]
    [SerializeField] private Animator _animator;

    // 투사체 발사기 (프리팹에 붙어있는 IAttackPerformer들)
    private IAttackPerformer _arcPerformer;
    private IAttackPerformer _directPerformer;

    // --- 런타임 상태 ---
    private Unit _owner;
    private AttackModule _data;
    private Unit _currentTarget;
    private float _lastAttackTime;

    public Unit CurrentTarget => _currentTarget;

    public void Setup(Unit owner, AttackModule data)
    {
        _owner = owner;
        _data = data;

        // 프리팹에 부착된 Performer들 캐싱 (기존 CacheAttackPerformers 대체)
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
        MaintainOrAcquireTarget();

        if (_currentTarget == null || _currentTarget.IsDead) return;

        // 사거리 내에 있는지 검사
        if (!IsTargetInAttackDistance(_currentTarget))
        {
            _currentTarget = null; // 멀어지면 타겟 초기화
            if (_animator != null) _animator.SetBool("OnAttack", false);
            return;
        }

        // 쿨다운 검사
        if (Time.time >= _lastAttackTime + GetFinalAttackInterval())
        {
            TriggerAttack();
        }
    }

    private void TriggerAttack()
    {
        // 1. 애니메이터가 있다면 애니메이션을 통해 공격 실행 (애니메이션 이벤트로 ExecuteAttack 호출)
        if (_animator != null)
        {
            _animator.SetBool("OnAttack", true);
        }
        // 2. 애니메이터가 없다면 즉시 발사
        else
        {
            ExecuteAttack();
        }
    }

    /// <summary>
    /// 실제 투사체를 소환하는 메서드입니다. (기존 AnimAttack / TryAttack 통합)
    /// 애니메이션 이벤트(Animation Event)에서 이 메서드를 호출하게 하세요.
    /// </summary>
    public void ExecuteAttack()
    {
        if (_currentTarget == null || _currentTarget.IsDead)
        {
            if (_animator != null) _animator.SetBool("OnAttack", false);
            return;
        }

        // 마우스 코스트 감소 기믹 (기존 플레이어 타워 로직 복원)
        // if (_owner.Team == TeamType.Player && !PlacementManager.Instance.SubtractMouseCount(amount)) return;

        IAttackPerformer performer = ResolveAttackPerformer();
        if (performer != null)
        {
            bool launched = performer.TryPerformAttack(_owner, _currentTarget, _data);
            if (launched)
            {
                _lastAttackTime = Time.time;
            }
        }

        if (_animator != null) _animator.SetBool("OnAttack", false);
    }

    // ==========================================
    // 내부 유틸리티 로직 (타겟팅 및 스탯 계산)
    // ==========================================

    private void MaintainOrAcquireTarget()
    {
        // 1. 기존 타겟이 살아있고 사거리 내에 있다면 그대로 유지
        if (_currentTarget != null && !_currentTarget.IsDead && IsTargetInAttackDistance(_currentTarget))
        {
            return;
        }

        // 2. 타겟이 없거나 죽었거나 멀어졌다면 새로 탐색
        _currentTarget = FindNearestEnemy();
    }

    /// <summary>
    /// 사거리 내에 있는 적 중 가장 가까운 유닛을 찾아 반환합니다.
    /// </summary>
    private Unit FindNearestEnemy()
    {
        Unit nearestTarget = null;
        float minDistance = float.MaxValue;

        // 1. 내 팀(TeamType)에 따라 공격 대상 레이어를 결정합니다.
        // 소속은 UnitDataSO 또는 Unit 클래스에서 가져옵니다.
        int targetLayerMask = (_owner.Team == E_TeamType.Player)
            ? LayerMask.GetMask("Enemy")
            : LayerMask.GetMask("Ally");

        // 2. 물리 엔진이 해당 레이어만 찝어서 가져옵니다. (가장 큰 성능 이득)
        Collider2D[] colliders = Physics2D.OverlapCircleAll(
            transform.position,
            _data.Distance,
            targetLayerMask
        );

        for (int i = 0; i < colliders.Length; i++)
        {
            // 이제 이 레이어에는 적팀 유닛만 있으므로, 팀 체크 로직이 생략 가능해집니다.
            Unit potentialTarget = colliders[i].GetComponent<Unit>();
            if (potentialTarget == null) potentialTarget = colliders[i].GetComponentInParent<Unit>();

            // 유효성(생존 여부)만 확인합니다.
            if (potentialTarget != null && !potentialTarget.IsDead)
            {
                float distance = Vector2.Distance(transform.position, potentialTarget.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestTarget = potentialTarget;
                }
            }
        }

        return nearestTarget;
    }

    private bool IsTargetInAttackDistance(Unit target)
    {
        float actualDist = Vector2.Distance(transform.position, target.transform.position);

        // 유닛의 Size(Vector2Int) 중 큰 값을 기준으로 반지름 계산
        float myRadius = Mathf.Max(_owner.Data.Size.x, _owner.Data.Size.y) * 0.5f;
        float targetRadius = Mathf.Max(target.Data.Size.x, target.Data.Size.y) * 0.5f;

        // 덩치를 뺀 "순수 간격"이 사거리보다 짧은지 확인
        return (actualDist - myRadius - targetRadius) <= _data.Distance;
    }

    private float GetFinalAttackInterval()
    {
        float finalSpeed = _data.Speed;

        // TODO: EntityStatReceiver(버프 모듈)가 완성되면 여기서 추가 공속을 가져와 더함
        // finalSpeed += _owner.StatReceiver.AttackSpeedFlatBonus;
        // finalSpeed += _data.Speed * _owner.StatReceiver.AttackSpeedPercentBonus;

        if (finalSpeed <= 0.01f) finalSpeed = 0.01f; // 0 나누기 방지
        return 1f / finalSpeed;
    }

    private IAttackPerformer ResolveAttackPerformer()
    {
        if (_data.Trajectory == E_AttackTrajectoryType.Arc && _arcPerformer != null)
            return _arcPerformer;

        if (_data.Trajectory == E_AttackTrajectoryType.Direct && _directPerformer != null)
            return _directPerformer;

        Debug.LogWarning($"[{_owner.Data.UnitName}] {_data.Trajectory} 타입의 Performer가 프리팹에 없습니다!");
        return _directPerformer ?? _arcPerformer; // Fallback
    }
}