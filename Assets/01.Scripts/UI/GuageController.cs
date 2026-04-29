using System;
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

        EventBus.Instance?.Unsubscribe<EnemyHitEvent>(OnEnemyHit);
        EventBus.Instance?.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance?.Unsubscribe<WaveEndedEvent>(OnWaveEnded);
    }

    private void Awake()
    {
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
        if (_isGaugeFull && _button != null)
            _button.interactable = true;
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
}
