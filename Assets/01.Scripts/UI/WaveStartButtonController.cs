using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class WaveStartButtonController : MonoBehaviour
{
    [SerializeField] private Button _button;

    [Header("Settlement Preview")]
    [SerializeField] private IncomeResourceProducer _incomeResourceProducer;
    [SerializeField] private TextMeshProUGUI _settlementPreviewText;
    [SerializeField] private Vector2 _settlementPreviewSize = new Vector2(260f, 32f);
    [SerializeField] private Vector2 _settlementPreviewOffset = new Vector2(0f, 44f);
    [SerializeField, Min(1f)] private float _settlementPreviewFontSize = 22f;
    [SerializeField] private Color _settlementPreviewColor = new Color(1f, 0.9f, 0.25f, 1f);
    [SerializeField] private string _settlementPreviewFormat = "정산받는 쥐 : {0}";

    private void Awake()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        if (_button != null)
            _button.onClick.AddListener(LogWaveStartButtonClicked);

        EnsureSettlementPreviewText();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(LogWaveStartButtonClicked);
    }

    private void OnValidate()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        ApplySettlementPreviewLayout();
        ApplySettlementPreviewStyle();
    }

    private void Refresh()
    {
        if (_button == null)
            return;

        // 튜토리얼에서는 무조건 활성화
        if (StageLoadContext.IsTutorial)
        {
            _button.interactable = true;        
            RefreshSettlementPreview(false, null, null);
            return;
        }

        StageManager stageManager = StageManager.Instance;
        GameFlowManager gameFlowManager = GameFlowManager.Instance;
        bool canStart = (stageManager != null && stageManager.IsWaitingForWaveStart)
            || (gameFlowManager != null && gameFlowManager.IsWaitingForNextWave);
        _button.interactable = canStart;

        RefreshSettlementPreview(canStart, stageManager, gameFlowManager);
    }

    private void LogWaveStartButtonClicked()
    {
        GameCsvLogger.Instance.LogEvent(
            GameLogEventType.ButtonClicked,
            actor: gameObject,
            metadata: new System.Collections.Generic.Dictionary<string, object> { { "button", "WaveStart" } });
    }

    private void RefreshSettlementPreview(bool canStart, StageManager stageManager, GameFlowManager gameFlowManager)
    {
        EnsureSettlementPreviewText();

        if (_settlementPreviewText == null)
            return;

        if (StageLoadContext.IsTutorial || !canStart)
        {
            _settlementPreviewText.gameObject.SetActive(false);
            return;
        }

        IncomeResourceProducer producer = ResolveIncomeResourceProducer();
        float remainingTime = GetRemainingWaveWaitTime(stageManager, gameFlowManager);
        int amount = producer != null ? producer.PreviewProductionForDuration(remainingTime) : 0;

        _settlementPreviewText.text = string.Format(_settlementPreviewFormat, amount);
        _settlementPreviewText.gameObject.SetActive(true);
    }

    private float GetRemainingWaveWaitTime(StageManager stageManager, GameFlowManager gameFlowManager)
    {
        if (stageManager != null && stageManager.IsWaitingForWaveStart)
            return stageManager.WaveStartRemainingTime;

        if (gameFlowManager != null && gameFlowManager.IsWaitingForNextWave)
            return gameFlowManager.CurrentWaveWaitRemainingTime;

        return 0f;
    }

    private IncomeResourceProducer ResolveIncomeResourceProducer()
    {
        if (_incomeResourceProducer != null)
            return _incomeResourceProducer;

        _incomeResourceProducer = FindFirstObjectByType<IncomeResourceProducer>(FindObjectsInactive.Include);
        return _incomeResourceProducer;
    }

    private void EnsureSettlementPreviewText()
    {
        if (_settlementPreviewText != null)
        {
            ApplySettlementPreviewLayout();
            ApplySettlementPreviewStyle();
            return;
        }

        RectTransform buttonRect = transform as RectTransform;
        if (buttonRect == null)
            return;

        Transform existing = transform.Find("WaveSettlementPreviewText");
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI existingText))
        {
            _settlementPreviewText = existingText;
            ApplySettlementPreviewLayout();
            ApplySettlementPreviewStyle();
            return;
        }

        GameObject textObj = new GameObject("WaveSettlementPreviewText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(buttonRect, false);
        textRect.anchorMin = new Vector2(0.5f, 1f);
        textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.pivot = new Vector2(0.5f, 0.5f);

        _settlementPreviewText = textObj.GetComponent<TextMeshProUGUI>();
        ApplySettlementPreviewLayout();
        ApplySettlementPreviewStyle();
        _settlementPreviewText.text = string.Empty;
        _settlementPreviewText.gameObject.SetActive(false);
    }

    private void ApplySettlementPreviewLayout()
    {
        if (_settlementPreviewText == null)
            return;

        RectTransform textRect = _settlementPreviewText.transform as RectTransform;
        if (textRect == null)
            return;

        textRect.anchorMin = new Vector2(0.5f, 1f);
        textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = _settlementPreviewSize;
        textRect.anchoredPosition = _settlementPreviewOffset;
    }

    private void ApplySettlementPreviewStyle()
    {
        if (_settlementPreviewText == null)
            return;

        TextMeshProUGUI buttonLabel = _button != null ? _button.GetComponentInChildren<TextMeshProUGUI>() : null;
        if (buttonLabel != null && buttonLabel.font != null)
            _settlementPreviewText.font = buttonLabel.font;

        _settlementPreviewText.fontSize = _settlementPreviewFontSize;
        _settlementPreviewText.alignment = TextAlignmentOptions.Center;
        _settlementPreviewText.color = _settlementPreviewColor;
        _settlementPreviewText.raycastTarget = false;
        _settlementPreviewText.textWrappingMode = TextWrappingModes.NoWrap;
    }
}
