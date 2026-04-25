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

    public event Action OnActivated;
    public event Action<float> OnGaugeChanged;

    private float _currentGauge = 0f;
    private bool _isGaugeFull = false;
    private bool _isPaused = false;
    private float _maxGaugeWidth;

    public float GaugeNormalized => _currentGauge / _maxGauge;
    public bool IsGaugeFull => _isGaugeFull;
    public float CurrentGauge => _currentGauge;

    private void OnEnable()
    {
        if (_button != null)
            _button.onClick.AddListener(HandleButtonClick);

        UpdateGaugeUI();

        EventBus.Instance?.Subscribe<EnemyHitEvent>(OnEnemyHit);
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(HandleButtonClick);

        EventBus.Instance?.Unsubscribe<EnemyHitEvent>(OnEnemyHit);
    }

    private void Awake()
    {
        if (_gaugeRect != null)
            _maxGaugeWidth = _gaugeRect.sizeDelta.x;

        _currentGauge = 0f;
        _isGaugeFull = false;
        if (_button != null)
            _button.interactable = false;

        UpdateGaugeUI();

        if (_siegeHandler == null)
        {
            _siegeHandler = FindAnyObjectByType<SiegeChargeHandler>();
        }

        if (_siegeHandler != null)
        {
            _siegeHandler.OnCrashEnd += Resume;
        }
    }

    private void OnDestroy()
    {
        if (_siegeHandler != null)
        {
            _siegeHandler.OnCrashEnd -= Resume;
        }
    }

    private void OnEnemyHit(EnemyHitEvent evt)
    {
        if (evt.AttackerTeam != TeamType.Player) return;

        OnHit();
    }

    public void OnHit()
    {
        if (_isPaused || _isGaugeFull) return;

        _currentGauge = Mathf.Min(_currentGauge + _gaugePerHit, _maxGauge);

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

    private void HandleButtonClick()
    {
        if (!_isGaugeFull || _isPaused) return;

        _currentGauge = 0f;
        _isGaugeFull = false;
        _isPaused = true;
        UpdateGaugeUI();

        if (_button != null) _button.interactable = false;

        OnActivated?.Invoke();
    }

    public void Resume()
    {
        _isPaused = false;
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