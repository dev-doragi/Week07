using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 범용 차지 게이지 컨트롤러
/// </summary>
public class GaugeController : MonoBehaviour
{
    [Header("Gauge Settings")]
    [SerializeField, Min(1f)] private float _maxGauge = 100f;
    [SerializeField, Min(0f)] private float _chargeGaugePower = 1f;

    [Header("UI")]
    [SerializeField] private Button _button;
    [SerializeField] private RectTransform _gaugeRect;

    [Header("References")]
    [SerializeField] private SiegeChargeHandler _siegeHandler;

    [Header("Events")]
    public UnityEvent OnActivated;

    private float _currentGauge;
    private bool _isGaugeFull;
    private bool _isPaused;
    private float _maxGaugeWidth; 

    public float GaugeNormalized => _currentGauge / _maxGauge;
    public bool IsGaugeFull => _isGaugeFull;

    private void OnEnable()
    {
        if (_button != null)
            _button.interactable = false;
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(HandleButtonClick);
    }

    private void Awake()
    {
        if (_gaugeRect != null)
            _maxGaugeWidth = _gaugeRect.sizeDelta.x;

        if (_siegeHandler == null)
        {
            _siegeHandler = FindAnyObjectByType<SiegeChargeHandler>();
        }
    }

    private void Update()
    {
        TickGauge();
    }

    private void TickGauge()
    {
        if (_isGaugeFull || _isPaused) return;

        _currentGauge = Mathf.Min(_currentGauge + _chargeGaugePower * Time.deltaTime, _maxGauge);
        SetGaugeWidth(_currentGauge / _maxGauge * _maxGaugeWidth);

        if (_currentGauge >= _maxGauge)
        {
            _isGaugeFull = true;
            if (_button != null) _button.interactable = true;
        }
    }

    private void HandleButtonClick()
    {
        if (!_isGaugeFull || _isPaused) return;

        // 게이지 소비 + 버튼 잠금
        _currentGauge = 0f;
        _isGaugeFull = false;
        _isPaused = true;
        SetGaugeWidth(0f);

        if (_button != null) _button.interactable = false;

        // 외부 액션 호출 (SiegeChargeHandler.ExecuteCrash 등)
        OnActivated?.Invoke();
    }

    /// <summary>
    /// 외부 액션 완료 후 게이지 재충전 재개
    /// SiegeChargeHandler.OnCrashEnd → 이 메서드에 연결
    /// </summary>
    public void Resume()
    {
        _isPaused = false;
    }

    private void SetGaugeWidth(float width)
    {
        if (_gaugeRect == null) return;
        _gaugeRect.sizeDelta = new Vector2(width, _gaugeRect.sizeDelta.y);
    }
}