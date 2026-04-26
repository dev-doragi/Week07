using System.Collections;
using TMPro;
using UnityEngine;

public class RewardPopupUI : MonoBehaviour
{
    private const string RootName = "RewardPopupRoot";
    private const string Galmuri9FontName = "Galmuri9 SDF";

    [SerializeField] private float _duration = 1.2f;
    [SerializeField] private Vector2 _startOffset = new Vector2(0f, 48f);
    [SerializeField] private Vector2 _endOffset = new Vector2(0f, 108f);
    [SerializeField] private Color _textColor = new Color(1f, 0.92f, 0.35f, 1f);

    private static RewardPopupUI _instance;
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private Camera _uiCamera;
    private TMP_FontAsset _popupFont;

    public static void ShowMouseReward(Vector3 worldPosition, int amount)
    {
        if (amount <= 0)
            return;

        RewardPopupUI popup = GetOrCreateInstance();
        if (popup != null)
            popup.Show(worldPosition, amount);
    }

    private static RewardPopupUI GetOrCreateInstance()
    {
        if (_instance != null)
            return _instance;

        Canvas canvas = FindPopupCanvas();
        if (canvas == null)
            return null;

        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(RewardPopupUI));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        _instance = root.GetComponent<RewardPopupUI>();
        _instance.Initialize(canvas);
        return _instance;
    }

    private static Canvas FindPopupCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.isActiveAndEnabled)
                return canvas;
        }

        return null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        if (_canvas == null)
            Initialize(GetComponentInParent<Canvas>());
    }

    private void Initialize(Canvas canvas)
    {
        _canvas = canvas;
        _canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        _uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        _popupFont = FindGalmuri9Font();
    }

    private static TMP_FontAsset FindGalmuri9Font()
    {
        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (TMP_FontAsset font in loadedFonts)
        {
            if (font != null && font.name == Galmuri9FontName)
                return font;
        }

        TextMeshProUGUI[] texts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TextMeshProUGUI text in texts)
        {
            if (text != null && text.font != null && text.font.name == Galmuri9FontName)
                return text.font;
        }

        return null;
    }

    private void Show(Vector3 worldPosition, int amount)
    {
        if (_canvas == null || _canvasRect == null)
            return;

        if (!TryGetAnchoredPosition(worldPosition, out Vector2 anchoredPosition))
            return;

        GameObject textObject = new GameObject("MouseRewardPopup", typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.sizeDelta = new Vector2(380f, 64f);
        rect.anchoredPosition = anchoredPosition + _startOffset;

        CanvasGroup canvasGroup = textObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = $"획득 한 쥐 +{amount}";
        text.font = _popupFont != null ? _popupFont : text.font;
        text.fontSize = 34f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = _textColor;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color(0.12f, 0.08f, 0.02f, 0.9f);

        StartCoroutine(AnimatePopup(rect, canvasGroup, anchoredPosition));
    }

    private bool TryGetAnchoredPosition(Vector3 worldPosition, out Vector2 anchoredPosition)
    {
        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            anchoredPosition = Vector2.zero;
            return false;
        }

        Vector2 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPosition, _uiCamera, out anchoredPosition);
    }

    private IEnumerator AnimatePopup(RectTransform rect, CanvasGroup canvasGroup, Vector2 basePosition)
    {
        float elapsed = 0f;

        while (elapsed < _duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            if (rect != null)
                rect.anchoredPosition = Vector2.Lerp(basePosition + _startOffset, basePosition + _endOffset, eased);

            if (canvasGroup != null)
                canvasGroup.alpha = 1f - Mathf.SmoothStep(0.55f, 1f, t);

            yield return null;
        }

        if (rect != null)
            Destroy(rect.gameObject);
    }
}
