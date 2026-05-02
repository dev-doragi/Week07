using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class GaugeController : MonoBehaviour
{
    [Header("Gauge Settings")]
    [SerializeField, Min(1f)] private float _maxGauge = 100f;
    [SerializeField, Min(1f)] private float _gaugePerHit = 10f;

    [Header("UI")]
    [SerializeField] private Button _button;
    [SerializeField] private RectTransform _gaugeRect;

    [Header("Full Gauge Punch")]
    [SerializeField] private RectTransform _punchTarget;
    [SerializeField] private bool _playFullGaugePunch = true;
    [SerializeField] private Vector3 _fullGaugePunchScale = new Vector3(0.18f, 0.18f, 0f);
    [SerializeField, Min(0f)] private float _fullGaugePunchDuration = 0.35f;
    [SerializeField, Min(1)] private int _fullGaugePunchVibrato = 6;
    [SerializeField, Range(0f, 1f)] private float _fullGaugePunchElasticity = 0.8f;
    [SerializeField] private bool _playFullGaugeLoop = true;
    [SerializeField] private Vector3 _fullGaugeLoopScaleMultiplier = new Vector3(1.08f, 1.08f, 1f);
    [SerializeField, Min(0.01f)] private float _fullGaugeLoopHalfDuration = 0.45f;
    [SerializeField] private Ease _fullGaugeLoopEase = Ease.InOutSine;

    [Header("References")]
    [SerializeField] private SiegeChargeHandler _siegeHandler;

    public event Action OnChargeActivated;
    public event Action<float> OnGaugeChanged;

    private float _currentGauge = 0f;
    private bool _isGaugeFull = false;
    private bool _isPaused = false;
    private bool _isWaveActive = false; // 웨이브 진행 중 플래그
    private float _maxGaugeWidth;
    private float _gaugeGainMultiplier = 1f;
    private Tween _fullGaugePunchTween;
    private Tween _fullGaugeLoopTween;
    private Vector3 _punchTargetOriginalScale = Vector3.one;

    public float GaugeNormalized => _currentGauge / _maxGauge;
    public bool IsGaugeFull => _isGaugeFull;
    public float CurrentGauge => _currentGauge;

    private void OnEnable()
    {
        if (_button != null)
            _button.onClick.AddListener(HandleButtonClick);

        UpdateGaugeUI();

        EventBus.Instance?.Subscribe<EnemyHitEvent>(OnEnemyHit);
        EventBus.Instance?.Subscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance?.Subscribe<WaveEndedEvent>(OnWaveEnded);
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(HandleButtonClick);

        StopFullGaugePunch(resetScale: true);

        EventBus.Instance?.Unsubscribe<EnemyHitEvent>(OnEnemyHit);
        EventBus.Instance?.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance?.Unsubscribe<WaveEndedEvent>(OnWaveEnded);
    }

    private void Awake()
    {
        ResolvePunchTarget();

        if (_gaugeRect != null)
            _maxGaugeWidth = _gaugeRect.sizeDelta.x;

        _currentGauge = 0f;
        _isGaugeFull = false;
        _isWaveActive = false;
        if (_button != null)
            _button.interactable = false;

        UpdateGaugeUI();

        if (_siegeHandler == null)
        {
            _siegeHandler = FindAnyObjectByType<SiegeChargeHandler>();
        }

        if (_siegeHandler != null)
        {
            OnChargeActivated += _siegeHandler.ExecuteCrash;
            _siegeHandler.OnCrashEnd += Resume;
        }
    }

    private void OnDestroy()
    {
        StopFullGaugePunch(resetScale: true);

        if (_siegeHandler != null)
        {
            OnChargeActivated -= _siegeHandler.ExecuteCrash;
            _siegeHandler.OnCrashEnd -= Resume;
        }
    }

    private void OnEnemyHit(EnemyHitEvent evt)
    {
        if (evt.AttackerTeam != TeamType.Player) return;

        OnHit();
    }

    /// <summary>
    /// 웨이브 시작 - 차지 허용 시작
    /// </summary>
    private void OnWaveStarted(WaveStartedEvent evt)
    {
        _isWaveActive = true;
        // 게이지가 찼으면 버튼 활성화
        Debug.Log("게이지 차기 시작");
        if (_isGaugeFull && _button != null)
        {
            _button.interactable = true;
            PlayFullGaugePunch();
        }
    }

    /// <summary>
    /// 웨이브 종료 - 차지 불가능하게 리셋
    /// </summary>
    private void OnWaveEnded(WaveEndedEvent evt)
    {
        _isWaveActive = false;
        _currentGauge = 0f;
        _isGaugeFull = false;
        _isPaused = false;
        if (_button != null)
            _button.interactable = false;
        StopFullGaugePunch(resetScale: true);
        UpdateGaugeUI();
    }

    public void OnHit()
    {
        // 웨이브 중에만 차지 가능
        if (_isPaused || _isGaugeFull || !_isWaveActive) return;

        float gain = _gaugePerHit * _gaugeGainMultiplier;
        _currentGauge = Mathf.Min(_currentGauge + gain, _maxGauge);

        UpdateGaugeUI();

        if (_currentGauge >= _maxGauge)
        {
            _isGaugeFull = true;
            if (_button != null) _button.interactable = true;
            PlayFullGaugePunch();
            OnGaugeChanged?.Invoke(1f);
        }
        else
        {
            OnGaugeChanged?.Invoke(GaugeNormalized);
        }
    }

    public void HandleButtonClick()
    {
        if (!_isGaugeFull || _isPaused || !_isWaveActive) return;

        _currentGauge = 0f;
        _isGaugeFull = false;
        _isPaused = true;
        UpdateGaugeUI();

        if (_button != null) _button.interactable = false;
        StopFullGaugePunch(resetScale: true);

        EventBus.Instance?.Publish(new SiegeChargeStartedEvent());
        OnChargeActivated?.Invoke();
        EventBus.Instance?.Publish(new TutorialAccelerationButtonUsedEvent());
    }

    public void Resume()
    {
        _isPaused = false;
    }

    public void SetDoctrineGaugeGainMultiplier(float multiplier)
    {
        _gaugeGainMultiplier = Mathf.Max(0.01f, multiplier);
        Debug.Log($"[GaugeController] Doctrine gauge gain multiplier set: {_gaugeGainMultiplier:F2}");
    }

    private void UpdateGaugeUI()
    {
        float normalizedGauge = GaugeNormalized;
        SetGaugeWidth(normalizedGauge * _maxGaugeWidth);
    }

    private void SetGaugeWidth(float width)
    {
        if (_gaugeRect == null) return;
        _gaugeRect.sizeDelta = new Vector2(width, _gaugeRect.sizeDelta.y);
    }

    private void ResolvePunchTarget()
    {
        if (_punchTarget == null && _button != null)
            _punchTarget = _button.transform as RectTransform;

        if (_punchTarget != null)
            _punchTargetOriginalScale = _punchTarget.localScale;
    }

    private void PlayFullGaugePunch()
    {
        if (_punchTarget == null) return;

        StopFullGaugePunch(resetScale: true);

        if (!_playFullGaugePunch || _fullGaugePunchDuration <= 0f)
        {
            StartFullGaugeLoop();
            return;
        }

        _fullGaugePunchTween = _punchTarget
            .DOPunchScale(_fullGaugePunchScale, _fullGaugePunchDuration, _fullGaugePunchVibrato, _fullGaugePunchElasticity)
            .OnComplete(() =>
            {
                _punchTarget.localScale = _punchTargetOriginalScale;
                StartFullGaugeLoop();
            });
    }

    private void StartFullGaugeLoop()
    {
        if (!_playFullGaugeLoop || !_isGaugeFull || _punchTarget == null) return;

        _fullGaugeLoopTween?.Kill();
        _punchTarget.localScale = _punchTargetOriginalScale;

        Vector3 loopScale = Vector3.Scale(_punchTargetOriginalScale, _fullGaugeLoopScaleMultiplier);
        _fullGaugeLoopTween = _punchTarget
            .DOScale(loopScale, _fullGaugeLoopHalfDuration)
            .SetEase(_fullGaugeLoopEase)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopFullGaugePunch(bool resetScale)
    {
        _fullGaugePunchTween?.Kill();
        _fullGaugePunchTween = null;
        _fullGaugeLoopTween?.Kill();
        _fullGaugeLoopTween = null;

        if (resetScale && _punchTarget != null)
            _punchTarget.localScale = _punchTargetOriginalScale;
    }
}
