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

    [Header("Effect Prefabs")]
    [SerializeField] private GameObject _healEffectPrefab;

    private Coroutine _hitEffectCo;
    private EntityStatReceiver _statReceiver;
    private EntityAttacker _attacker; // 공격 로직 참조
    private UnitAnimator _animator;     // 애니메이션
    private float _currentHp;
    private TeamType _team;
    private bool _isInitialized = false;
    private UnitState _currentState = UnitState.Idle; // 현재 상태
    private bool _isOnGrid = false;

    public UnitDataSO Data => _data;
    public SpriteRenderer BaseRenderer => _baseRenderer;
    public TeamType Team => _team;
    public UnitCategory Category => _data.Category;
    public float CurrentHp => _currentHp;
    public bool IsDead => _currentState == UnitState.Dead;
    public UnitState CurrentState => _currentState;
    public EntityStatReceiver StatReceiver => _statReceiver;
    public void SetOnGrid(bool value) => _isOnGrid = value;

    public event Action<float, float> OnHpChanged;
    public event Action<Unit> OnDead;

    public void InitializeRuntime()
    {
        if (_data == null)
        {
            Debug.LogError($"[{name}] UnitDataSO가 누락되었습니다.");
            return;
        }

        _animator = GetComponent<UnitAnimator>();
        _statReceiver = GetComponent<EntityStatReceiver>();
        _team = _data.Team;
        _currentHp = _data.MaxHp;
        _isInitialized = true;

        OnHpChanged?.Invoke(_currentHp, _data.MaxHp);
        AssembleModules();

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

        if (_data.CanHeal)
        {
            var healer = gameObject.GetOrAddComponent<EntityHealer>();
            healer.Setup(this, _data.Heal.HealAmount, _data.Heal.HealCooldown, _data.Heal.HealRange);
        }
    }

    public void ForceKill()
    {
        if (IsDead) return;
        _currentHp = 0f;
        ChangeState(UnitState.Dead);
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
        if (_currentState == UnitState.Dead) return;

        _currentState = newState;

        switch (_currentState)
        {
            case UnitState.Idle:
                _animator?.PlayIdle();
                break;
            case UnitState.Attack:
                //_animator?.PlayAttack();
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

        if (_data.Category == UnitCategory.Wheel) return;

        float baseDefense = _data.BaseDefenseRate;
        float finalDefense = _statReceiver.GetModifiedValue(SupportStatType.DefenseRate, baseDefense);
        float finalDamage = hitData.Damage * (1f - Mathf.Clamp01(finalDefense));

        _currentHp -= Mathf.Max(0f, finalDamage);

        UpdateVisualFeedback();
        OnHpChanged?.Invoke(_currentHp, _data.MaxHp);

        if (_currentHp <= 0f)
            ChangeState(UnitState.Dead);
    }

    public void Heal(float amount)
    {
        if (!_isInitialized || IsDead) return;
        float maxHp = _data.MaxHp;
        float prevHp = _currentHp;
        _currentHp = Mathf.Min(_currentHp + amount, maxHp);
        if (_currentHp != prevHp)
        {
            OnHpChanged?.Invoke(_currentHp, maxHp);
            UpdateVisualFeedback();
            if (_healEffectPrefab != null && PoolManager.Instance != null)
            {
                GameObject healFX = PoolManager.Instance.Spawn(_healEffectPrefab.name, transform.position, _healEffectPrefab.transform.rotation);
                if (healFX != null)
                {
                    var ps = healFX.GetComponent<ParticleSystem>();
                    if (ps != null && healFX.GetComponent<DespawnController>() == null)
                        healFX.AddComponent<DespawnController>().Setup(ps);
                }
            }
        }
    }

    private void UpdateVisualFeedback()
    {
        if (_baseRenderer == null || _damageOverlays == null) return;

        float ratio = _currentHp / _data.MaxHp;

        if (ratio <= 0.7f && _damageOverlays.Length > 0) _damageOverlays[0].gameObject.SetActive(true);
        if (ratio <= 0.4f && _damageOverlays.Length > 1) _damageOverlays[1].gameObject.SetActive(true);
        if (ratio <= 0.1f && _damageOverlays.Length > 2) _damageOverlays[2].gameObject.SetActive(true);

        float colorVal = 0.5f + (ratio / 2f);
        if (_hitEffectCo != null) StopCoroutine(_hitEffectCo);
        _hitEffectCo = StartCoroutine(HitFlashRoutine(new Color(1f, colorVal, colorVal, 1f)));
    }

    private IEnumerator HitFlashRoutine(Color targetColor)
    {
        if (_baseRenderer == null) yield break;

        for (int i = 0; i < 2; i++)
        {
            if (_baseRenderer == null) yield break;
            _baseRenderer.color = new Color(1f, 1f, 1f, 0.75f);
            yield return new WaitForSecondsRealtime(0.05f);
            if (_baseRenderer == null) yield break;
            _baseRenderer.color = targetColor;
            yield return new WaitForSecondsRealtime(0.05f);
        }
        _hitEffectCo = null;
    }

    private void HandleDeath()
    {
        if (_team == TeamType.Enemy && _data.Category != UnitCategory.Core)
        {
            EventBus.Instance?.Publish(new EnemyDefeatedEvent());
            EventBus.Instance?.Publish(new TutorialEnemyDefeatedEvent());
        }

        // 코어 파괴 이벤트 발행
        if (_data.Category == UnitCategory.Core && _team == TeamType.Enemy)
        {
            CameraManager.Instance?.ShakeWeak();
            EventBus.Instance?.Publish(new CoreDestroyedEvent { IsPlayerBase = false });
        }
        else if (_data.Category == UnitCategory.Core && _team == TeamType.Player)
        {
            CameraManager.Instance?.ShakeWeak();
            EventBus.Instance?.Publish(new CoreDestroyedEvent { IsPlayerBase = true });
        }
        SpawnDeathEffect(_data.DeathSpawnKey, _data.BaseDeathSpawnCount);
        
        OnDead?.Invoke(this);
        if(_isOnGrid) return;

        // 그리드에서 분리 (FallingUnit이 Destroy까지 책임)
        transform.SetParent(null, true);
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 연출 시작과 동시에 스폰
        var falling = gameObject.AddComponent<FallingUnit>();
        falling.Begin();
    }

    private void SpawnDeathEffect(string key, int count)
    {
        if (string.IsNullOrEmpty(key) || count <= 0) return;
        if (PoolManager.Instance == null) return;

        if (_data.Category == UnitCategory.Core && StageManager.Instance != null)
        {
            count += (StageManager.Instance.CurrentWaveIndex * 15)
                   + (StageManager.Instance.CurrentStageIndex * 20);
        }

        for (int i = 0; i < count; i++)
            PoolManager.Instance.Spawn(key, transform.position, Quaternion.identity);
    }

    private void OnDestroy()
    {
        if (_hitEffectCo != null) StopCoroutine(_hitEffectCo);
        _hitEffectCo = null;
    }
}
