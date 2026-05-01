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
    [SerializeField, Min(0f)] private float _subtractRevealDelay = 0.1f;
    [SerializeField] private float _calculationScale = 1.5f;

    [Header("Result Impact Animation")]
    [SerializeField] private RectTransform _resultRoot;
    [SerializeField] private TextMeshProUGUI _resultText;
    [SerializeField] private GameObject _emphasisImage;
    [SerializeField] private RectTransform _resultRevealPoint;
    [SerializeField] private RectTransform _impactTargetPoint;
    [SerializeField, Min(0f)] private float _resultRevealDuration = 0.25f;
    [SerializeField, Min(0f)] private float _flyDuration = 0.45f;
    [SerializeField] private float _resultPopScale = 1.25f;
    [SerializeField] private float _impactScale = 1.2f;

    private Sequence _calculationSequence;
    private Sequence _impactSequence;
    private RectTransform _playerCPRect;
    private RectTransform _enemyCPRect;
    private Canvas _canvas;
    private Vector2 _playerOriginalPosition;
    private Vector2 _enemyOriginalPosition;
    private Vector2 _resultOriginalPosition;
    private Vector3 _playerOriginalScale;
    private Vector3 _enemyOriginalScale;
    private Vector3 _resultOriginalScale;
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
        EventBus.Instance?.Subscribe<SiegeImpactStartedEvent>(OnSiegeImpactStarted);
        EventBus.Instance?.Subscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance?.Subscribe<WaveEndedEvent>(OnWaveEnded);
        EventBus.Instance?.Subscribe<StageClearedEvent>(OnStageCleared);
        EventBus.Instance?.Subscribe<StageCleanedUpEvent>(OnStageCleanedUp);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<CollisionPowerUpdatedEvent>(OnCollisionPowerUpdated);
        EventBus.Instance?.Unsubscribe<SiegeChargeStartedEvent>(OnSiegeChargeStarted);
        EventBus.Instance?.Unsubscribe<SiegeImpactStartedEvent>(OnSiegeImpactStarted);
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
            .Join(_enemyCPRect.DOScale(_enemyOriginalScale * _calculationScale, _scaleDuration).SetEase(Ease.OutBack))
            .AppendInterval(_subtractRevealDelay)
            .AppendCallback(() => SetActive(_subtractImage, true));
    }

    private void PlayImpactAnimation(SiegeImpactStartedEvent e)
    {
        if (_resultRoot == null && _resultText != null)
            _resultRoot = _resultText.rectTransform;

        if (_resultRoot == null) return;

        _impactSequence?.Kill();

        if (_resultText != null)
            _resultText.text = $"{e.FinalDamage:F1}";

        SetActive(_resultRoot.gameObject, true);

        Vector2 revealPosition = GetTargetAnchoredPosition(_resultRoot, _resultRevealPoint, new Vector2(0f, 170f));
        Vector2 impactPosition = GetTargetAnchoredPosition(_resultRoot, _impactTargetPoint, new Vector2(0f, -120f));

        _resultRoot.anchoredPosition = revealPosition;
        _resultRoot.localScale = Vector3.zero;

        _impactSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        float startDelay = Mathf.Max(0f, e.DelayUntilImpact - _resultRevealDuration - _flyDuration);

        _impactSequence
            .AppendInterval(startDelay)
            .AppendCallback(() => SetActive(_emphasisImage, true))
            .Append(_resultRoot.DOScale(_resultOriginalScale * _resultPopScale, _resultRevealDuration).SetEase(Ease.OutBack))
            .Append(_resultRoot.DOAnchorPos(impactPosition, _flyDuration).SetEase(Ease.InCubic))
            .Join(_resultRoot.DOScale(_resultOriginalScale * _impactScale, _flyDuration).SetEase(Ease.InCubic))
            .Append(_resultRoot.DOShakeAnchorPos(0.15f, new Vector2(20f, 12f), 12, 80f, false, true).SetUpdate(true))
            .AppendCallback(ResetAnimationState);
    }

    private void CacheReferences()
    {
        if (_playerCPText != null)
            _playerCPRect = _playerCPText.rectTransform;

        if (_enemyCPText != null)
            _enemyCPRect = _enemyCPText.rectTransform;

        if (_resultRoot == null && _resultText != null)
            _resultRoot = _resultText.rectTransform;

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
        }

        if (_enemyCPRect != null)
        {
            _enemyOriginalPosition = _enemyCPRect.anchoredPosition;
            _enemyOriginalScale = _enemyCPRect.localScale;
        }

        if (_resultRoot != null)
        {
            _resultOriginalPosition = _resultRoot.anchoredPosition;
            _resultOriginalScale = _resultRoot.localScale;
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
        }

        if (_enemyCPRect != null)
        {
            _enemyCPRect.anchoredPosition = _enemyOriginalPosition;
            _enemyCPRect.localScale = _enemyOriginalScale;
        }

        if (_resultRoot != null)
        {
            _resultRoot.anchoredPosition = _resultOriginalPosition;
            _resultRoot.localScale = _resultOriginalScale;
        }

        ResetVisualObjects();
    }

    private void ResetVisualObjects()
    {
        SetActive(_subtractImage, false);
        SetActive(_emphasisImage, false);

        if (_resultRoot != null)
            SetActive(_resultRoot.gameObject, false);
    }

    private void KillSequences()
    {
        _calculationSequence?.Kill();
        _calculationSequence = null;

        _impactSequence?.Kill();
        _impactSequence = null;
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

    private void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
