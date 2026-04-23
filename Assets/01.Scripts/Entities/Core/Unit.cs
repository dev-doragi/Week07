using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 유닛 및 건물의 실제 런타임 인스턴스를 관리하는 핵심 엔티티 컨트롤러입니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - UnitDataSO 기반의 스탯 초기화 및 전투 모듈(Attack, Defender, Supporter) 자동 조립
/// - 팀 소속(E_TeamType) 및 생명주기 관리
/// 
/// [이벤트 흐름]
/// - Subscribe: (내부 모듈 조립 시 데이터 참조)
/// - Publish: CoreDestroyedEvent, (OnHpChanged/OnDead C# 이벤트)
/// </remarks>

[RequireComponent(typeof(EntityStatReceiver))]
public class Unit : MonoBehaviour
{
    [Header("Data Reference")]
    [SerializeField] private UnitDataSO _data;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer _renderer;
    private Coroutine _hitEffectCo;

    private EntityStatReceiver _statReceiver;
    private float _currentHp;
    private E_TeamType _team;
    private bool _isInitialized = false;

    public UnitDataSO Data => _data;
    public E_TeamType Team => _team;
    public float CurrentHp => _currentHp;
    public bool IsDead => _currentHp <= 0f;
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

        _team = _data.Team;
        _currentHp = _data.MaxHp;
        _isInitialized = true;

        OnHpChanged?.Invoke(_currentHp, _data.MaxHp);

        AssembleModules();

        Debug.Log($"[{_data.UnitName}] 초기화 완료 (Team: {_team}, HP: {_currentHp})");
    }

    private void AssembleModules()
    {
        if (_data.CanAttack)
        {
            var attacker = gameObject.GetOrAddComponent<EntityAttacker>();
            attacker.Setup(this, _data.Attack);
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

    public void TakeDamage(float rawDamage)
    {
        if (!_isInitialized || IsDead) return;

        float baseDefense = _data.BaseDefenseRate;
        float finalDefense = _statReceiver.GetModifiedValue(E_SupportStatType.DefenseRate, baseDefense);

        float finalDamage = rawDamage * (1f - Mathf.Clamp01(finalDefense));

        _currentHp -= Mathf.Max(0f, finalDamage);

        UpdateVisualFeedback();
        OnHpChanged?.Invoke(_currentHp, _data.MaxHp);

        if (IsDead)
        {
            HandleDeath();
        }
    }

    private void UpdateVisualFeedback()
    {
        if (_renderer == null) return;

        float ratio = _currentHp / _data.MaxHp;
        float colorVal = 0.5f + (ratio / 2f);
        Color baseColor = new Color(1f, colorVal, colorVal, 1f);

        if (_hitEffectCo != null) StopCoroutine(_hitEffectCo);
        _hitEffectCo = StartCoroutine(HitFlashRoutine(baseColor));
    }

    private IEnumerator HitFlashRoutine(Color targetColor)
    {
        for (int i = 0; i < 2; i++)
        {
            _renderer.color = new Color(1f, 1f, 1f, 0.75f); // Flash
            yield return new WaitForSeconds(0.05f);
            _renderer.color = targetColor;
            yield return new WaitForSeconds(0.05f);
        }
    }

    private void HandleDeath()
    {
        Debug.Log($"[{_data.UnitName}] 파괴됨.");

        // 적 코어 파괴
        if (_data.Category == E_UnitCategory.Core && _team == E_TeamType.Enemy)
        {
            SpawnEnemyDeathRats();
            EventBus.Instance?.Publish(new CoreDestroyedEvent { IsPlayerBase = false });
        }
        // 아군 코어 파괴
        else if (_data.Category == E_UnitCategory.Core && _team == E_TeamType.Player)
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