using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

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
    [SerializeField] private int _resourcePerCell = 1;
    [SerializeField] private bool _useUnscaledTime;
    [SerializeField] private bool _logProduction = true;

    [Header("UI")]
    [SerializeField] private Slider _productionGaugeSlider;
    [SerializeField] private TextMeshProUGUI _predictionText;
    [SerializeField] private TextMeshProUGUI _attackConsumptionText;
    [SerializeField] private RectTransform _attackConsumptionParent;
    [SerializeField] private TMP_FontAsset _uiFont;
    [SerializeField] private Vector2 _gaugeSize = new Vector2(220f, 14f);
    [SerializeField] private Vector2 _gaugeOffset = new Vector2(0f, -24f);
    [SerializeField] private Color _gaugeBackgroundColor = new Color(1f, 1f, 1f, 0.16f);
    [SerializeField] private Color _gaugeFillColor = new Color(0.35f, 0.95f, 0.45f, 0.95f);
    [SerializeField] private Vector2 _predictionTextOffset = new Vector2(0f, -44f);
    [SerializeField] private int _predictionFontSize = 22;
    [SerializeField] private Color _predictionTextColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private string _predictionTextFormat = "예상 획득: +{0} / {1:0.#}s";
    [SerializeField] private Vector2 _attackConsumptionTextSize = new Vector2(300f, 30f);
    [SerializeField] private Vector2 _attackConsumptionTextOffset = Vector2.zero;
    [SerializeField] private int _attackConsumptionFontSize = 22;
    [SerializeField] private Color _attackConsumptionTextColor = new Color(1f, 0.35f, 0.25f, 0.95f);
    [SerializeField] private string _attackConsumptionTextFormat = "예상 소모: -{0:0.#} / {1:0.#}s";

    public int TotalProduced { get; private set; }
    public int LastProduced { get; private set; }

    private Coroutine _scanRoutine;
    private bool _warnedMissingResourceManager;
    private bool _warnedMissingGridBoard;
    private bool _warnedMissingGridRoot;
    private bool _warnedMissingAttackConsumptionParent;
    private float _scanTimer;
    private int _productionBonus = 0;

    private void OnEnable()
    {
        if (_resourcePerCell <= 1)
            _resourcePerCell = 1;

        if (EventBus.Instance != null)
            EventBus.Instance.Subscribe<StageMapVisibilityChangedEvent>(OnStageMapVisibilityChanged);

        EnsureProductionGauge();
        EnsurePredictionText();
        EnsureAttackConsumptionText(false);
        UpdatePredictionText();
        UpdateAttackConsumptionText();

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
        UpdatePredictionText();
        UpdateAttackConsumptionText();

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

    public void SetProductionBonus(int bonus)
    {
        _productionBonus = bonus;
    }

    [ContextMenu("Produce Once")]
    public void ProduceOnce()
    {
        if (_gridBoard == null)
            return;


        int totalOccupied = _gridBoard.GetOccupiedCellCount();
        int amount = (totalOccupied * Mathf.Max(1, _resourcePerCell)) + _productionBonus;

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
            manager.AddMouseCount(amount, "income_production");
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
            int baseAmount = totalOccupied * Mathf.Max(1, _resourcePerCell);
            Debug.Log($"[IncomeResourceProducer] 생산 완료 | 총 {amount} " +
              $"(전체칸: {totalOccupied} × {_resourcePerCell} = {baseAmount}" +
              $" + 우하단보너스: {_productionBonus})");
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
            manager.AddMouseCount(amount, "income_skipped_wait");

        if (_logProduction)
            Debug.Log($"[IncomeResourceProducer] Granted {amount} resources for skipped wait ({duration:F1}s, occupied: {occupied}).");

        return amount;
    }

    public void SetGridBoard(IncomeGridBoard gridBoard)
    {
        _gridBoard = gridBoard;
        _warnedMissingGridBoard = false;
        _warnedMissingGridRoot = false;
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
        if (_resourceManager != null) return _resourceManager;

        try
        {
            _resourceManager = ResourceManager.Instance;
        }
        catch
        {

        }

        return _resourceManager;
    }

    private static T FindSceneObject<T>() where T : Object
    {
#if UNITY_EDITOR
        T[] editorObjects = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < editorObjects.Length; i++)
        {
            T obj = editorObjects[i];
            if (obj == null || EditorUtility.IsPersistent(obj))
                continue;

            return obj;
        }
#endif
        return Object.FindFirstObjectByType<T>();
    }

    [ContextMenu("Build Production Gauge UI")]
    public void BuildProductionGaugeUI()
    {
        EnsureProductionGauge();
        EnsurePredictionText();
        EnsureAttackConsumptionText(true);

        if (_productionGaugeSlider != null && !Application.isPlaying)
            _productionGaugeSlider.gameObject.SetActive(true);

        if (_predictionText != null && !Application.isPlaying)
            _predictionText.gameObject.SetActive(true);

        if (_attackConsumptionText != null && !Application.isPlaying)
            _attackConsumptionText.gameObject.SetActive(true);
    }

    [ContextMenu("Build Attack Consumption UI")]
    public void BuildAttackConsumptionUI()
    {
        EnsureAttackConsumptionText(true);

        if (_attackConsumptionText != null && !Application.isPlaying)
            _attackConsumptionText.gameObject.SetActive(true);

        if (_attackConsumptionText != null)
            Debug.Log($"[IncomeResourceProducer] Attack Consumption UI ready: {_attackConsumptionText.name}", _attackConsumptionText);
    }

    private void EnsureProductionGauge()
    {
        if (_productionGaugeSlider != null)
            return;

        RectTransform boardRect = ResolveBoardRect();
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
        MarkSceneObjectDirty(gaugeObj);
    }

    private void EnsurePredictionText()
    {
        if (_predictionText != null)
            return;

        RectTransform boardRect = ResolveBoardRect();
        if (boardRect == null)
            return;

        Transform existing = boardRect.Find("IncomePredictionText");
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI existingText))
        {
            _predictionText = existingText;
            ApplyUIFont(_predictionText);
            return;
        }

        GameObject textObj = new GameObject("IncomePredictionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(boardRect, false);
        textRect.anchorMin = new Vector2(0.5f, 1f);
        textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(260f, 30f);
        textRect.anchoredPosition = _predictionTextOffset;

        _predictionText = textObj.GetComponent<TextMeshProUGUI>();
        ApplyUIFont(_predictionText);
        _predictionText.fontSize = Mathf.Max(14, _predictionFontSize);
        _predictionText.alignment = TextAlignmentOptions.Center;
        _predictionText.color = _predictionTextColor;
        _predictionText.raycastTarget = false;
        _predictionText.text = string.Empty;
        MarkSceneObjectDirty(textObj);
    }

    private void EnsureAttackConsumptionText(bool rebuildFromHierarchy)
    {
        if (!rebuildFromHierarchy && _attackConsumptionText != null)
            return;

        RectTransform parentRect = ResolveAttackConsumptionParent();
        if (parentRect == null)
            return;

        Transform existing = parentRect.Find("AttackConsumptionText");
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI existingText))
        {
            _attackConsumptionText = existingText;
            ApplyUIFont(_attackConsumptionText);
            _attackConsumptionText.gameObject.SetActive(true);
            if (rebuildFromHierarchy)
                Debug.Log("[IncomeResourceProducer] Found existing AttackConsumptionText under MouseVisualizer Panel.", _attackConsumptionText);
            return;
        }

        if (_attackConsumptionText != null)
            return;

        GameObject textObj = new GameObject("AttackConsumptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RegisterCreatedObjectUndo(textObj, "Build Attack Consumption UI");
#endif
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(parentRect, false);
        textRect.SetAsLastSibling();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = _attackConsumptionTextSize;
        textRect.anchoredPosition = _attackConsumptionTextOffset;

        _attackConsumptionText = textObj.GetComponent<TextMeshProUGUI>();
        ApplyUIFont(_attackConsumptionText);
        _attackConsumptionText.fontSize = Mathf.Max(14, _attackConsumptionFontSize);
        _attackConsumptionText.alignment = TextAlignmentOptions.Center;
        _attackConsumptionText.color = _attackConsumptionTextColor;
        _attackConsumptionText.raycastTarget = false;
        _attackConsumptionText.text = string.Empty;
        MarkSceneObjectDirty(textObj);
        MarkSceneObjectDirty(parentRect.gameObject);
        Debug.Log("[IncomeResourceProducer] Created AttackConsumptionText under MouseVisualizer Panel.", _attackConsumptionText);
    }

    private RectTransform ResolveAttackConsumptionParent()
    {
        if (_attackConsumptionParent != null)
            return _attackConsumptionParent;

        RectTransform mousePanel = FindMouseVisualizerPanel();
        if (mousePanel != null)
        {
            _attackConsumptionParent = mousePanel;
            _warnedMissingAttackConsumptionParent = false;
            return _attackConsumptionParent;
        }

        if (!_warnedMissingAttackConsumptionParent)
        {
            _warnedMissingAttackConsumptionParent = true;
            Debug.LogWarning("[IncomeResourceProducer] MouseVisualizer/Panel was not found. Assign Attack Consumption Parent to Canvas > InGamePanel > MouseVisualizer > Panel before building the UI.");
        }

        return null;
    }

    private static RectTransform FindMouseVisualizerPanel()
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || IsPersistentObject(canvas))
                continue;

            Transform panel = canvas.transform.Find("InGamePanel/MouseVisualizer/Panel");
            if (panel != null && panel.TryGetComponent(out RectTransform panelRect))
                return panelRect;
        }

        RectTransform[] rects = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null || rect.name != "MouseVisualizer" || IsPersistentObject(rect))
                continue;

            Transform panel = rect.Find("Panel");
            if (panel != null && panel.TryGetComponent(out RectTransform panelRect))
                return panelRect;
        }

        return null;
    }

    private static bool IsPersistentObject(Object obj)
    {
#if UNITY_EDITOR
        return EditorUtility.IsPersistent(obj);
#else
        return false;
#endif
    }

    private RectTransform ResolveBoardRect()
    {
        if (_gridBoard == null)
            _gridBoard = FindSceneObject<IncomeGridBoard>();

        if (_gridBoard == null)
        {
            if (!_warnedMissingGridBoard)
            {
                _warnedMissingGridBoard = true;
                Debug.LogWarning("[IncomeResourceProducer] IncomeGridBoard was not found. Assign Grid Board or keep one in the active scene before building the UI.");
            }
            return null;
        }

        RectTransform boardRect = _gridBoard.GridRoot != null
            ? _gridBoard.GridRoot
            : _gridBoard.transform as RectTransform;

        if (boardRect == null)
        {
            if (!_warnedMissingGridRoot)
            {
                _warnedMissingGridRoot = true;
                Debug.LogWarning("[IncomeResourceProducer] IncomeGridBoard needs a RectTransform or GridRoot before building the UI.");
            }
            return null;
        }

        _warnedMissingGridBoard = false;
        _warnedMissingGridRoot = false;
        return boardRect;
    }

    private static void MarkSceneObjectDirty(GameObject obj)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || obj == null)
            return;

        EditorUtility.SetDirty(obj);
        EditorSceneManager.MarkSceneDirty(obj.scene);
#endif
    }

    private void UpdateProductionGauge(float value)
    {
        EnsureProductionGauge();

        if (_productionGaugeSlider != null)
            _productionGaugeSlider.value = value;
    }

    private void UpdatePredictionText()
    {
        EnsurePredictionText();

        if (_predictionText == null)
            return;

        ApplyUIFont(_predictionText);
        float interval = Mathf.Max(0.1f, _scanInterval);
        int predicted = GetPredictedProductionPerCycle();
        _predictionText.text = string.Format(_predictionTextFormat, predicted, interval);
    }

    private void UpdateAttackConsumptionText()
    {
        EnsureAttackConsumptionText(false);

        if (_attackConsumptionText == null)
            return;

        ApplyUIFont(_attackConsumptionText);
        float interval = Mathf.Max(0.1f, _scanInterval);
        float predicted = GetPredictedAttackConsumptionPerCycle(interval);
        _attackConsumptionText.text = string.Format(_attackConsumptionTextFormat, predicted, interval);
    }

    private int GetPredictedProductionPerCycle()
    {
        if (_gridBoard == null)
            return 0;

        int occupied = _gridBoard.GetOccupiedCellCount();
        if (occupied <= 0) return 0;
        return (occupied * Mathf.Max(1, _resourcePerCell)) + _productionBonus;
    }

    private float GetPredictedAttackConsumptionPerCycle(float interval)
    {
        GridManager gridManager = ResolveGridManager();
        if (gridManager == null)
            return 0f;

        float total = 0f;
        var placedUnits = gridManager.GetPlacedUnitsSnapshot();
        for (int i = 0; i < placedUnits.Count; i++)
        {
            PlacedUnit placed = placedUnits[i];
            UnitDataSO data = placed?.Data;
            AttackModule attack = data != null ? data.Attack : null;
            if (data == null
                || data.Team != TeamType.Player
                || data.Category != UnitCategory.Attack
                || attack == null
                || attack.AttackCost <= 0)
            {
                continue;
            }

            float attackInterval = GetAttackInterval(placed, attack);
            total += attack.AttackCost * interval / attackInterval;
        }

        return total;
    }

    private float GetAttackInterval(PlacedUnit placed, AttackModule attack)
    {
        float baseAttackInterval = Mathf.Max(0.01f, attack.Speed);
        float baseAttacksPerSecond = 1f / baseAttackInterval;

        Unit unit = placed.Instance != null
            ? placed.Instance.GetComponentInChildren<Unit>()
            : null;

        float modifiedAttacksPerSecond = unit != null && unit.StatReceiver != null
            ? unit.StatReceiver.GetModifiedValue(SupportStatType.AttackSpeed, baseAttacksPerSecond)
            : baseAttacksPerSecond;

        return 1f / Mathf.Max(0.1f, modifiedAttacksPerSecond);
    }

    private GridManager ResolveGridManager()
    {
        try
        {
            if (GridManager.Instance != null)
                return GridManager.Instance;
        }
        catch
        {

        }

        return FindSceneObject<GridManager>();
    }

    private void ApplyUIFont(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        TMP_FontAsset font = ResolveUIFont();
        if (font != null)
            text.font = font;
    }

    private TMP_FontAsset ResolveUIFont()
    {
        if (_uiFont != null)
            return _uiFont;

#if UNITY_EDITOR
        _uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/10.Fonts/Galmuri9 SDF.asset");
#endif
        return _uiFont;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
