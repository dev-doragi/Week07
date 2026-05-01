using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StageMapRewardToastUI : MonoBehaviour
{
    [SerializeField] private Canvas _targetCanvas;
    [SerializeField] private RectTransform _toastRoot;
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private RectTransform _rewardRowsRoot;
    [SerializeField] private List<Image> _rewardIconImages = new List<Image>();
    [SerializeField] private List<TextMeshProUGUI> _rewardMessageTexts = new List<TextMeshProUGUI>();
    [SerializeField] private TMP_FontAsset _messageFont;
    [SerializeField] private float _visibleDuration = 2.2f;
    [Header("Default Toast Layout")]
    [SerializeField] private Vector2 _toastSize = new Vector2(520f, 120f);
    [SerializeField] private Color _backgroundColor = new Color(0.125f, 0.125f, 0.125f, 0.94f);
    [SerializeField] private Vector2 _iconSize = new Vector2(72f, 72f);
    [SerializeField] private Vector2 _iconPosition = new Vector2(68f, 0f);
    [SerializeField] private Vector4 _messagePadding = new Vector4(128f, 18f, 24f, 18f);
    [SerializeField] private float _messageFontSize = 28f;
    [SerializeField] private Color _messageColor = Color.white;

    private readonly System.Collections.Generic.List<StageMapRewardAppliedEvent> _pendingRewards = new System.Collections.Generic.List<StageMapRewardAppliedEvent>();
    private Coroutine _showRoutine;

    private void Awake()
    {
        if (Application.isPlaying && _toastRoot != null)
            _toastRoot.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<StageMapRewardAppliedEvent>(OnRewardApplied);
            EventBus.Instance.Subscribe<StageLoadedEvent>(OnStageLoaded);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<StageMapRewardAppliedEvent>(OnRewardApplied);
            EventBus.Instance.Unsubscribe<StageLoadedEvent>(OnStageLoaded);
        }
    }

    private void OnRewardApplied(StageMapRewardAppliedEvent evt)
    {
        if (!string.IsNullOrEmpty(evt.DisplayName))
            _pendingRewards.Add(evt);
    }

    private void OnStageLoaded(StageLoadedEvent evt)
    {
        if (_pendingRewards.Count == 0)
            return;

        Show(_pendingRewards);
        _pendingRewards.Clear();
    }

    private void Show(System.Collections.Generic.List<StageMapRewardAppliedEvent> rewards)
    {
        EnsureToast();
        ApplyFontIfNeeded();

        if (_toastRoot == null)
            return;

        if (_showRoutine != null)
            StopCoroutine(_showRoutine);

        _toastRoot.sizeDelta = new Vector2(_toastSize.x, _toastSize.y + Mathf.Max(0, rewards.Count - 1) * 80f);
        EnsureRewardRows(rewards.Count);
        ApplyRewardsToRows(rewards);
        _toastRoot.gameObject.SetActive(true);
        _showRoutine = StartCoroutine(HideAfterDelay());
    }

    private void ApplyRewardsToRows(System.Collections.Generic.List<StageMapRewardAppliedEvent> rewards)
    {
        for (int i = 0; i < _rewardMessageTexts.Count; i++)
        {
            bool hasReward = i < rewards.Count;
            Transform row = _rewardMessageTexts[i] != null ? _rewardMessageTexts[i].transform.parent : null;
            if (row != null)
                row.gameObject.SetActive(hasReward);

            if (_rewardMessageTexts[i] != null)
            {
                _rewardMessageTexts[i].text = hasReward ? rewards[i].DisplayName : string.Empty;
                _rewardMessageTexts[i].gameObject.SetActive(hasReward);
            }

            if (i < _rewardIconImages.Count && _rewardIconImages[i] != null)
            {
                _rewardIconImages[i].sprite = hasReward ? rewards[i].Icon : null;
                _rewardIconImages[i].enabled = hasReward && rewards[i].Icon != null;
                _rewardIconImages[i].gameObject.SetActive(hasReward);
            }
        }
    }

    [ContextMenu("Build Reward Toast UI")]
    public void BuildRewardToastUI()
    {
        EnsureCanvas();
        EnsureToast();
        ApplyFontIfNeeded();
        EnsureRewardRows(2);

        if (_toastRoot != null && !Application.isPlaying)
            _toastRoot.gameObject.SetActive(true);
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(_visibleDuration);

        if (_toastRoot != null)
            _toastRoot.gameObject.SetActive(false);

        _showRoutine = null;
    }

    private void EnsureToast()
    {
        if (_toastRoot != null)
        {
            ResolveToastReferences();
            EnsureRewardRows(2);
            return;
        }

        EnsureCanvas();
        if (_targetCanvas == null)
            return;

        Transform existing = _targetCanvas.transform.Find("StageMapRewardToast");
        if (existing != null && existing.TryGetComponent(out RectTransform existingRoot))
        {
            _toastRoot = existingRoot;
            ResolveToastReferences();
            return;
        }

        GameObject rootObj = new GameObject("StageMapRewardToast", typeof(RectTransform), typeof(Image));
        _toastRoot = rootObj.GetComponent<RectTransform>();
        _toastRoot.SetParent(_targetCanvas.transform, false);
        _toastRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _toastRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _toastRoot.pivot = new Vector2(0.5f, 0.5f);
        _toastRoot.sizeDelta = _toastSize;
        _toastRoot.anchoredPosition = Vector2.zero;

        Image background = rootObj.GetComponent<Image>();
        background.color = _backgroundColor;
        background.raycastTarget = false;

        GameObject rowsObj = new GameObject("RewardRows", typeof(RectTransform));
        _rewardRowsRoot = rowsObj.GetComponent<RectTransform>();
        _rewardRowsRoot.SetParent(_toastRoot, false);
        _rewardRowsRoot.anchorMin = Vector2.zero;
        _rewardRowsRoot.anchorMax = Vector2.one;
        _rewardRowsRoot.offsetMin = Vector2.zero;
        _rewardRowsRoot.offsetMax = Vector2.zero;

        EnsureRewardRows(2);
        ApplyFontIfNeeded();

        _toastRoot.gameObject.SetActive(false);
    }

    private void ResolveToastReferences()
    {
        if (_toastRoot == null)
            return;

        if (_rewardRowsRoot == null)
        {
            Transform rows = _toastRoot.Find("RewardRows");
            if (rows != null)
                _rewardRowsRoot = rows as RectTransform;
        }

        ResolveLegacyReferences();
        ResolveRewardRowReferences();

        if (_iconImage != null)
            _iconImage.gameObject.SetActive(false);

        if (_messageText != null)
            _messageText.gameObject.SetActive(false);
    }

    private void ResolveLegacyReferences()
    {
        if (_toastRoot == null)
            return;

        if (_iconImage == null)
        {
            Transform icon = _toastRoot.Find("Icon");
            if (icon != null)
                _iconImage = icon.GetComponent<Image>();
        }

        if (_messageText == null)
        {
            Transform message = _toastRoot.Find("Message");
            if (message != null)
                _messageText = message.GetComponent<TextMeshProUGUI>();
        }
    }

    private void ResolveRewardRowReferences()
    {
        if (_rewardRowsRoot == null)
            return;

        _rewardIconImages.Clear();
        _rewardMessageTexts.Clear();

        for (int i = 0; i < _rewardRowsRoot.childCount; i++)
        {
            Transform row = _rewardRowsRoot.GetChild(i);
            Transform icon = row.Find("Icon");
            Transform message = row.Find("Message");

            if (icon != null && icon.TryGetComponent(out Image iconImage) && !_rewardIconImages.Contains(iconImage))
                _rewardIconImages.Add(iconImage);

            if (message != null && message.TryGetComponent(out TextMeshProUGUI messageText) && !_rewardMessageTexts.Contains(messageText))
                _rewardMessageTexts.Add(messageText);
        }
    }

    private void EnsureRewardRows(int count)
    {
        if (_toastRoot == null)
            return;

        if (_rewardRowsRoot == null)
        {
            Transform rows = _toastRoot.Find("RewardRows");
            if (rows != null)
                _rewardRowsRoot = rows as RectTransform;
        }

        if (_rewardRowsRoot == null)
        {
            GameObject rowsObj = new GameObject("RewardRows", typeof(RectTransform));
            _rewardRowsRoot = rowsObj.GetComponent<RectTransform>();
            _rewardRowsRoot.SetParent(_toastRoot, false);
            _rewardRowsRoot.anchorMin = Vector2.zero;
            _rewardRowsRoot.anchorMax = Vector2.one;
            _rewardRowsRoot.offsetMin = Vector2.zero;
            _rewardRowsRoot.offsetMax = Vector2.zero;
        }

        ResolveRewardRowReferences();
        for (int i = _rewardMessageTexts.Count; i < count; i++)
            CreateRewardRow(i);

        ApplyFontIfNeeded();
    }

    private void CreateRewardRow(int index)
    {
        float rowHeight = 72f;
        float spacing = 8f;
        float y = (rowHeight + spacing) * (0.5f - index);

        GameObject rowObj = new GameObject($"RewardRow{index}", typeof(RectTransform));
        RectTransform rowRect = rowObj.GetComponent<RectTransform>();
        rowRect.SetParent(_rewardRowsRoot, false);
        rowRect.anchorMin = new Vector2(0f, 0.5f);
        rowRect.anchorMax = new Vector2(1f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(0f, rowHeight);
        rowRect.anchoredPosition = new Vector2(0f, y);

        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.SetParent(rowRect, false);
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = _iconSize;
        iconRect.anchoredPosition = _iconPosition;

        Image icon = iconObj.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        _rewardIconImages.Add(icon);

        GameObject textObj = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(rowRect, false);
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(_messagePadding.x, _messagePadding.w);
        textRect.offsetMax = new Vector2(-_messagePadding.z, -_messagePadding.y);

        TextMeshProUGUI message = textObj.GetComponent<TextMeshProUGUI>();
        message.fontSize = _messageFontSize;
        message.alignment = TextAlignmentOptions.MidlineLeft;
        message.color = _messageColor;
        message.raycastTarget = false;
        message.enableWordWrapping = true;
        _rewardMessageTexts.Add(message);
    }

    private void ApplyFontIfNeeded()
    {
        EnsureDefaultFont();

        if (_messageText != null && _messageFont != null && ShouldReplaceFont(_messageText.font))
            _messageText.font = _messageFont;

        for (int i = 0; i < _rewardMessageTexts.Count; i++)
        {
            TextMeshProUGUI text = _rewardMessageTexts[i];
            if (text != null && _messageFont != null && ShouldReplaceFont(text.font))
                text.font = _messageFont;
        }
    }

    private static bool ShouldReplaceFont(TMP_FontAsset currentFont)
    {
        if (currentFont == null)
            return true;

        return currentFont.name == "LiberationSans SDF";
    }

    private void EnsureDefaultFont()
    {
        if (_messageFont != null)
            return;

#if UNITY_EDITOR
        _messageFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/10.Fonts/Galmuri9 SDF.asset");
#endif
    }

    private void EnsureCanvas()
    {
        if (_targetCanvas != null)
            return;

        _targetCanvas = FindFirstObjectByType<Canvas>();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        EnsureDefaultFont();
    }

    private void OnValidate()
    {
        EnsureDefaultFont();
    }
#endif
}
