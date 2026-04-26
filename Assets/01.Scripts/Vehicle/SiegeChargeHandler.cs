using System;
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

    public event Action OnCrashEnd;

    private Sequence _sequence;
    private Vector3 _startPosition;
    private EnemyGridManager _enemyGrid;
    private bool _isCrashing;
    private bool _isDashing;
    private bool _hasImpactedThisCrash;

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
        
        if (_enemyGridObject != null)
            _enemyGrid = _enemyGridObject.GetComponentInChildren<EnemyGridManager>();
        
        if (_enemyGrid == null)
        {
            Debug.LogWarning("[SiegeChargeHandler] EnemyGridManager not found, crash cancelled.");
            return;
        }
        
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
        if (_enemyGrid == null)
        {
            Debug.LogWarning("[SiegeChargeHandler] EnemyGridManager not found, skipping damage.");
            return;
        }

        float playerCP = _grid != null ? _grid.CalculateTotalCollisionPower() : 0f;
        float enemyCP = _enemyGrid.CalculateTotalCollisionPower();
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
                DistributeDamage(targets, delta, TeamType.Player, "Enemy");
            }
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
}
