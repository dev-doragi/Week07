using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class SiegeChargeHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager _grid;
    [SerializeField] private GameObject _enemyGridObject;
    [SerializeField] private Collider2D _crashCollider;
    [SerializeField] private LayerMask _enemyLayer;

    [Header("Motion")]
    [SerializeField] private float _pullBackDistance = 1f;
    [SerializeField] private float _pullBackDuration = 0.8f;
    [SerializeField] private float _crashDistance = 10f;
    [SerializeField] private float _crashDuration = 1f;
    [SerializeField] private float _returnDuration = 2.5f;

    [Header("Shake")]
    [SerializeField] private float _shakeStrength = 0.15f;
    [SerializeField] private float _shakeDuration = 0.3f;
    [SerializeField] private int _shakeVibrato = 15;
    [SerializeField, Range(0f, 90f)] private float _shakeRandomness = 90f;

    [Header("Damage")]
    [SerializeField, Range(0f, 1f)] private float _penetration = 0f;

    [Header("Doctrine - Ram")]
    [SerializeField, Range(0f, 1f)] private float _doctrineEnemyCollisionPowerReductionPercent = 0f;
    [SerializeField, Min(0f)] private float _doctrineBonusDamagePercent = 0f;
    [SerializeField, Min(0f)] private float _doctrineStunDurationSeconds = 0f;

    public event Action OnCrashEnd;

    private Sequence _sequence;
    private Vector3 _startPosition;
    private EnemyGridManager _enemyGrid;
    private bool _isCrashing;
    private bool _isDashing;
    private bool _hasImpactedThisCrash;
    private readonly Dictionary<Unit, Coroutine> _enemyStunRoutines = new Dictionary<Unit, Coroutine>();

    public bool IsCrashing => _isCrashing;

    private void Awake()
    {
        if (_grid != null) _startPosition = _grid.transform.position;
    }

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<PlayerGridChangedEvent>(OnGridChanged);
        EventBus.Instance?.Subscribe<EnemyGridChangedEvent>(OnGridChanged);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<PlayerGridChangedEvent>(OnGridChanged);
        EventBus.Instance?.Unsubscribe<EnemyGridChangedEvent>(OnGridChanged);
    }

    private void OnGridChanged<T>(T _) => RefreshCollisionPowerUI();

    private void OnDestroy()
    {
        CancelCrash();
    }

    public void ExecuteCrash()
    {
        if (_isCrashing) return;

        _enemyGrid = ResolveEnemyGrid();
        if (_enemyGrid == null)
        {
            Debug.LogWarning("[SiegeChargeHandler] EnemyGridManager not found, crash cancelled.");
            return;
        }

        _enemyGrid.RegisterExistingUnitsFromChildren();
        GameLogger.Instance?.RecordRamUsed(nameof(SiegeChargeHandler));
        PlayCrashSequence();
    }

    public void CancelCrash()
    {
        if (!_isCrashing) return;

        _sequence?.Kill();
        _sequence = null;
        
        // 상태 완전히 초기화 (쿨타임 안 돌기)
        _isCrashing = false;
        _isDashing = false;
        _hasImpactedThisCrash = false;
        
        // 그리드를 원래 위치로 즉시 복귀
        if (_grid != null)
            _grid.transform.position = _startPosition;
        
        Debug.Log("[SiegeChargeHandler] Crash cancelled - all state reset");
    }

    public void SetDoctrineEnemyCollisionPowerReductionPercent(float percent)
    {
        _doctrineEnemyCollisionPowerReductionPercent = Mathf.Clamp01(percent);
        Debug.Log($"[SiegeChargeHandler] Doctrine enemy collision power reduction set: {_doctrineEnemyCollisionPowerReductionPercent * 100f:0}%");
    }

    public void SetDoctrineBonusDamagePercent(float percent)
    {
        _doctrineBonusDamagePercent = Mathf.Max(0f, percent);
        Debug.Log($"[SiegeChargeHandler] Doctrine bonus damage set: {_doctrineBonusDamagePercent * 100f:0}%");
    }

    public void SetDoctrineStunDurationSeconds(float seconds)
    {
        _doctrineStunDurationSeconds = Mathf.Max(0f, seconds);
        Debug.Log($"[SiegeChargeHandler] Doctrine stun duration set: {_doctrineStunDurationSeconds:0.##}s");
    }

    private EnemyGridManager ResolveEnemyGrid()
    {
        EnemyGridManager resolved = null;

        if (_enemyGridObject != null)
        {
            resolved = _enemyGridObject.GetComponentInChildren<EnemyGridManager>(true);
        }

        if (resolved == null)
        {
            resolved = FindAnyObjectByType<EnemyGridManager>();
        }

        return resolved;
    }

    private void PlayCrashSequence()
    {
        Vector3 leftTarget = _startPosition + Vector3.left * _pullBackDistance;
        Vector3 rightTarget = _startPosition + Vector3.right * _crashDistance;

        _isCrashing = true;
        _isDashing = false;
        _hasImpactedThisCrash = false;

        RefreshCollisionPowerUI();

        var gridTransform = _grid.transform;
        _sequence = DOTween.Sequence();

        _sequence.Append(gridTransform.DOMove(leftTarget, _pullBackDuration).SetEase(Ease.OutCubic));
        _sequence.AppendCallback(() => _isDashing = true);
        _sequence.Append(gridTransform.DOMove(rightTarget, _crashDuration).SetEase(Ease.InExpo));
        _sequence.AppendCallback(() => { _isDashing = false; if (!_hasImpactedThisCrash) TriggerImpact(); });
        _sequence.Append(gridTransform.DOShakePosition(_shakeDuration, new Vector3(_shakeStrength, _shakeStrength, 0f), _shakeVibrato, _shakeRandomness, false, true));
        _sequence.Append(gridTransform.DOMove(_startPosition, _returnDuration).SetEase(Ease.InQuad));

        _sequence.OnComplete(EndCrash);
        _sequence.OnKill(EndCrash); // Kill 시에도 항상 EndCrash 호출
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!_isDashing || _hasImpactedThisCrash) return;
        
        bool isEnemyTag = collision.CompareTag("Enemy");
        bool isEnemyLayer = (_enemyLayer.value & (1 << collision.gameObject.layer)) != 0;
        
        if (!isEnemyTag && !isEnemyLayer)
            return;

        Debug.Log($"[SiegeChargeHandler] 충돌 감지: {collision.gameObject.name}");
        OnEnemyHit();
    }

    private void OnEnemyHit()
    {
        // 이미 한 번 충돌했으면 무시
        if (_hasImpactedThisCrash) return;
        
        _hasImpactedThisCrash = true;

        // 돌진 연출은 계속 진행 (시퀀스 중단 안 함)
        // 데미지 계산만 수행
        TriggerImpact();

        Debug.Log("[SiegeChargeHandler] 충돌 데미지 계산 완료, 돌진 연출 계속 진행");
    }

    private void EndCrash()
    {
        if (!_isCrashing) return;
        _isCrashing = false;
        _isDashing = false;
        OnCrashEnd?.Invoke();
    }

    private void TriggerImpact()
    {
        _enemyGrid = _enemyGrid != null ? _enemyGrid : ResolveEnemyGrid();
        if (_enemyGrid == null)
        {
            Debug.LogWarning("[SiegeChargeHandler] EnemyGridManager not found, skipping damage.");
            return;
        }

        _enemyGrid.RegisterExistingUnitsFromChildren();

        float playerCP = _grid != null ? _grid.CalculateTotalCollisionPower() : 0f;
        float enemyCP = _enemyGrid.CalculateTotalCollisionPower();
        enemyCP *= 1f - _doctrineEnemyCollisionPowerReductionPercent;
        ResolveCollision(playerCP, enemyCP, _enemyGrid);
    }

    private void ResolveCollision(float playerCP, float enemyCP, EnemyGridManager enemyGrid)
    {
        float delta = Mathf.Abs(playerCP - enemyCP);
        bool isPlayerLosing = playerCP < enemyCP;

        Debug.Log($"[Collision] PlayerCP: {playerCP} | EnemyCP: {enemyCP} | Delta: {delta}");

        if (delta > 0f)
        {
            if (isPlayerLosing)
            {
                var targets = _grid.GetAllLivingUnits();
                DistributeDamage(targets, delta, TeamType.Enemy, "Player");
            }
            else
            {
                var targets = enemyGrid.GetAllLivingUnits();
                float boostedDamage = delta * (1f + Mathf.Max(0f, _doctrineBonusDamagePercent));
                DistributeDamage(targets, boostedDamage, TeamType.Player, "Enemy");
            }
        }

        if (_doctrineStunDurationSeconds > 0f)
        {
            ApplyStunToAllEnemies(_doctrineStunDurationSeconds);
        }

        EventBus.Instance?.Publish(new SiegeCollisionResolvedEvent { PlayerCP = playerCP, EnemyCP = enemyCP, Delta = delta, IsPlayerLosing = isPlayerLosing });
    }

    private void DistributeDamage(List<Unit> targets, float totalDamage, TeamType attackerTeam, string victimTeam)
    {
        if (targets == null || targets.Count == 0)
        {
            Debug.LogWarning("[SiegeChargeHandler] no targets for damage distribution.");
            return;
        }

        float perUnitDamage = totalDamage / targets.Count;
        var hitData = new DamageData { Damage = perUnitDamage, AttackerTeam = attackerTeam, HitPoint = Vector2.zero, IsPiercing = _penetration >= 1f };

        Debug.Log($"[Damage] {victimTeam} team 받음 | 총 데미지: {totalDamage} | 개별 데미지: {perUnitDamage} | 유닛 수: {targets.Count}");

        int damageCount = 0;
        foreach (Unit unit in targets)
        {
            if (unit == null || unit.IsDead) continue;
            unit.TakeDamage(hitData);
            damageCount++;
        }

        Debug.Log($"[Damage] {damageCount}개 유닛에 데미지 적용 완료");
    }

    public void RefreshCollisionPowerUI()
    {
        float playerCP = _grid != null ? _grid.CalculateTotalCollisionPower() : 0f;
        float enemyCP = _enemyGrid != null ? _enemyGrid.CalculateTotalCollisionPower() : 0f;
        EventBus.Instance?.Publish(new CollisionPowerUpdatedEvent { PlayerCP = playerCP, EnemyCP = enemyCP });
    }

    private void ApplyStunToAllEnemies(float duration)
    {
        List<Unit> targets = _enemyGrid != null ? _enemyGrid.GetAllLivingUnits() : null;
        if (targets == null || targets.Count == 0)
        {
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            Unit target = targets[i];
            if (target == null || target.IsDead)
            {
                continue;
            }

            if (_enemyStunRoutines.TryGetValue(target, out Coroutine existing) && existing != null)
            {
                StopCoroutine(existing);
            }

            _enemyStunRoutines[target] = StartCoroutine(StunRoutine(target, duration));
        }
    }

    private IEnumerator StunRoutine(Unit target, float duration)
    {
        if (target == null || target.IsDead)
        {
            yield break;
        }

        target.ChangeState(UnitState.Stun);
        yield return new WaitForSeconds(duration);

        if (target != null && !target.IsDead && target.CurrentState == UnitState.Stun)
        {
            target.ChangeState(UnitState.Idle);
        }

        if (target != null)
        {
            _enemyStunRoutines.Remove(target);
        }
    }
}

