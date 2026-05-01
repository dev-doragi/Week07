using TMPro;
using DG.Tweening;
using UnityEngine;

public class CollisionPowerUIController : MonoBehaviour
{
    [Header("Collision Power Text")]
    [SerializeField] private TextMeshProUGUI _playerCPText;
    [SerializeField] private TextMeshProUGUI _enemyCPText;

    [Header("Calculation Animation")]
    [SerializeField] private RectTransform _playerCalculationPoint;
    [SerializeField] private RectTransform _enemyCalculationPoint;
    [SerializeField] private GameObject _subtractImage;
    [SerializeField, Min(0f)] private float _gatherDuration = 0.35f;
    [SerializeField, Min(0f)] private float _scaleDuration = 0.2f;
    [SerializeField] private float _calculationScale = 1.5f;

    [Header("Collision Impact Animation")]
    [SerializeField] private GameObject _emphasisImage;
    [SerializeField] private RectTransform _impactTargetPoint;
    [SerializeField, Min(0f)] private float _clashStartDelay = 0.15f;
    [SerializeField, Min(0.05f)] private float _minClashMoveDuration = 0.2f;
    [SerializeField, Min(0f)] private float _loserFlyDuration = 0.65f;
    [SerializeField, Min(0f)] private float _loserFlyDistance = 1400f;
    [SerializeField] private float _loserSpinDegrees = 540f;
    [SerializeField] private float _winnerImpactScale = 1.2f;
    [SerializeField, Min(0f)] private float _impactShakeDuration = 0.15f;

    [Header("Damage Result Display")]
    [SerializeField] private RectTransform _damageResultRoot;
    [SerializeField] private TextMeshProUGUI _damageResultText;
    [SerializeField] private RectTransform _damageResultPoint;
    [SerializeField] private RectTransform _ourDamageResultPoint;
    [SerializeField, Min(0f)] private float _damageResultDuration = 1.2f;
    [SerializeField, Min(0f)] private float _damageResultPopDuration = 0.2f;
    [SerializeField] private float _damageResultPopScale = 1.15f;

    private Sequence _calculationSequence;
    private Sequence _impactSequence;
    private Sequence _damageResultSequence;
    private RectTransform _playerCPRect;
    private RectTransform _enemyCPRect;
    private Canvas _canvas;
    private Vector2 _playerOriginalPosition;
    private Vector2 _enemyOriginalPosition;
    private Vector2 _damageResultOriginalPosition;
    private Vector3 _playerOriginalScale;
    private Vector3 _enemyOriginalScale;
    private Vector3 _damageResultOriginalScale;
    private Vector3 _playerOriginalRotation;
    private Vector3 _enemyOriginalRotation;
    private Vector3 _damageResultOriginalRotation;
    private bool _playerOriginalActive;
    private bool _enemyOriginalActive;
    private bool _hasOriginalState;
    private bool _isStageEnding;

    private void Awake()
    {
        CacheReferences();
        CacheOriginalState();
        ResetAnimationState();
    }

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<CollisionPowerUpdatedEvent>(OnCollisionPowerUpdated);
        EventBus.Instance?.Subscribe<SiegeChargeStartedEvent>(OnSiegeChargeStarted);
        EventBus.Instance?.Subscribe<SiegeChargeEndedEvent>(OnSiegeChargeEnded);
        EventBus.Instance?.Subscribe<SiegeImpactStartedEvent>(OnSiegeImpactStarted);
        EventBus.Instance?.Subscribe<SiegeCollisionResolvedEvent>(OnSiegeCollisionResolved);
        EventBus.Instance?.Subscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance?.Subscribe<WaveEndedEvent>(OnWaveEnded);
        EventBus.Instance?.Subscribe<StageClearedEvent>(OnStageCleared);
        EventBus.Instance?.Subscribe<StageCleanedUpEvent>(OnStageCleanedUp);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<CollisionPowerUpdatedEvent>(OnCollisionPowerUpdated);
        EventBus.Instance?.Unsubscribe<SiegeChargeStartedEvent>(OnSiegeChargeStarted);
        EventBus.Instance?.Unsubscribe<SiegeChargeEndedEvent>(OnSiegeChargeEnded);
        EventBus.Instance?.Unsubscribe<SiegeImpactStartedEvent>(OnSiegeImpactStarted);
        EventBus.Instance?.Unsubscribe<SiegeCollisionResolvedEvent>(OnSiegeCollisionResolved);
        EventBus.Instance?.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance?.Unsubscribe<WaveEndedEvent>(OnWaveEnded);
        EventBus.Instance?.Unsubscribe<StageClearedEvent>(OnStageCleared);
        EventBus.Instance?.Unsubscribe<StageCleanedUpEvent>(OnStageCleanedUp);

        ResetAnimationState();
    }

    private void OnDestroy()
    {
        ResetAnimationState();
    }

    private void OnCollisionPowerUpdated(CollisionPowerUpdatedEvent e)
    {
        if (_playerCPText != null)
            _playerCPText.text = $"{e.PlayerCP:F1}";

        if (_enemyCPText != null)
            _enemyCPText.text = $"{e.EnemyCP:F1}";
    }

    private void OnSiegeChargeStarted(SiegeChargeStartedEvent _)
    {
        if (_isStageEnding) return;
        PlayCalculationAnimation();
    }

    private void OnSiegeImpactStarted(SiegeImpactStartedEvent e)
    {
        if (_isStageEnding) return;
        PlayImpactAnimation(e);
    }

    private void OnSiegeChargeEnded(SiegeChargeEndedEvent _)
    {
        ResetAnimationState();
    }

    private void OnSiegeCollisionResolved(SiegeCollisionResolvedEvent e)
    {
        if (_isStageEnding) return;
        PlayDamageResultDisplay(e);
    }

    private void OnWaveStarted(WaveStartedEvent _)
    {
        _isStageEnding = false;
    }

    private void OnWaveEnded(WaveEndedEvent e)
    {
        if (e.IsWin)
            _isStageEnding = true;

        ResetAnimationState();
    }

    private void OnStageCleared(StageClearedEvent _)
    {
        _isStageEnding = true;
        ResetAnimationState();
    }

    private void OnStageCleanedUp(StageCleanedUpEvent _)
    {
        _isStageEnding = false;
        ResetAnimationState();
    }

    private void PlayCalculationAnimation()
    {
        CacheOriginalState();
        KillSequences();
        ResetVisualObjects();

        if (_playerCPRect == null || _enemyCPRect == null) return;

        Vector2 playerTarget = GetTargetAnchoredPosition(_playerCPRect, _playerCalculationPoint, new Vector2(-120f, 250f));
        Vector2 enemyTarget = GetTargetAnchoredPosition(_enemyCPRect, _enemyCalculationPoint, new Vector2(120f, 250f));

        _calculationSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        _calculationSequence
            .Join(_playerCPRect.DOAnchorPos(playerTarget, _gatherDuration).SetEase(Ease.OutCubic))
            .Join(_enemyCPRect.DOAnchorPos(enemyTarget, _gatherDuration).SetEase(Ease.OutCubic))
            .Join(_playerCPRect.DOScale(_playerOriginalScale * _calculationScale, _scaleDuration).SetEase(Ease.OutBack))
            .Join(_enemyCPRect.DOScale(_enemyOriginalScale * _calculationScale, _scaleDuration).SetEase(Ease.OutBack));
    }

    private void PlayImpactAnimation(SiegeImpactStartedEvent e)
    {
        if (_playerCPRect == null || _enemyCPRect == null) return;

        _impactSequence?.Kill();

        Vector2 playerClashPoint = GetClashAnchoredPosition(_playerCPRect);
        Vector2 enemyClashPoint = GetClashAnchoredPosition(_enemyCPRect);
        float moveStartDelay = Mathf.Min(
            Mathf.Max(0f, _gatherDuration + _clashStartDelay),
            Mathf.Max(0f, e.DelayUntilImpact - _minClashMoveDuration));
        float clashMoveDuration = Mathf.Max(_minClashMoveDuration, e.DelayUntilImpact - moveStartDelay);

        _impactSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        _impactSequence
            .AppendInterval(moveStartDelay)
            .Append(_playerCPRect.DOAnchorPos(playerClashPoint, clashMoveDuration).SetEase(Ease.InExpo))
            .Join(_enemyCPRect.DOAnchorPos(enemyClashPoint, clashMoveDuration).SetEase(Ease.InExpo));

        AppendPostImpactAnimation(e, playerClashPoint, enemyClashPoint);
    }

    private void CacheReferences()
    {
        if (_playerCPText != null)
            _playerCPRect = _playerCPText.rectTransform;

        if (_enemyCPText != null)
            _enemyCPRect = _enemyCPText.rectTransform;

        if (_damageResultRoot == null && _damageResultText != null)
            _damageResultRoot = _damageResultText.rectTransform;

        _canvas = GetComponentInParent<Canvas>();
    }

    private void CacheOriginalState()
    {
        CacheReferences();

        if (_hasOriginalState) return;

        if (_playerCPRect != null)
        {
            _playerOriginalPosition = _playerCPRect.anchoredPosition;
            _playerOriginalScale = _playerCPRect.localScale;
            _playerOriginalRotation = _playerCPRect.localEulerAngles;
            _playerOriginalActive = _playerCPRect.gameObject.activeSelf;
        }

        if (_enemyCPRect != null)
        {
            _enemyOriginalPosition = _enemyCPRect.anchoredPosition;
            _enemyOriginalScale = _enemyCPRect.localScale;
            _enemyOriginalRotation = _enemyCPRect.localEulerAngles;
            _enemyOriginalActive = _enemyCPRect.gameObject.activeSelf;
        }

        if (_damageResultRoot != null)
        {
            _damageResultOriginalPosition = _damageResultRoot.anchoredPosition;
            _damageResultOriginalScale = _damageResultRoot.localScale;
            _damageResultOriginalRotation = _damageResultRoot.localEulerAngles;
        }

        _hasOriginalState = true;
    }

    private void ResetAnimationState()
    {
        KillSequences();

        if (!_hasOriginalState)
            CacheOriginalState();

        if (_playerCPRect != null)
        {
            _playerCPRect.anchoredPosition = _playerOriginalPosition;
            _playerCPRect.localScale = _playerOriginalScale;
            _playerCPRect.localEulerAngles = _playerOriginalRotation;
            SetActive(_playerCPRect.gameObject, _playerOriginalActive);
        }

        if (_enemyCPRect != null)
        {
            _enemyCPRect.anchoredPosition = _enemyOriginalPosition;
            _enemyCPRect.localScale = _enemyOriginalScale;
            _enemyCPRect.localEulerAngles = _enemyOriginalRotation;
            SetActive(_enemyCPRect.gameObject, _enemyOriginalActive);
        }

        if (_damageResultRoot != null)
        {
            _damageResultRoot.anchoredPosition = _damageResultOriginalPosition;
            _damageResultRoot.localScale = _damageResultOriginalScale;
            _damageResultRoot.localEulerAngles = _damageResultOriginalRotation;
        }

        ResetVisualObjects();
    }

    private void ResetVisualObjects()
    {
        SetActive(_subtractImage, false);
        SetActive(_emphasisImage, false);

        if (_damageResultRoot != null)
            SetActive(_damageResultRoot.gameObject, false);
    }

    private void PlayDamageResultDisplay(SiegeCollisionResolvedEvent e)
    {
        if (_damageResultRoot == null && _damageResultText != null)
            _damageResultRoot = _damageResultText.rectTransform;

        if (_damageResultRoot == null) return;

        _damageResultSequence?.Kill();

        float displayDamage = e.IsPlayerLosing ? -e.FinalDamage : e.FinalDamage;

        if (_damageResultText != null)
        {
            int roundedDamage = Mathf.RoundToInt(displayDamage);
            _damageResultText.text = roundedDamage > 0 ? $"+{roundedDamage}" : roundedDamage.ToString();
        }

        RectTransform resultPoint = displayDamage < 0f && _ourDamageResultPoint != null
            ? _ourDamageResultPoint
            : _damageResultPoint;
        Vector2 displayPosition = GetTargetAnchoredPosition(_damageResultRoot, resultPoint, _damageResultOriginalPosition);

        _damageResultRoot.anchoredPosition = displayPosition;
        _damageResultRoot.localEulerAngles = _damageResultOriginalRotation;
        _damageResultRoot.localScale = Vector3.zero;
        SetActive(_damageResultRoot.gameObject, true);
        SetActive(_emphasisImage, true);

        _damageResultSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        _damageResultSequence
            .Append(_damageResultRoot.DOScale(_damageResultOriginalScale * _damageResultPopScale, _damageResultPopDuration).SetEase(Ease.OutBack))
            .Append(_damageResultRoot.DOScale(_damageResultOriginalScale, _damageResultPopDuration * 0.5f).SetEase(Ease.OutQuad))
            .AppendInterval(_damageResultDuration)
            .AppendCallback(() =>
            {
                if (_damageResultRoot != null)
                {
                    _damageResultRoot.anchoredPosition = _damageResultOriginalPosition;
                    _damageResultRoot.localScale = _damageResultOriginalScale;
                    _damageResultRoot.localEulerAngles = _damageResultOriginalRotation;
                    SetActive(_damageResultRoot.gameObject, false);
                }

                SetActive(_emphasisImage, false);
            });
    }

    private void AppendPostImpactAnimation(SiegeImpactStartedEvent e, Vector2 playerClashPoint, Vector2 enemyClashPoint)
    {
        if (e.Delta <= 0f)
        {
            AppendTieImpact();
            return;
        }

        RectTransform loser = e.IsPlayerLosing ? _playerCPRect : _enemyCPRect;
        RectTransform winner = e.IsPlayerLosing ? _enemyCPRect : _playerCPRect;
        Vector2 loserClashPoint = e.IsPlayerLosing ? playerClashPoint : enemyClashPoint;
        Vector2 flyDirection = e.IsPlayerLosing ? new Vector2(-0.45f, -1f) : new Vector2(0.45f, -1f);
        Vector2 flyTarget = loserClashPoint + flyDirection.normalized * _loserFlyDistance;
        float spinDirection = e.IsPlayerLosing ? 1f : -1f;

        _impactSequence
            .AppendCallback(() => SetActive(winner.gameObject, false))
            .Append(loser.DOAnchorPos(flyTarget, _loserFlyDuration).SetEase(Ease.OutCubic))
            .Join(loser.DOLocalRotate(new Vector3(0f, 0f, _loserSpinDegrees * spinDirection), _loserFlyDuration, RotateMode.FastBeyond360).SetEase(Ease.OutCubic))
            .Join(winner.DOScale(winner.localScale * _winnerImpactScale, _impactShakeDuration).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad))
            .Join(winner.DOShakeAnchorPos(_impactShakeDuration, new Vector2(18f, 8f), 10, 80f, false, true))
            .Join(_playerCPRect.DOShakeAnchorPos(_impactShakeDuration, new Vector2(12f, 6f), 8, 70f, false, true))
            .Join(_enemyCPRect.DOShakeAnchorPos(_impactShakeDuration, new Vector2(12f, 6f), 8, 70f, false, true));
    }

    private void AppendTieImpact()
    {
        _impactSequence
            .Append(_playerCPRect.DOShakeAnchorPos(_impactShakeDuration, new Vector2(18f, 8f), 10, 80f, false, true))
            .Join(_enemyCPRect.DOShakeAnchorPos(_impactShakeDuration, new Vector2(18f, 8f), 10, 80f, false, true));
    }

    private void KillSequences()
    {
        _calculationSequence?.Kill();
        _calculationSequence = null;

        _impactSequence?.Kill();
        _impactSequence = null;

        _damageResultSequence?.Kill();
        _damageResultSequence = null;
    }

    private Vector2 GetTargetAnchoredPosition(RectTransform movingRect, RectTransform targetRect, Vector2 fallback)
    {
        if (movingRect == null) return fallback;
        if (targetRect == null) return fallback;
        if (movingRect.parent == targetRect.parent) return targetRect.anchoredPosition;

        RectTransform parentRect = movingRect.parent as RectTransform;
        if (parentRect == null) return targetRect.anchoredPosition;

        Camera camera = null;
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            camera = _canvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, targetRect.position);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, camera, out Vector2 localPoint)
            ? localPoint
            : fallback;
    }

    private Vector2 GetClashAnchoredPosition(RectTransform movingRect)
    {
        if (_impactTargetPoint != null)
            return GetTargetAnchoredPosition(movingRect, _impactTargetPoint, Vector2.zero);

        if (_playerCalculationPoint == null || _enemyCalculationPoint == null)
            return Vector2.zero;

        Vector3 midpoint = (_playerCalculationPoint.position + _enemyCalculationPoint.position) * 0.5f;
        return WorldPointToAnchoredPosition(movingRect, midpoint, Vector2.zero);
    }

    private Vector2 WorldPointToAnchoredPosition(RectTransform movingRect, Vector3 worldPosition, Vector2 fallback)
    {
        if (movingRect == null) return fallback;

        RectTransform parentRect = movingRect.parent as RectTransform;
        if (parentRect == null) return fallback;

        Camera camera = null;
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            camera = _canvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldPosition);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, camera, out Vector2 localPoint)
            ? localPoint
            : fallback;
    }

    private void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
