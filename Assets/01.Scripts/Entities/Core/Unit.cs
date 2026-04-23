using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 유닛 및 건물의 런타임 인스턴스를 관리하는 핵심 엔티티 컨트롤러입니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - UnitDataSO 기반의 스탯 초기화 및 FSM(상태 머신) 기반 행동 제어
/// - [IDLE, ATTACK, STUN, DEAD] 상태 간의 전환 및 상태별 로직 수행
/// - 피격(TakeDamage) 및 사망 처리, 시각적 피드백(피칠갑 오버레이) 관리
/// 
/// [이벤트 흐름]
/// - OnHpChanged: 체력 변동 시 UI 업데이트용
/// - OnDead: 유닛 파괴 시 풀링 회수 및 시스템 알림용
/// </remarks>

[RequireComponent(typeof(EntityStatReceiver))]
public class Unit : MonoBehaviour, IDamageable
{
    [Header("Data Reference")]
    [SerializeField] private UnitDataSO _data;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer _baseRenderer;

    [Tooltip("순서대로 1단계(70%), 2단계(40%), 3단계(10%) 파손 스프라이트입니다.")]
    [SerializeField] private SpriteRenderer[] _damageOverlays;

    private Coroutine _hitEffectCo;
    private EntityStatReceiver _statReceiver;
    private EntityAttacker _attacker; // 공격 로직 참조

    private float _currentHp;
    private TeamType _team;
    private bool _isInitialized = false;
    private UnitState _currentState = UnitState.Idle; // 현재 상태

    public UnitDataSO Data => _data;
    public TeamType Team => _team;
    public UnitCategory Category => _data.Category;
    public float CurrentHp => _currentHp;
    public bool IsDead => _currentState == UnitState.Dead;
    public UnitState CurrentState => _currentState;
    public EntityStatReceiver StatReceiver => _statReceiver;

    public event Action<float, float> OnHpChanged;
    public event Action<Unit> OnDead;

    public void InitializeRuntime()
    {
        if (_data == null)
        {
            Debug.LogError($"[{name}] UnitDataSO가 누락되었습니다.");
            return;
        }

        _statReceiver = GetComponent<EntityStatReceiver>();
        _team = _data.Team;
        _currentHp = _data.MaxHp;
        _isInitialized = true;

        OnHpChanged?.Invoke(_currentHp, _data.MaxHp);
        AssembleModules();

        // 초기 상태 설정
        ChangeState(UnitState.Idle);
        UpdateVisualFeedback();
    }

    private void AssembleModules()
    {
        if (_data.CanAttack)
        {
            _attacker = gameObject.GetOrAddComponent<EntityAttacker>();
            _attacker.Setup(this, _data.Attack);
        }

        if (_data.CanCollide)
        {
            var defender = gameObject.GetOrAddComponent<EntityDefender>();
            defender.Setup(this, _data.Defense);
        }

        if (_data.CanSupport)
        {
            var supporter = gameObject.GetOrAddComponent<EntitySupporter>();
            supporter.Setup(this, _data.Support);
        }
    }

    private void Update()
    {
        if (!_isInitialized || IsDead || _currentState == UnitState.Stun) return;

        if (_currentState == UnitState.Idle && _attacker != null)
        {
            if (_attacker.SearchAndCheckTarget())
            {
                ChangeState(UnitState.Attack);
            }
        }
    }

    /// <summary>
    /// 유닛의 상태를 안전하게 변경하고 관련 로직을 트리거합니다.
    /// </summary>
    public void ChangeState(UnitState newState)
    {
        if (_currentState == UnitState.Dead) return; // 이미 사망했다면 상태 변경 불가

        _currentState = newState;

        switch (_currentState)
        {
            case UnitState.Idle:
                // 대기 시 특별한 초기화가 필요하다면 여기서 수행
                break;
            case UnitState.Attack:
                // 공격 상태 진입 시의 로직은 EntityAttacker의 루프에서 처리됨
                break;
            case UnitState.Stun:
                // 스턴 시 모든 진행 중인 액션 정지
                break;
            case UnitState.Dead:
                HandleDeath();
                break;
        }
    }

    public void TakeDamage(DamageData hitData)
    {
        if (!_isInitialized || IsDead) return;

        float baseDefense = _data.BaseDefenseRate;
        float finalDefense = _statReceiver.GetModifiedValue(SupportStatType.DefenseRate, baseDefense);

        float finalDamage = hitData.Damage * (1f - Mathf.Clamp01(finalDefense));
        _currentHp -= Mathf.Max(0f, finalDamage);

        UpdateVisualFeedback();
        OnHpChanged?.Invoke(_currentHp, _data.MaxHp);

        // [기획 반영] 체력이 0 이하면 DEAD 상태로 전환
        if (_currentHp <= 0f)
        {
            ChangeState(UnitState.Dead);
        }
    }

    private void UpdateVisualFeedback()
    {
        if (_baseRenderer == null || _damageOverlays == null) return;

        float ratio = _currentHp / _data.MaxHp;

        // [기획 반영] 3단계 피칠갑 오버레이 누적 활성화
        if (ratio <= 0.7f && _damageOverlays.Length > 0) _damageOverlays[0].gameObject.SetActive(true);
        if (ratio <= 0.4f && _damageOverlays.Length > 1) _damageOverlays[1].gameObject.SetActive(true);
        if (ratio <= 0.1f && _damageOverlays.Length > 2) _damageOverlays[2].gameObject.SetActive(true);

        // 피격 시 깜빡임 효과
        float colorVal = 0.5f + (ratio / 2f);
        if (_hitEffectCo != null) StopCoroutine(_hitEffectCo);
        _hitEffectCo = StartCoroutine(HitFlashRoutine(new Color(1f, colorVal, colorVal, 1f)));
    }

    private IEnumerator HitFlashRoutine(Color targetColor)
    {
        for (int i = 0; i < 2; i++)
        {
            _baseRenderer.color = new Color(1f, 1f, 1f, 0.75f);
            yield return new WaitForSeconds(0.05f);
            _baseRenderer.color = targetColor;
            yield return new WaitForSeconds(0.05f);
        }
    }

    private void HandleDeath()
    {
        // [기획 반영] 코어 파괴 시 팀별 이벤트 발행
        if (_data.Category == UnitCategory.Core && _team == TeamType.Enemy)
        {
            SpawnEnemyDeathRats();
            EventBus.Instance?.Publish(new CoreDestroyedEvent { IsPlayerBase = false });
        }
        else if (_data.Category == UnitCategory.Core && _team == TeamType.Player)
        {
            EventBus.Instance?.Publish(new CoreDestroyedEvent { IsPlayerBase = true });
        }

        OnDead?.Invoke(this);

        if (PoolManager.Instance != null)
            PoolManager.Instance.Despawn(gameObject);
        else
            Destroy(gameObject);
    }

    private void SpawnEnemyDeathRats()
    {
        if (StageManager.Instance == null || PoolManager.Instance == null) return;

        int additionalRats = (StageManager.Instance.CurrentWaveIndex * 15) + (StageManager.Instance.CurrentStageIndex * 20);
        int totalSpawnCount = _data.BaseDeathSpawnCount + additionalRats;

        for (int i = 0; i < totalSpawnCount; i++)
        {
            PoolManager.Instance.Spawn("DropRat", transform.position, Quaternion.identity);
        }
    }
}