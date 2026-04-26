using System.Collections;
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

    private StageMapRewardAppliedEvent? _pendingReward;
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
            EventBus.Instance.Subscribe<StageMapNodeSelectedEvent>(OnMapNodeSelected);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<StageMapRewardAppliedEvent>(OnRewardApplied);
            EventBus.Instance.Unsubscribe<StageMapNodeSelectedEvent>(OnMapNodeSelected);
        }
    }

    private void OnRewardApplied(StageMapRewardAppliedEvent evt)
    {
        _pendingReward = evt;
    }

    private void OnMapNodeSelected(StageMapNodeSelectedEvent evt)
    {
        if (!_pendingReward.HasValue)
            return;

        StageMapRewardAppliedEvent reward = _pendingReward.Value;
        _pendingReward = null;

        if (string.IsNullOrEmpty(reward.DisplayName))
            return;

        Show(reward);
    }

    private void Show(StageMapRewardAppliedEvent reward)
    {
        EnsureToast();
        ApplyFontIfNeeded();

        if (_toastRoot == null || _messageText == null)
            return;

        if (_showRoutine != null)
            StopCoroutine(_showRoutine);

        if (_iconImage != null)
        {
            _iconImage.sprite = reward.Icon;
            _iconImage.enabled = reward.Icon != null;
        }

        _messageText.text = reward.DisplayName;
        _toastRoot.gameObject.SetActive(true);
        _showRoutine = StartCoroutine(HideAfterDelay());
    }

    [ContextMenu("Build Reward Toast UI")]
    public void BuildRewardToastUI()
    {
        EnsureCanvas();
        EnsureToast();
        ApplyFontIfNeeded();

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

        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.SetParent(_toastRoot, false);
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = _iconSize;
        iconRect.anchoredPosition = _iconPosition;
        _iconImage = iconObj.GetComponent<Image>();
        _iconImage.preserveAspect = true;
        _iconImage.raycastTarget = false;

        GameObject textObj = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(_toastRoot, false);
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(_messagePadding.x, _messagePadding.w);
        textRect.offsetMax = new Vector2(-_messagePadding.z, -_messagePadding.y);
        _messageText = textObj.GetComponent<TextMeshProUGUI>();
        _messageText.fontSize = _messageFontSize;
        _messageText.alignment = TextAlignmentOptions.MidlineLeft;
        _messageText.color = _messageColor;
        _messageText.raycastTarget = false;
        ApplyFontIfNeeded();

        _toastRoot.gameObject.SetActive(false);
    }

    private void ResolveToastReferences()
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

    private void ApplyFontIfNeeded()
    {
        if (_messageText == null)
            return;

        EnsureDefaultFont();
        if (_messageFont != null && ShouldReplaceFont(_messageText.font))
            _messageText.font = _messageFont;
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
