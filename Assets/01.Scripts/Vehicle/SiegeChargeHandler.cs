using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 아군 요새 돌진 시스템 통합 핸들러
/// [역할]
/// 1. 돌진 게이지 충전 (구 GridChargeSystem)
/// 2. DOTween 기반 돌진 연출 (구 GridCrashController)
/// 3. CollisionPower 기반 데미지 분배 (구 SiegeChargeHandler)
/// </summary>
/// <remarks>
/// [시퀀스]
///   1. 사전 모션: 왼쪽으로 당김
///   2. 돌진: 오른쪽으로 가속
///   3-A. 돌진 중 Enemy 레이어 감지 → 즉시 정지 → CP 데미지 → 흔들림 → 복귀
///   3-B. 끝까지 도달 시 → CP 데미지 → 흔들림 → 복귀
///   4. 복귀 완료 후 게이지 재충전 시작
/// </remarks>
public class SiegeChargeHandler : MonoBehaviour
{
    // ──────────────────────────────────────────────
    // 인스펙터
    // ──────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private GridManager _grid;

    [Tooltip("돌진 중 충돌 감지용 Collider2D (IsTrigger = true 권장)")]
    [SerializeField] private Collider2D _crashCollider;

    [Tooltip("적으로 판정할 레이어")]
    [SerializeField] private LayerMask _enemyLayer;

    [Header("Gauge Settings")]
    [SerializeField, Min(1f)] private float _maxGauge = 100f;
    [SerializeField, Min(0f)] private float _chargeGaugePower = 1f;

    [Header("Crash Motion")]
    [Tooltip("사전 모션: 왼쪽으로 당기는 거리")]
    [SerializeField] private float _pullBackDistance = 1f;
    [SerializeField] private float _pullBackDuration = 0.8f;

    [Tooltip("돌진: 오른쪽으로 박는 거리")]
    [SerializeField] private float _crashDistance = 10f;
    [SerializeField] private float _crashDuration = 1f;

    [Header("Shake")]
    [SerializeField] private float _shakeStrength = 0.15f;
    [SerializeField] private float _shakeDuration = 0.3f;
    [SerializeField] private int   _shakeVibrato = 15;
    [SerializeField, Range(0f, 90f)] private float _shakeRandomness = 90f;

    [SerializeField] private float _returnDuration = 2.5f;

    [Header("Damage Settings")]
    [Tooltip("데미지 분배 시 방어율을 무시하는 비율 (1 = 완전 무시)")]
    [Range(0f, 1f)]
    [SerializeField] private float _penetration = 0f;

    [Header("Events")]
    [Tooltip("게이지가 가득 찬 순간 1회 호출 (버튼 활성화)")]
    public UnityEvent OnGaugeFull;

    [Tooltip("게이지 값이 변할 때마다 호출 (0~1 정규화 값, UI 프로그레스 바용)")]
    public UnityEvent<float> OnGaugeChanged;

    [Tooltip("돌진 시퀀스 시작 시 (버튼 비활성화 연결)")]
    public UnityEvent OnCrashStart;

    [Tooltip("돌진 시퀀스 종료 시 (복귀 완료)")]
    public UnityEvent OnCrashEnd;

    // ──────────────────────────────────────────────
    // 런타임 상태
    // ──────────────────────────────────────────────

    // 게이지
    private float _currentGauge;
    private bool  _isGaugeFull;

    // 연출
    private Sequence _sequence;
    private Vector3  _startPosition;
    private bool     _isCrashing;
    private bool     _isDashing;
    private bool     _hasImpactedThisCrash;
    private bool     _suppressEndCrashOnKill;

    // ──────────────────────────────────────────────
    // 공개 프로퍼티
    // ──────────────────────────────────────────────

    public float GaugeNormalized  => _currentGauge / _maxGauge;
    public bool  IsGaugeFull      => _isGaugeFull;
    public bool  IsCrashing       => _isCrashing;

    public float ChargeGaugePower
    {
        get => _chargeGaugePower;
        set => _chargeGaugePower = Mathf.Max(0f, value);
    }

    // ──────────────────────────────────────────────
    // Unity 생명주기
    // ──────────────────────────────────────────────

    private void Awake()
    {
        if (_grid != null)
            _startPosition = _grid.transform.position;
    }

    private void Update()
    {
        TickGauge();
        PollCrashCollision();
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
    }

    // ──────────────────────────────────────────────
    // 게이지 충전 (구 GridChargeSystem)
    // ──────────────────────────────────────────────

    private void TickGauge()
    {
        if (_isGaugeFull || _isCrashing) return;

        _currentGauge += _chargeGaugePower * Time.deltaTime;

        if (_currentGauge >= _maxGauge)
        {
            _currentGauge = _maxGauge;
            _isGaugeFull  = true;
            OnGaugeChanged?.Invoke(1f);
            OnGaugeFull?.Invoke();
            return;
        }

        OnGaugeChanged?.Invoke(GaugeNormalized);
    }

    private void ConsumeGauge()
    {
        _currentGauge = 0f;
        _isGaugeFull  = false;
        OnGaugeChanged?.Invoke(0f);
    }

    /// <summary>디버그/치트용: 즉시 게이지 충전</summary>
    [ContextMenu("Fill Gauge")]
    public void FillGaugeImmediately()
    {
        _currentGauge = _maxGauge;
        _isGaugeFull  = true;
        OnGaugeFull?.Invoke();
        OnGaugeChanged?.Invoke(1f);
    }

    // ──────────────────────────────────────────────
    // 돌진 진입점 (UI 버튼 → OnClick 연결)
    // ──────────────────────────────────────────────

    public void TryCrash()
    {
        if (_isCrashing) return;
        if (!_isGaugeFull) return;

        ConsumeGauge();
        PlayCrashSequence();
    }

    // ──────────────────────────────────────────────
    // 돌진 연출 (구 GridCrashController)
    // ──────────────────────────────────────────────

    [ContextMenu("Play Crash Sequence")]
    public void PlayCrashSequence()
    {
        _suppressEndCrashOnKill = true;
        _sequence?.Kill();
        _suppressEndCrashOnKill = false;

        Vector3 leftTarget  = _startPosition + Vector3.left  * _pullBackDistance;
        Vector3 rightTarget = _startPosition + Vector3.right * _crashDistance;

        _isCrashing           = true;
        _isDashing            = false;
        _hasImpactedThisCrash = false;
        OnCrashStart?.Invoke();

        // CP 수치 UI 갱신 (연출 시작 시점에 최신값 반영)
        RefreshCollisionPowerUI();

        var gridTransform = _grid.transform;
        _sequence = DOTween.Sequence();

        // 1) 사전 모션: 왼쪽으로 당김
        _sequence.Append(gridTransform.DOMove(leftTarget, _pullBackDuration)
            .SetEase(Ease.OutCubic));

        // 2) 돌진 전진 플래그 ON
        _sequence.AppendCallback(() => _isDashing = true);

        // 3) 돌진
        _sequence.Append(gridTransform.DOMove(rightTarget, _crashDuration)
            .SetEase(Ease.InExpo));

        // 4) 끝까지 도달 (충돌 없이) → 플래그 OFF + 데미지
        _sequence.AppendCallback(() =>
        {
            _isDashing = false;
            if (!_hasImpactedThisCrash) TriggerImpact();
        });

        // 5) 흔들림
        _sequence.Append(gridTransform.DOShakePosition(
            _shakeDuration,
            new Vector3(_shakeStrength, _shakeStrength, 0f),
            _shakeVibrato, _shakeRandomness, false, true));

        // 6) 복귀
        _sequence.Append(gridTransform.DOMove(_startPosition, _returnDuration)
            .SetEase(Ease.InQuad));

        // 7) 종료
        _sequence.OnComplete(EndCrash);
        _sequence.OnKill(() => { if (!_suppressEndCrashOnKill) EndCrash(); });
    }

    // ──────────────────────────────────────────────
    // 충돌 감지 폴링 (구 GridCrashController.Update)
    // DOTween 이동 중 OnTriggerEnter2D가 불안정하므로 직접 쿼리
    // ──────────────────────────────────────────────

    private void PollCrashCollision()
    {
        if (!_isDashing || _hasImpactedThisCrash || _crashCollider == null) return;

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask    = _enemyLayer,
            useTriggers  = true
        };

        float lookAhead = (_crashDistance / _crashDuration) * Time.deltaTime + 0.05f;
        var   hits      = new RaycastHit2D[1];
        int   hitCount  = _crashCollider.Cast(Vector2.right, filter, hits, lookAhead);

        if (hitCount > 0) OnEnemyHit();
    }

    private void OnEnemyHit()
    {
        _hasImpactedThisCrash = true;
        _isDashing            = false;

        TriggerImpact();

        _suppressEndCrashOnKill = true;
        _sequence?.Kill();
        _suppressEndCrashOnKill = false;

        var gridTransform = _grid.transform;
        _sequence = DOTween.Sequence();

        _sequence.Append(gridTransform.DOShakePosition(
            _shakeDuration,
            new Vector3(_shakeStrength, 0f, 0f),
            _shakeVibrato, _shakeRandomness, false, true));

        _sequence.Append(gridTransform.DOMove(_startPosition, _returnDuration)
            .SetEase(Ease.InQuad));

        _sequence.OnComplete(EndCrash);
        _sequence.OnKill(() => { if (!_suppressEndCrashOnKill) EndCrash(); });
    }

    private void EndCrash()
    {
        if (!_isCrashing) return;
        _isCrashing = false;
        _isDashing  = false;
        OnCrashEnd?.Invoke();
    }

    // ──────────────────────────────────────────────
    // CP 계산 + 데미지 분배 (구 SiegeChargeHandler)
    // ──────────────────────────────────────────────

    private void TriggerImpact()
    {
        EnemyGridManager enemyGrid = GetEnemyGridManager();
        if (enemyGrid == null)
        {
            Debug.LogWarning("[SiegeChargeHandler] EnemyGridManager를 찾을 수 없어 데미지를 건너뜁니다.");
            return;
        }

        float playerCP = _grid != null ? _grid.CalculateTotalCollisionPower() : 0f;
        float enemyCP  = enemyGrid.CalculateTotalCollisionPower();

        ResolveCollision(playerCP, enemyCP, enemyGrid);
    }

    private void ResolveCollision(float playerCP, float enemyCP, EnemyGridManager enemyGrid)
    {
        float delta         = Mathf.Abs(playerCP - enemyCP);
        bool  isPlayerLosing = playerCP < enemyCP;

        Debug.Log($"[SiegeChargeHandler] 충돌 | 아군CP: {playerCP} / 적CP: {enemyCP} / delta: {delta} / 낮은쪽: {(isPlayerLosing ? "아군" : "적군")}");

        if (delta > 0f)
        {
            if (isPlayerLosing)
                DistributeDamage(_grid.GetAllLivingUnits(), delta, TeamType.Enemy);
            else
                DistributeDamage(enemyGrid.GetAllLivingUnits(), delta, TeamType.Player);
        }

        EventBus.Instance?.Publish(new SiegeCollisionResolvedEvent
        {
            PlayerCP       = playerCP,
            EnemyCP        = enemyCP,
            Delta          = delta,
            IsPlayerLosing = isPlayerLosing
        });
    }

    private void DistributeDamage(List<Unit> targets, float totalDamage, TeamType attackerTeam)
    {
        if (targets == null || targets.Count == 0)
        {
            Debug.LogWarning("[SiegeChargeHandler] 데미지 대상 유닛이 없습니다.");
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

        foreach (Unit unit in targets)
        {
            if (unit == null || unit.IsDead) continue;
            unit.TakeDamage(hitData);
        }

        Debug.Log($"[SiegeChargeHandler] 데미지 분배 완료 | 총 {totalDamage} / {targets.Count}유닛 = 1인당 {perUnitDamage:F1}");
    }

    /// <summary>
    /// Prepare 페이즈에서 유닛 배치 변경 시 호출하면 UI 수치가 갱신됩니다.
    /// </summary>
    public void RefreshCollisionPowerUI()
    {
        EnemyGridManager enemyGrid = GetEnemyGridManager();
        float playerCP = _grid != null ? _grid.CalculateTotalCollisionPower() : 0f;
        float enemyCP  = enemyGrid != null ? enemyGrid.CalculateTotalCollisionPower() : 0f;

        EventBus.Instance?.Publish(new CollisionPowerUpdatedEvent
        {
            PlayerCP = playerCP,
            EnemyCP  = enemyCP
        });
    }

    // ──────────────────────────────────────────────
    // 헬퍼
    // ──────────────────────────────────────────────

    private EnemyGridManager GetEnemyGridManager()
    {
        if (StageManager.Instance == null) return null;
        var layout = StageManager.Instance.CurrentLayout;
        if (layout == null || layout.CurrentEnemySiege == null) return null;
        return layout.CurrentEnemySiege.GetComponent<EnemyGridManager>();
    }
}
