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
    [SerializeField, Range(0f, 1f)] private float _attackCategoryDamageMultiplier = 0.3f;

    [Header("Doctrine - Ram")]
    [SerializeField, Range(0f, 1f)] private float _doctrineEnemyCollisionPowerReductionPercent = 0f;
    [SerializeField, Min(0f)] private float _doctrineBonusDamagePercent = 0f;
    [SerializeField, Min(0f)] private float _doctrineStunDurationSeconds = 0f;

    public event Action OnCrashEnd;

    private Sequence _sequence;
    private Vector3 _startPosition;
    private EnemyGridManager _enemyGrid;
    private bool _isCrashing;

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
        EventBus.Instance?.Subscribe<WaveStartedEvent>(OnWaveStarted);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<PlayerGridChangedEvent>(OnGridChanged);
        EventBus.Instance?.Unsubscribe<EnemyGridChangedEvent>(OnGridChanged);
        EventBus.Instance?.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
    }

    private void OnGridChanged<T>(T _) => RefreshCollisionPowerUI();

    private void OnDestroy()
    {
        _sequence?.Kill();
    }

    // ─────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────

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

    public void SetDoctrineEnemyCollisionPowerReductionPercent(float percent) =>
        _doctrineEnemyCollisionPowerReductionPercent = Mathf.Clamp01(percent);

    public void SetDoctrineBonusDamagePercent(float percent) =>
        _doctrineBonusDamagePercent = Mathf.Max(0f, percent);

    public void SetDoctrineStunDurationSeconds(float seconds) =>
        _doctrineStunDurationSeconds = Mathf.Max(0f, seconds);

    // ─────────────────────────────────────────────
    // Sequence: 후퇴 → 돌진(고정 거리) → 데미지 → 쉐이크 → 복귀
    // 중간 중단 없음, 무조건 완주
    // ─────────────────────────────────────────────

    private void PlayCrashSequence()
    {
        _isCrashing = true;
        RefreshCollisionPowerUI();

        var tf           = _grid.transform;
        Vector3 pullBack = _startPosition + Vector3.left  * _pullBackDistance;
        Vector3 crashEnd = _startPosition + Vector3.right * _crashDistance;

        _sequence = DOTween.Sequence()
            // 1. 후퇴
            .Append(tf.DOMove(pullBack, _pullBackDuration).SetEase(Ease.OutCubic))
            // 2. 돌진 (고정 거리 완주)
            .Append(tf.DOMove(crashEnd, _crashDuration).SetEase(Ease.InExpo))
            // 3. 충돌 지점 도달 → 데미지
            .AppendCallback(TriggerImpact)
            // 4. 쉐이크
            .Append(tf.DOShakePosition(
                _shakeDuration,
                new Vector3(_shakeStrength, _shakeStrength, 0f),
                _shakeVibrato,
                _shakeRandomness,
                false,
                true))
            // 5. 복귀
            .Append(tf.DOMove(_startPosition, _returnDuration).SetEase(Ease.InQuad))
            .OnComplete(EndCrash);
    }

    private void EndCrash()
    {
        _isCrashing = false;
        OnCrashEnd?.Invoke();
    }

    // ─────────────────────────────────────────────
    // 데미지 처리
    // ─────────────────────────────────────────────

    private void TriggerImpact()
    {
        CameraManager.Instance?.ShakeStrong();

        _enemyGrid = _enemyGrid != null ? _enemyGrid : ResolveEnemyGrid();
        if (_enemyGrid == null)
        {
            Debug.LogWarning("[SiegeChargeHandler] EnemyGridManager not found, skipping damage.");
            return;
        }

        _enemyGrid.RegisterExistingUnitsFromChildren();

        float playerCP = _grid != null ? _grid.CalculateTotalCollisionPower() : 0f;
        float enemyCP  = _enemyGrid.CalculateTotalCollisionPower() * (1f - _doctrineEnemyCollisionPowerReductionPercent);

        ResolveCollision(playerCP, enemyCP, _enemyGrid);
    }

    private void ResolveCollision(float playerCP, float enemyCP, EnemyGridManager enemyGrid)
    {
        float delta          = Mathf.Abs(playerCP - enemyCP);
        bool  isPlayerLosing = playerCP < enemyCP;

        Debug.Log($"[Collision] PlayerCP: {playerCP} | EnemyCP: {enemyCP} | Delta: {delta}");

        if (delta > 0f)
        {
            if (isPlayerLosing)
            {
                DistributeDamage(_grid.GetAllLivingUnits(), delta, TeamType.Enemy, "Player");
            }
            else
            {
                float boosted = delta * (1f + Mathf.Max(0f, _doctrineBonusDamagePercent));
                DistributeDamage(enemyGrid.GetAllLivingUnits(), boosted, TeamType.Player, "Enemy");
            }
        }

        if (_doctrineStunDurationSeconds > 0f)
            ApplyStunToAllEnemies(_doctrineStunDurationSeconds);

        EventBus.Instance?.Publish(new SiegeCollisionResolvedEvent
        {
            PlayerCP       = playerCP,
            EnemyCP        = enemyCP,
            Delta          = delta,
            IsPlayerLosing = isPlayerLosing
        });
    }

    private void DistributeDamage(List<Unit> targets, float totalDamage, TeamType attackerTeam, string victimTeam)
    {
        if (targets == null || targets.Count == 0)
        {
            Debug.LogWarning("[SiegeChargeHandler] no targets for damage distribution.");
            return;
        }

        float baseDamagePerUnit = totalDamage * 1.5f;

        int damageCount = 0;
        foreach (Unit unit in targets)
        {
            if (unit == null || unit.IsDead) continue;

            float finalDamage = baseDamagePerUnit;
            if (unit.Data != null && unit.Data.Category == UnitCategory.Attack)
                finalDamage *= _attackCategoryDamageMultiplier;

            var hitData = new DamageData
            {
                Damage       = finalDamage,
                AttackerTeam = attackerTeam,
                HitPoint     = Vector2.zero,
                IsPiercing   = _penetration >= 1f
            };

            unit.TakeDamage(hitData);
            damageCount++;
        }

        Debug.Log($"[Damage] {victimTeam} | 총 데미지: {totalDamage} | {damageCount}개 유닛 적용");
    }

    // ─────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────

    public void RefreshCollisionPowerUI()
    {
        float playerCP = _grid != null ? _grid.CalculateTotalCollisionPower() : 0f;

        if (_enemyGrid == null) _enemyGrid = ResolveEnemyGrid();

        float enemyCP = _enemyGrid != null ? _enemyGrid.CalculateTotalCollisionPower() : 0f;

        EventBus.Instance?.Publish(new CollisionPowerUpdatedEvent { PlayerCP = playerCP, EnemyCP = enemyCP });
    }

    private EnemyGridManager ResolveEnemyGrid()
    {
        EnemyGridManager resolved = null;

        if (_enemyGridObject != null)
            resolved = _enemyGridObject.GetComponentInChildren<EnemyGridManager>(true);

        if (resolved == null)
            resolved = FindAnyObjectByType<EnemyGridManager>();

        return resolved;
    }

    private void ApplyStunToAllEnemies(float duration)
    {
        List<Unit> targets = _enemyGrid != null ? _enemyGrid.GetAllLivingUnits() : null;
        if (targets == null || targets.Count == 0) return;

        foreach (Unit target in targets)
        {
            if (target == null || target.IsDead) continue;

            if (_enemyStunRoutines.TryGetValue(target, out Coroutine existing) && existing != null)
                StopCoroutine(existing);

            _enemyStunRoutines[target] = StartCoroutine(StunRoutine(target, duration));
        }
    }

    private IEnumerator StunRoutine(Unit target, float duration)
    {
        if (target == null || target.IsDead) yield break;

        target.ChangeState(UnitState.Stun);
        yield return new WaitForSeconds(duration);

        if (target != null && !target.IsDead && target.CurrentState == UnitState.Stun)
            target.ChangeState(UnitState.Idle);

        if (target != null)
            _enemyStunRoutines.Remove(target);
    }

    private void OnWaveStarted(WaveStartedEvent evt) => RefreshCollisionPowerUI();
}

