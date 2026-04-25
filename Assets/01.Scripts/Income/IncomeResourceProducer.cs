using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일정 주기로 Income 그리드를 스캔해 점유 칸 수 기반 자원을 생산하고 ResourceManager에 반영한다.
/// </summary>
public class IncomeResourceProducer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private IncomeGridBoard _gridBoard;
    [SerializeField] private ResourceManager _resourceManager;

    [Header("Production")]
    [SerializeField] private float _scanInterval = 5f;
    [Min(4)]
    [SerializeField] private int _resourcePerCell = 4;
    [SerializeField] private bool _useUnscaledTime;
    [SerializeField] private bool _logProduction = true;

    [Header("UI")]
    [SerializeField] private Slider _productionGaugeSlider;
    [SerializeField] private Vector2 _gaugeSize = new Vector2(220f, 14f);
    [SerializeField] private Vector2 _gaugeOffset = new Vector2(0f, -24f);
    [SerializeField] private Color _gaugeBackgroundColor = new Color(1f, 1f, 1f, 0.16f);
    [SerializeField] private Color _gaugeFillColor = new Color(0.35f, 0.95f, 0.45f, 0.95f);

    public int TotalProduced { get; private set; }
    public int LastProduced { get; private set; }

    private Coroutine _scanRoutine;
    private bool _warnedMissingResourceManager;
    private float _scanTimer;

    private void OnEnable()
    {
        if (_resourcePerCell < 4)
            _resourcePerCell = 4;

        if (EventBus.Instance != null)
            EventBus.Instance.Subscribe<StageMapVisibilityChangedEvent>(OnStageMapVisibilityChanged);

        EnsureProductionGauge();

        if (!StageMapController.IsMapVisible())
            StartScanning();
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
            EventBus.Instance.Unsubscribe<StageMapVisibilityChangedEvent>(OnStageMapVisibilityChanged);

        StopScanning();
    }

    private void Update()
    {
        if (_scanRoutine == null || StageMapController.IsMapVisible())
            return;

        if (_gridBoard == null || _gridBoard.GetOccupiedCellCount() <= 0)
        {
            _scanTimer = 0f;
            UpdateProductionGauge(0f);
            return;
        }

        float interval = Mathf.Max(0.1f, _scanInterval);
        _scanTimer += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        UpdateProductionGauge(Mathf.Clamp01(_scanTimer / interval));

        if (_scanTimer < interval)
            return;

        _scanTimer = 0f;
        UpdateProductionGauge(0f);
        ProduceOnce();
    }

    [ContextMenu("Produce Once")]
    public void ProduceOnce()
    {
        if (_gridBoard == null)
            return;

        int occupied = _gridBoard.GetOccupiedCellCount();
        int amount = occupied * Mathf.Max(1, _resourcePerCell);

        LastProduced = amount;
        if (amount <= 0)
            return;

        TotalProduced += amount;

        var manager = ResolveResourceManager();
        if (manager != null)
        {
            // 한번이라도 연결되면 누락 경고 상태는 해제한다.
            _warnedMissingResourceManager = false;

            int before = manager.CurrentMouse;
            manager.AddMouseCount(amount);
            if (_logProduction)
            {
                Debug.Log($"[IncomeResourceProducer] ResourceManager linked. Mouse: {before} -> {manager.CurrentMouse}");
            }
        }
        else if (!_warnedMissingResourceManager)
        {
            _warnedMissingResourceManager = true;
            Debug.LogWarning("[IncomeResourceProducer] ResourceManager was not found. Produced amount is not applied.");
        }

        if (_logProduction)
        {
            Debug.Log($"[IncomeResourceProducer] Produced {amount} resources (occupied: {occupied}, perCell: {_resourcePerCell}).");
        }
    }

    public int GrantProductionForDuration(float duration)
    {
        if (_gridBoard == null)
            return 0;

        int occupied = _gridBoard.GetOccupiedCellCount();
        if (occupied <= 0)
            return 0;

        float interval = Mathf.Max(0.1f, _scanInterval);
        int amount = Mathf.FloorToInt(occupied * Mathf.Max(1, _resourcePerCell) * Mathf.Max(0f, duration) / interval);
        if (amount <= 0)
            return 0;

        LastProduced = amount;
        TotalProduced += amount;

        ResourceManager manager = ResolveResourceManager();
        if (manager != null)
            manager.AddMouseCount(amount);

        if (_logProduction)
            Debug.Log($"[IncomeResourceProducer] Granted {amount} resources for skipped wait ({duration:F1}s, occupied: {occupied}).");

        return amount;
    }

    public void SetGridBoard(IncomeGridBoard gridBoard)
    {
        _gridBoard = gridBoard;
    }

    public void SetResourceManager(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public void StartScanning()
    {
        if (!isActiveAndEnabled || _scanRoutine != null)
            return;

        _scanRoutine = StartCoroutine(ScanningMarkerRoutine());
    }

    public void StopScanning()
    {
        if (_scanRoutine == null)
            return;

        StopCoroutine(_scanRoutine);
        _scanRoutine = null;
    }

    private IEnumerator ScanningMarkerRoutine()
    {
        while (true)
            yield return null;
    }

    private void OnStageMapVisibilityChanged(StageMapVisibilityChangedEvent evt)
    {
        if (evt.IsVisible)
            StopScanning();
        else
            StartScanning();
    }

    private ResourceManager ResolveResourceManager()
    {
        if (_resourceManager != null)
            return _resourceManager;

        _resourceManager = FindSceneObject<ResourceManager>();
        return _resourceManager;
    }

    private static T FindSceneObject<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>();
    }

    [ContextMenu("Build Production Gauge UI")]
    public void BuildProductionGaugeUI()
    {
        EnsureProductionGauge();

        if (_productionGaugeSlider != null && !Application.isPlaying)
            _productionGaugeSlider.gameObject.SetActive(true);
    }

    private void EnsureProductionGauge()
    {
        if (_productionGaugeSlider != null || _gridBoard == null)
            return;

        RectTransform boardRect = _gridBoard.transform as RectTransform;
        if (boardRect == null)
            return;

        Transform existing = boardRect.Find("IncomeProductionGauge");
        if (existing != null && existing.TryGetComponent(out Slider existingSlider))
        {
            _productionGaugeSlider = existingSlider;
            _productionGaugeSlider.value = 0f;
            return;
        }

        GameObject gaugeObj = new GameObject("IncomeProductionGauge", typeof(RectTransform), typeof(Slider));
        RectTransform gaugeRect = gaugeObj.GetComponent<RectTransform>();
        gaugeRect.SetParent(boardRect, false);
        gaugeRect.anchorMin = new Vector2(0.5f, 1f);
        gaugeRect.anchorMax = new Vector2(0.5f, 1f);
        gaugeRect.pivot = new Vector2(0.5f, 0.5f);
        gaugeRect.sizeDelta = _gaugeSize;
        gaugeRect.anchoredPosition = _gaugeOffset;

        GameObject backgroundObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        RectTransform backgroundRect = backgroundObj.GetComponent<RectTransform>();
        backgroundRect.SetParent(gaugeRect, false);
        Stretch(backgroundRect);
        Image background = backgroundObj.GetComponent<Image>();
        background.color = _gaugeBackgroundColor;
        background.raycastTarget = false;

        GameObject fillAreaObj = new GameObject("Fill Area", typeof(RectTransform));
        RectTransform fillAreaRect = fillAreaObj.GetComponent<RectTransform>();
        fillAreaRect.SetParent(gaugeRect, false);
        Stretch(fillAreaRect);

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.SetParent(fillAreaRect, false);
        Stretch(fillRect);
        Image fill = fillObj.GetComponent<Image>();
        fill.color = _gaugeFillColor;
        fill.raycastTarget = false;

        _productionGaugeSlider = gaugeObj.GetComponent<Slider>();
        _productionGaugeSlider.minValue = 0f;
        _productionGaugeSlider.maxValue = 1f;
        _productionGaugeSlider.value = 0f;
        _productionGaugeSlider.transition = Selectable.Transition.None;
        _productionGaugeSlider.interactable = false;
        _productionGaugeSlider.fillRect = fillRect;
        _productionGaugeSlider.targetGraphic = fill;
        _productionGaugeSlider.direction = Slider.Direction.LeftToRight;
    }

    private void UpdateProductionGauge(float value)
    {
        EnsureProductionGauge();

        if (_productionGaugeSlider != null)
            _productionGaugeSlider.value = value;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
