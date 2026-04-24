using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class IncomeSystemAutoSetup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas _targetCanvas;
    [SerializeField] private IncomeGridBoard _gridBoard;
    [SerializeField] private IncomeInventory _inventory;
    [SerializeField] private IncomeResourceProducer _resourceProducer;

    [Header("Auto Build")]
    [SerializeField] private bool _buildOnAwake = true;
    [SerializeField] private bool _overridePanelTransforms = false;

    [Header("Grid Panel")]
    [SerializeField] private Vector2 _gridPanelPosition = new Vector2(-220f, 20f);
    [SerializeField] private Vector2 _defaultGridPanelSize = new Vector2(360f, 360f);

    [Header("Inventory Panel")]
    [SerializeField] private bool _matchInventorySizeToGrid = true;
    [SerializeField] private Vector2 _inventoryPanelSize = new Vector2(360f, 360f);
    [SerializeField] private float _inventoryGapFromGrid = 90f;
    [SerializeField] private float _inventoryVerticalOffset = 0f;
    [SerializeField] private Vector2 _inventoryPadding = new Vector2(12f, 12f);
    [SerializeField] private float _inventoryButtonHeight = 44f;

    [Header("Style")]
    [SerializeField] private Color _panelBackgroundColor = new Color(0f, 0f, 0f, 0.28f);
    [SerializeField] private Color _panelBorderColor = new Color(0.9f, 0.9f, 0.9f, 0.8f);
    [SerializeField] private Color _viewportBackgroundColor = new Color(0f, 0f, 0f, 0.18f);
    [SerializeField] private Color _buttonColor = new Color(0.15f, 0.55f, 0.22f, 0.92f);
    [SerializeField] private Color _buttonTextColor = Color.white;
    [SerializeField] private string _acquireButtonLabel = "랜덤 블록 획득";
    [SerializeField] private TMP_FontAsset _buttonFontAsset;

    [Header("Scrollbar")]
    [SerializeField] private float _scrollbarWidth = 14f;
    [SerializeField] private float _scrollbarGap = 8f;
    [SerializeField] private Color _scrollbarBackgroundColor = new Color(1f, 1f, 1f, 0.18f);
    [SerializeField] private Color _scrollbarHandleColor = new Color(1f, 1f, 1f, 0.82f);

    private void Awake()
    {
        if (_buildOnAwake)
            BuildIfNeeded();
    }

    [ContextMenu("Build Income UI")]
    public void BuildIfNeeded()
    {
        if (_targetCanvas == null)
        {
            _targetCanvas = FindSceneObject<Canvas>();
        }

        if (_targetCanvas == null)
        {
            Debug.LogWarning("[IncomeSystemAutoSetup] Canvas was not found in scene.");
            return;
        }

        var canvasRect = _targetCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            Debug.LogWarning("[IncomeSystemAutoSetup] Canvas RectTransform was not found.");
            return;
        }

        var gridPanel = ResolveOrCreateGridPanel(canvasRect, out bool gridPanelCreated);
        if (gridPanel == null)
            return;

        if (_gridBoard == null)
            _gridBoard = gridPanel.GetComponent<IncomeGridBoard>();
        if (_gridBoard == null)
            _gridBoard = gridPanel.gameObject.AddComponent<IncomeGridBoard>();

        if (!_gridBoard.AutoGenerateBaseCells)
            _gridBoard.SetAutoGenerateBaseCells(true);

        if (gridPanelCreated || _overridePanelTransforms)
            ConfigurePanel(gridPanel, _gridPanelPosition, _defaultGridPanelSize, _panelBackgroundColor, _panelBorderColor);

        _gridBoard.BuildVisualGrid();
        Vector2 gridSize = _gridBoard.GetBoardSize();

        Vector2 resolvedInventorySize = _matchInventorySizeToGrid ? gridSize : _inventoryPanelSize;
        Vector2 inventoryPanelPosition = new Vector2(
            _gridPanelPosition.x + (gridSize.x * 0.5f) + _inventoryGapFromGrid + (resolvedInventorySize.x * 0.5f),
            _gridPanelPosition.y + _inventoryVerticalOffset);

        var inventoryPanel = ResolveOrCreateInventoryPanel(canvasRect, out bool inventoryPanelCreated);
        if (inventoryPanel == null)
            return;

        if (inventoryPanelCreated || _overridePanelTransforms)
            ConfigurePanel(inventoryPanel, inventoryPanelPosition, resolvedInventorySize, _panelBackgroundColor, _panelBorderColor);

        var viewport = GetOrCreateRectTransform(inventoryPanel, "InventoryViewport");
        ConfigureViewportRect(viewport);
        ConfigureViewportVisual(viewport);

        var content = GetOrCreateRectTransform(viewport, "InventoryContent");
        ConfigureContentRect(content, viewport);

        var button = GetOrCreateButton(inventoryPanel, "AcquireRandomBlockButton");
        ConfigureAcquireButton(button);

        var scrollRect = inventoryPanel.GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = inventoryPanel.gameObject.AddComponent<ScrollRect>();

        var verticalScrollbar = GetOrCreateVerticalScrollbar(inventoryPanel, "InventoryVerticalScrollbar");
        ConfigureVerticalScrollbar(verticalScrollbar);

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.verticalScrollbar = verticalScrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scrollRect.verticalScrollbarSpacing = _scrollbarGap;

        if (_inventory == null)
            _inventory = inventoryPanel.GetComponent<IncomeInventory>();
        if (_inventory == null)
            _inventory = inventoryPanel.gameObject.AddComponent<IncomeInventory>();

        _inventory.SetGridBoard(_gridBoard);
        _inventory.SetDragRoot(canvasRect);
        _inventory.SetInventoryRoot(content);
        _inventory.SetLayoutWidthReference(viewport);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(_inventory.AcquireRandomBlockFromButton);

        if (_resourceProducer == null)
        {
            _resourceProducer = GetComponent<IncomeResourceProducer>();
            if (_resourceProducer == null)
                _resourceProducer = gameObject.AddComponent<IncomeResourceProducer>();
        }

        _resourceProducer.SetGridBoard(_gridBoard);

        var resourceManager = FindSceneObject<ResourceManager>();
        _resourceProducer.SetResourceManager(resourceManager);
    }

    private RectTransform ResolveOrCreateGridPanel(RectTransform canvasRect, out bool created)
    {
        if (_gridBoard != null)
        {
            created = false;
            return _gridBoard.transform as RectTransform;
        }

        _gridBoard = FindSceneObject<IncomeGridBoard>();
        if (_gridBoard != null)
        {
            created = false;
            return _gridBoard.transform as RectTransform;
        }

        return GetOrCreatePanel(canvasRect, "IncomeGridPanel", out created);
    }

    private RectTransform ResolveOrCreateInventoryPanel(RectTransform canvasRect, out bool created)
    {
        if (_inventory != null)
        {
            created = false;
            return _inventory.transform as RectTransform;
        }

        _inventory = FindSceneObject<IncomeInventory>();
        if (_inventory != null)
        {
            created = false;
            return _inventory.transform as RectTransform;
        }

        return GetOrCreatePanel(canvasRect, "IncomeInventoryPanel", out created);
    }

    private RectTransform GetOrCreatePanel(RectTransform parent, string panelName, out bool created)
    {
        Transform existing = parent.Find(panelName);
        if (existing != null)
        {
            created = false;
            return existing as RectTransform;
        }

        created = true;
        var panelGo = new GameObject(panelName, typeof(RectTransform), typeof(Image));
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        ConfigurePanel(panelRect, Vector2.zero, panelRect.sizeDelta, _panelBackgroundColor, _panelBorderColor);
        return panelRect;
    }

    private void ConfigurePanel(RectTransform panelRect, Vector2 anchoredPosition, Vector2 size, Color backgroundColor, Color borderColor)
    {
        if (panelRect == null)
            return;

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = size;

        var image = panelRect.GetComponent<Image>();
        if (image == null)
            image = panelRect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.color = backgroundColor;

        var outline = panelRect.GetComponent<Outline>();
        if (outline == null)
            outline = panelRect.gameObject.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
    }

    private RectTransform GetOrCreateRectTransform(RectTransform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
            return existing as RectTransform;

        var childGo = new GameObject(childName, typeof(RectTransform));
        var childRect = childGo.GetComponent<RectTransform>();
        childRect.SetParent(parent, false);
        return childRect;
    }

    private void ConfigureViewportRect(RectTransform viewport)
    {
        viewport.anchorMin = new Vector2(0f, 0f);
        viewport.anchorMax = new Vector2(1f, 1f);
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.offsetMin = new Vector2(_inventoryPadding.x, _inventoryPadding.y * 2f + _inventoryButtonHeight);
        viewport.offsetMax = new Vector2(-(_inventoryPadding.x + _scrollbarWidth + _scrollbarGap), -_inventoryPadding.y);
    }

    private void ConfigureViewportVisual(RectTransform viewport)
    {
        var image = viewport.GetComponent<Image>();
        if (image == null)
            image = viewport.gameObject.AddComponent<Image>();
        image.raycastTarget = true;
        image.color = _viewportBackgroundColor;

        var mask = viewport.GetComponent<RectMask2D>();
        if (mask == null)
            viewport.gameObject.AddComponent<RectMask2D>();
    }

    private void ConfigureContentRect(RectTransform content, RectTransform viewport)
    {
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 0f);
        content.pivot = new Vector2(0f, 0f);
        content.anchoredPosition = Vector2.zero;

        float width = viewport.rect.width > 1f ? viewport.rect.width : 300f;
        float height = viewport.rect.height > 1f ? viewport.rect.height : 300f;
        content.sizeDelta = new Vector2(width, height);
    }

    private Button GetOrCreateButton(RectTransform panel, string buttonName)
    {
        Transform existing = panel.Find(buttonName);
        if (existing != null && existing.TryGetComponent<Button>(out var existingButton))
            return existingButton;

        var buttonGo = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = buttonGo.GetComponent<RectTransform>();
        rect.SetParent(panel, false);

        var button = buttonGo.GetComponent<Button>();

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    private void ConfigureAcquireButton(Button button)
    {
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(-(_inventoryPadding.x * 2f + _scrollbarWidth + _scrollbarGap), _inventoryButtonHeight);
        rect.anchoredPosition = new Vector2(0f, _inventoryPadding.y);

        var image = button.GetComponent<Image>();
        image.color = _buttonColor;
        image.raycastTarget = true;

        var tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = _acquireButtonLabel;
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = _buttonTextColor;
            tmp.raycastTarget = false;
            if (_buttonFontAsset != null)
                tmp.font = _buttonFontAsset;
        }
    }

    private Scrollbar GetOrCreateVerticalScrollbar(RectTransform panel, string scrollbarName)
    {
        Transform existing = panel.Find(scrollbarName);
        if (existing != null && existing.TryGetComponent<Scrollbar>(out var existingScrollbar))
            return existingScrollbar;

        var scrollbarGo = new GameObject(scrollbarName, typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        var scrollbarRect = scrollbarGo.GetComponent<RectTransform>();
        scrollbarRect.SetParent(panel, false);

        var slidingAreaGo = new GameObject("SlidingArea", typeof(RectTransform));
        var slidingAreaRect = slidingAreaGo.GetComponent<RectTransform>();
        slidingAreaRect.SetParent(scrollbarRect, false);

        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        var handleRect = handleGo.GetComponent<RectTransform>();
        handleRect.SetParent(slidingAreaRect, false);

        var scrollbar = scrollbarGo.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleGo.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        return scrollbar;
    }

    private void ConfigureVerticalScrollbar(Scrollbar scrollbar)
    {
        if (scrollbar == null)
            return;

        var rect = scrollbar.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(_scrollbarWidth, -(_inventoryPadding.y * 2f + _inventoryButtonHeight));
        rect.anchoredPosition = new Vector2(-_inventoryPadding.x, (_inventoryButtonHeight + _inventoryPadding.y) * 0.5f);

        var background = scrollbar.GetComponent<Image>();
        if (background != null)
        {
            background.color = _scrollbarBackgroundColor;
            background.raycastTarget = true;
        }

        var slidingArea = scrollbar.transform.Find("SlidingArea") as RectTransform;
        if (slidingArea != null)
        {
            slidingArea.anchorMin = Vector2.zero;
            slidingArea.anchorMax = Vector2.one;
            slidingArea.offsetMin = new Vector2(2f, 2f);
            slidingArea.offsetMax = new Vector2(-2f, -2f);
        }

        if (scrollbar.handleRect != null)
        {
            scrollbar.handleRect.anchorMin = Vector2.zero;
            scrollbar.handleRect.anchorMax = Vector2.one;
            scrollbar.handleRect.offsetMin = Vector2.zero;
            scrollbar.handleRect.offsetMax = Vector2.zero;

            var handleImage = scrollbar.handleRect.GetComponent<Image>();
            if (handleImage != null)
            {
                handleImage.color = _scrollbarHandleColor;
                handleImage.raycastTarget = true;
            }
        }
    }

    private static T FindSceneObject<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_buttonFontAsset == null)
        {
            string[] guids = AssetDatabase.FindAssets("Galmuri9 t:TMP_FontAsset");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _buttonFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            }
        }
    }
#endif
}
