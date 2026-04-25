using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

// ================================================================
// 그리드 돌진 연출 + 데미지 계산
// 시퀀스:
//   1. 왼쪽으로 살짝 당김 (사전 모션)
//   2. 오른쪽으로 강하게 돌진
//   3-A. 돌진 중 Enemy 레이어에 닿으면 즉시 정지 → 데미지 → 흔들림 → 복귀
//   3-B. 끝까지 돌진 시 도착점에서 데미지 → 흔들림 → 복귀
//   4. 원위치로 복귀
// DOTween Transform 이동 특성상 OnTriggerEnter2D가 불안정하므로
// Update()에서 Physics2D.OverlapCollider 폴링으로 충돌 감지
// ================================================================
public class GridCrashController : MonoBehaviour
{
    //참조
    [Header("References")]
    [SerializeField] private GridManager _grid;
    [SerializeField] private GridChargeSystem _chargeSystem;

    [Tooltip("돌진 중 충돌 감지용 Collider2D (IsTrigger = true 권장)")]
    [SerializeField] private Collider2D _crashCollider;

    [Tooltip("적으로 판정할 레이어")]
    [SerializeField] private LayerMask _enemyLayer;

    //돌진 연출 파라미터
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
    [SerializeField] private int _shakeVibrato = 15;
    [SerializeField, Range(0f, 90f)] private float _shakeRandomness = 90f;

    [Tooltip("복귀 시간")]
    [SerializeField] private float _returnDuration = 2.5f;

    //이벤트
    [Header("Events")]
    [Tooltip("충돌 데미지 발생 시 호출 (데미지 전달)")]
    public UnityEvent<int> OnCrashImpact;

    [Tooltip("돌진 시퀀스 시작 시 → 버튼 비활성화 연결")]
    public UnityEvent OnCrashStart;

    [Tooltip("돌진 시퀀스 종료 시 (복귀 완료) → 게이지 다 찼을 때 버튼 활성화는 GridChargeSystem.OnGaugeFull 사용")]
    public UnityEvent OnCrashEnd;

    //런타임 상태
    private Sequence _sequence;
    private Vector3 _startPosition;
    private bool _isCrashing;
    private bool _isDashing;                    //돌진(전진) 구간에서만 true
    private bool _hasImpactedThisCrash;         //돌진 1회당 데미지 1회 제한
    private bool _suppressEndCrashOnKill;       //시퀀스를 직접 Kill 할 때 EndCrash 중복 호출 방지

    private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();

    public bool IsCrashing => _isCrashing;

    //Unity 생명주기
    private void Awake()
    {
        _startPosition = _grid.transform.position;
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
    }

    // 돌진 전진 구간에서만 적 충돌 폴링
    // DOTween Transform 이동은 OnTriggerEnter2D가 불안정하므로 직접 쿼리
    private void Update()
    {
        if (!_isDashing || _hasImpactedThisCrash || _crashCollider == null) return;

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask    = _enemyLayer,
            useTriggers  = true
        };
        float lookAhead = (_crashDistance / _crashDuration) * Time.deltaTime + 0.05f;
        var hits = new RaycastHit2D[1];
        int hitCount = _crashCollider.Cast(Vector2.right, filter, hits, lookAhead);

        if(hitCount > 0) OnEnemyHit();

        // _overlapBuffer.TutorialClear();

        // if (Physics2D.OverlapCollider(_crashCollider, filter, _overlapBuffer) > 0)
        // {
        //     OnEnemyHit();
        // }
    }

    //돌진 실행 — UI 버튼 OnClick에 연결
    public void TryCrash()
    {
        if (_isCrashing) return;
        if (_chargeSystem == null || !_chargeSystem.IsFull) return;

        _chargeSystem.Consume();
        PlayCrashSequence();
    }

    [ContextMenu("Play Crash Sequence")]
    public void PlayCrashSequence()
    {
        // 이전 시퀀스 정리 (EndCrash 중복 호출 방지)
        _suppressEndCrashOnKill = true;
        _sequence?.Kill();
        _suppressEndCrashOnKill = false;

        Vector3 leftTarget  = _startPosition + Vector3.left  * _pullBackDistance;
        Vector3 rightTarget = _startPosition + Vector3.right * _crashDistance;

        _isCrashing           = true;
        _isDashing            = false;
        _hasImpactedThisCrash = false;
        OnCrashStart?.Invoke();

        var gridTransform = _grid.transform;
        _sequence = DOTween.Sequence();

        // 1) 사전 모션: 왼쪽으로 당김
        _sequence.Append(gridTransform.DOMove(leftTarget, _pullBackDuration)
            .SetEase(Ease.OutCubic));

        // 2) 돌진 전진 시작 플래그 ON
        _sequence.AppendCallback(() => _isDashing = true);

        // 3) 돌진: 오른쪽으로 가속
        _sequence.Append(gridTransform.DOMove(rightTarget, _crashDuration)
            .SetEase(Ease.InExpo));

        // 4) 끝까지 갔을 때 (충돌 없이) → 플래그 OFF + 데미지
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

        // 7) 종료 콜백
        _sequence.OnComplete(EndCrash);
        _sequence.OnKill(() => { if (!_suppressEndCrashOnKill) EndCrash(); });
    }

    // 돌진 중 Enemy 레이어에 닿았을 때
    private void OnEnemyHit()
    {
        _hasImpactedThisCrash = true;
        _isDashing            = false;

        TriggerImpact();

        // 진행 중인 전진 시퀀스 중단 (EndCrash는 억제)
        _suppressEndCrashOnKill = true;
        _sequence?.Kill();
        _suppressEndCrashOnKill = false;

        // 현재 위치에서 흔들림 + 복귀로 전환
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
        if (!_isCrashing) return;   //중복 호출 방지
        _isCrashing = false;
        _isDashing  = false;
        OnCrashEnd?.Invoke();
    }

    // 현재 그리드 가장 오른쪽 열 Attack 유닛들의 공격력 합산
    private void TriggerImpact()
    {
        int damage = _grid.CalculateRightmostColumnDamage();
        Debug.Log($"[Crash] 충돌 데미지: {damage}");
        OnCrashImpact?.Invoke(damage);
    }
}
