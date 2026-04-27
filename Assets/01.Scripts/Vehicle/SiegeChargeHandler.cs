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

    // 충돌로 인해 Sequence를 중단했을 때 OnKill에서 EndCrash가 조기 호출되는 것을 방지
    private bool _impactInterrupted;

    private readonly Dictionary<Unit, Coroutine> _enemyStunRoutines = new Dictionary<Unit, Coroutine>();
    private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>(32);
    private ContactFilter2D _enemyFilter;

    public bool IsCrashing => _isCrashing;

    private void Awake()
    {
        if (_grid != null) _startPosition = _grid.transform.position;

        _enemyFilter = new ContactFilter2D();
        _enemyFilter.SetLayerMask(_enemyLayer);
        _enemyFilter.useTriggers = true;
        _enemyFilter.useLayerMask = true;
    }

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<PlayerGridChangedEvent>(OnGridChanged);
        EventBus.Instance?.Subscribe<EnemyGridChangedEvent>(OnGridChanged);
        EventBus.Instance?.Subscribe<WaveStartedEvent>(OnWaveStarted); // 추가
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<PlayerGridChangedEvent>(OnGridChanged);
        EventBus.Instance?.Unsubscribe<EnemyGridChangedEvent>(OnGridChanged);
        EventBus.Instance?.Unsubscribe<WaveStartedEvent>(OnWaveStarted); // 추가
    }

    private void OnGridChanged<T>(T _) => RefreshCollisionPowerUI();

    private void OnDestroy()
    {
        CancelCrash();
    }

    // ==========================================
    // 매 프레임 적 콜라이더 폴링
    // 충돌 감지 → 현재 위치에서 이동 중단 후 충돌 연출 재생
    // ==========================================
    private void Update()
    {
        if (!_isDashing || _hasImpactedThisCrash) return;
        if (_crashCollider == null) return;

        _overlapBuffer.Clear();
        int count = _crashCollider.Overlap(_enemyFilter, _overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = _overlapBuffer[i];
            if (col == null) continue;

            // 적 투사체 무시
            if (col.GetComponentInParent<ProjectileBase>() != null) continue;

            // 아군 자신 무시
            if (col.transform.IsChildOf(_grid.transform)) continue;

            Debug.Log($"[SiegeChargeHandler] 충돌 감지: {col.gameObject.name}");
            OnEnemyHit();
            return;
        }
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

        _impactInterrupted = false; // 강제 취소는 정상 EndCrash 허용
        _sequence?.Kill();
        _sequence = null;

        _isCrashing = false;
        _isDashing = false;
        _hasImpactedThisCrash = false;

        if (_grid != null)
            _grid.transform.position = _startPosition;

        Debug.Log("[SiegeChargeHandler] Crash cancelled - all state reset");
    }

    public void SetDoctrineEnemyCollisionPowerReductionPercent(float percent)
    {
        _doctrineEnemyCollisionPowerReductionPercent = Mathf.Clamp01(percent);
    }

    public void SetDoctrineBonusDamagePercent(float percent)
    {
        _doctrineBonusDamagePercent = Mathf.Max(0f, percent);
    }

    public void SetDoctrineStunDurationSeconds(float seconds)
    {
        _doctrineStunDurationSeconds = Mathf.Max(0f, seconds);
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

    private void PlayCrashSequence()
    {
        Vector3 leftTarget  = _startPosition + Vector3.left  * _pullBackDistance;
        Vector3 rightTarget = _startPosition + Vector3.right * _crashDistance;

        _isCrashing           = true;
        _isDashing            = false;
        _hasImpactedThisCrash = false;
        _impactInterrupted    = false;

        RefreshCollisionPowerUI();

        var gridTransform = _grid.transform;
        _sequence = DOTween.Sequence();

        // 1단계: 후퇴
        _sequence.Append(gridTransform.DOMove(leftTarget, _pullBackDuration).SetEase(Ease.OutCubic));

        // 2단계: 돌진 (Update 폴링이 이 구간에서 적 감지)
        _sequence.AppendCallback(() => _isDashing = true);
        _sequence.Append(gridTransform.DOMove(rightTarget, _crashDuration).SetEase(Ease.InExpo));

        // 3단계: 적과 미충돌 시 끝까지 이동 후 처리
        _sequence.AppendCallback(() =>
        {
            _isDashing = false;
            if (!_hasImpactedThisCrash)
            {
                Debug.Log("[SiegeChargeHandler] 목표 지점까지 도달 - 충돌 없음, 기본 처리");
                TriggerImpact();
                PlayReturnSequenceFromCurrentPosition();
            }
        });

        // OnKill: 충돌 인터럽트가 아닐 때만 EndCrash
        _sequence.OnComplete(() => { /* AppendCallback에서 처리 */ });
        _sequence.OnKill(() => { if (!_impactInterrupted) EndCrash(); });
    }

    // ==========================================
    // 충돌 감지 → 현재 위치에서 즉시 정지 → shake → 복귀
    // ==========================================
    private void OnEnemyHit()
    {
        if (_hasImpactedThisCrash) return;

        _hasImpactedThisCrash = true;
        _isDashing            = false;
        _impactInterrupted    = true; // Kill 시 EndCrash 조기 호출 방지

        // 현재 위치(충돌 지점) 기록
        Vector3 impactPosition = _grid.transform.position;
        Debug.Log($"[SiegeChargeHandler] 충돌 지점 정지: {impactPosition}");

        // 돌진 시퀀스 중단 (OnKill에서 EndCrash 안 부름)
        _sequence?.Kill();
        _sequence = null;

        // 데미지 계산
        TriggerImpact();

        // 충돌 지점에서 shake → 복귀
        PlayReturnSequenceFromCurrentPosition();
    }

    // 현재 위치 기준으로 shake + 복귀 시퀀스를 생성
    private void PlayReturnSequenceFromCurrentPosition()
    {
        var gridTransform = _grid.transform;

        _sequence = DOTween.Sequence();
        _sequence.Append(
            gridTransform.DOShakePosition(
                _shakeDuration,
                new Vector3(_shakeStrength, _shakeStrength, 0f),
                _shakeVibrato,
                _shakeRandomness,
                false,
                true));
        _sequence.Append(
            gridTransform.DOMove(_startPosition, _returnDuration).SetEase(Ease.InQuad));

        _sequence.OnComplete(EndCrash);
        _sequence.OnKill(EndCrash);
    }

    private void EndCrash()
    {
        if (!_isCrashing) return;
        _isCrashing        = false;
        _isDashing         = false;
        _impactInterrupted = false;
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
        float enemyCP  = _enemyGrid.CalculateTotalCollisionPower();
        enemyCP *= 1f - _doctrineEnemyCollisionPowerReductionPercent;

        ResolveCollision(playerCP, enemyCP, _enemyGrid);
    }

    private void ResolveCollision(float playerCP, float enemyCP, EnemyGridManager enemyGrid)
    {
        float delta         = Mathf.Abs(playerCP - enemyCP);
        bool  isPlayerLosing = playerCP < enemyCP;

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
            ApplyStunToAllEnemies(_doctrineStunDurationSeconds);

        EventBus.Instance?.Publish(new SiegeCollisionResolvedEvent
        {
            PlayerCP     = playerCP,
            EnemyCP      = enemyCP,
            Delta        = delta,
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

        float perUnitDamage = totalDamage / targets.Count;
        var hitData = new DamageData
        {
            Damage       = perUnitDamage,
            AttackerTeam = attackerTeam,
            HitPoint     = Vector2.zero,
            IsPiercing   = _penetration >= 1f
        };

        Debug.Log($"[Damage] {victimTeam} team | 총 데미지: {totalDamage} | 개별: {perUnitDamage} | 유닛 수: {targets.Count}");

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

        if (_enemyGrid == null)
            _enemyGrid = ResolveEnemyGrid();

        float enemyCP = _enemyGrid != null ? _enemyGrid.CalculateTotalCollisionPower() : 0f;

        Debug.Log($"[SiegeChargeHandler] CP 갱신: Player={playerCP}, Enemy={enemyCP}");
        EventBus.Instance?.Publish(new CollisionPowerUpdatedEvent { PlayerCP = playerCP, EnemyCP = enemyCP });
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

    private void OnWaveStarted(WaveStartedEvent evt)
    {
        RefreshCollisionPowerUI();
    }
}

