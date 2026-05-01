using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class IncomeBlockPrefabEntry
{
    public IncomeBlockType Type;
    public IncomeBlockPiece Prefab;
}

[Serializable]
public class CoreBlockSprites
{
    [Header("CoreSquare: 2x2 (4개)")]
    public Sprite SquareTopLeft;
    public Sprite SquareTopRight;
    public Sprite SquareBottomLeft;
    public Sprite SquareBottomRight;

    [Header("CoreCross: 십자 (5개)")]
    public Sprite CrossCenter;
    public Sprite CrossTop;
    public Sprite CrossBottom;
    public Sprite CrossLeft;
    public Sprite CrossRight;
}

/// <summary>
/// 인벤토리 내 블록 생성/배치(줄바꿈 포함)와 랜덤 획득 흐름을 담당한다.
/// </summary>
public class IncomeInventory : MonoBehaviour
{
    private const string DefaultBlockPrefabFolder = "Assets/02.Prefabs/Income/Blocks";
    private const string DefaultCoreCrossBlockPrefabPath = "Assets/02.Prefabs/Income/Blocks/Income_CoreCross.prefab";
    private const string DefaultCoreSquareBlockPrefabPath = "Assets/02.Prefabs/Income/Blocks/Income_CoreSquare.prefab";

    private static readonly IncomeBlockType[] AllBlockTypes =
    {
        IncomeBlockType.I,
        IncomeBlockType.J,
        IncomeBlockType.L,
        IncomeBlockType.O,
        IncomeBlockType.S,
        IncomeBlockType.T,
        IncomeBlockType.Z
    };

    [Header("References")]
    [SerializeField] private IncomeGridBoard _gridBoard;
    [SerializeField] private RectTransform _inventoryRoot;
    [SerializeField] private RectTransform _layoutWidthReference;
    [SerializeField] private RectTransform _dragRoot;
    [SerializeField] private ScrollRect _scrollRect;

    [Header("Start Reward")]
    [SerializeField] private bool _spawnInitialOnStart = true;
    [SerializeField] private IncomeBlockPiece _coreCrossBlockPrefab;
    [SerializeField] private IncomeBlockPiece _coreSquareBlockPrefab;

    [Header("Layout")]
    [SerializeField] private Vector2 _startPosition = new Vector2(20f, 20f);
    [SerializeField] private float _spacing = 20f;
    [SerializeField] private bool _autoScrollToNewBlock = true;

    [Header("Prefabs")]
    [SerializeField] private IncomeBlockPiece _fallbackBlockPrefab;
    [SerializeField] private List<IncomeBlockPrefabEntry> _blockPrefabs = new();

    [Header("Tetromino Outline Colors")]
    [SerializeField] private Color _iColor = new Color(0.35f, 0.95f, 0.95f, 0.95f);
    [SerializeField] private Color _jColor = new Color(0.35f, 0.55f, 0.95f, 0.95f);
    [SerializeField] private Color _lColor = new Color(0.95f, 0.65f, 0.30f, 0.95f);
    [SerializeField] private Color _oColor = new Color(0.95f, 0.90f, 0.35f, 0.95f);
    [SerializeField] private Color _sColor = new Color(0.45f, 0.90f, 0.45f, 0.95f);
    [SerializeField] private Color _tColor = new Color(0.80f, 0.45f, 0.90f, 0.95f);
    [SerializeField] private Color _zColor = new Color(0.95f, 0.40f, 0.40f, 0.95f);
    [SerializeField] private Color _coreCrossColor = new Color(0.95f, 0.95f, 0.95f, 0.95f);
    [SerializeField] private Color _coreSquareColor = new Color(0.95f, 0.85f, 0.45f, 0.95f);

    private readonly List<IncomeBlockPiece> _spawnedPieces = new();
    private bool _layoutInitialized;
    private Vector2 _nextCursor;
    private float _currentRowMaxHeight;
    private readonly List<IncomeBlockPiece> _fixedPieces = new();
    
    private void Awake()
    {
        if (_inventoryRoot == null)
            _inventoryRoot = transform as RectTransform;

        if (_layoutWidthReference == null)
            _layoutWidthReference = _inventoryRoot;

        if (_dragRoot == null && _inventoryRoot != null)
            _dragRoot = _inventoryRoot.root as RectTransform;
    }

    private void Start()
    {
        if (_spawnInitialOnStart)
            SpawnInitialBlock();
    }

    public void SetAllPiecesInteractionLocked(bool locked)
    {
        for(int i = 0; i < _spawnedPieces.Count; i++)
        {
            if(_spawnedPieces[i] == null) continue;
            if(_fixedPieces.Contains(_spawnedPieces[i])) continue;    
            _spawnedPieces[i].SetInteractionLocked(locked);
        }
    }

    [ContextMenu("Spawn Initial Block")]
    public void SpawnInitialBlock()
    {
        if (_spawnedPieces.Count > 0)
            return;

        AddFixedStartBlockToGrid();
    }

    [ContextMenu("Acquire Random Block")]
    public void AcquireRandomBlockFromButton()
    {
        // UI Button OnClick에서 바로 연결하는 진입점.
        AcquireRandomBlock();
    }

    public IncomeBlockPiece AcquireRandomBlock()
    {
        int randomIndex = UnityEngine.Random.Range(0, AllBlockTypes.Length);
        var type = AllBlockTypes[randomIndex];
        return AddBlock(type);
    }

    public IncomeBlockPiece AddBlock(IncomeBlockType type)
    {
        if (_inventoryRoot == null)
        {
            Debug.LogWarning("[IncomeInventory] InventoryRoot is not assigned.");
            return null;
        }

        EnsureLayoutInitialized();

        float cellSize = _gridBoard != null ? _gridBoard.CellSize : 72f;
        Vector2 pieceSize = CalculatePieceSize(type, cellSize);
        Vector2 spawnPosition = GetNextSpawnPosition(pieceSize);

        var piece = CreateBlock(type, _inventoryRoot, spawnPosition, GetColor(type));
        if (piece == null)
            return null;

        AdvanceLayout(pieceSize);
        EnsureContentHeight();
        ScrollToBlock(spawnPosition, pieceSize);

        return piece;
    }

    // 이전 버전과의 호환을 위한 진입점.
    public void SpawnStarterBlocks()
    {
        SpawnInitialBlock();
    }

    public void SetGridBoard(IncomeGridBoard gridBoard)
    {
        _gridBoard = gridBoard;
    }

    public void SetDragRoot(RectTransform dragRoot)
    {
        _dragRoot = dragRoot;
    }

    public void SetInventoryRoot(RectTransform inventoryRoot)
    {
        _inventoryRoot = inventoryRoot;
        if (_layoutWidthReference == null)
            _layoutWidthReference = _inventoryRoot;

        ResetLayoutCursor();
    }

    public void SetLayoutWidthReference(RectTransform layoutWidthReference)
    {
        _layoutWidthReference = layoutWidthReference;
        ResetLayoutCursor();
    }

    public void SetScrollRect(ScrollRect scrollRect)
    {
        _scrollRect = scrollRect;
    }

    private IncomeBlockPiece CreateBlock(IncomeBlockType type, RectTransform parent, Vector2 homePosition, Color color)
    {
        IncomeBlockPiece piece = null;
        RectTransform spawnParent = parent != null ? parent : _inventoryRoot;

        var prefab = ResolvePrefab(type);
        if (prefab != null)
        {
            piece = Instantiate(prefab, spawnParent, false);
        }
        else if (_fallbackBlockPrefab != null)
        {
            piece = Instantiate(_fallbackBlockPrefab, spawnParent, false);
        }
        else
        {
            var pieceGo = new GameObject($"Income_{type}",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(IncomeBlockPiece));
            var rect = pieceGo.GetComponent<RectTransform>();
            rect.SetParent(spawnParent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            piece = pieceGo.GetComponent<IncomeBlockPiece>();
        }

        if (piece == null)
            return null;

        piece.name = $"Income_{type}";
        piece.Initialize(type, _gridBoard, _dragRoot, spawnParent, homePosition, color);

        _spawnedPieces.Add(piece);
        return piece;
    }

    private void AddFixedStartBlockToGrid()
    {
        if (_gridBoard == null || _gridBoard.GridRoot == null)
        {
            Debug.LogWarning("[IncomeInventory] GridBoard is not ready. Fixed start block was not placed.");
            return;
        }

        IncomeBlockType coreBlockType = ResolveCoreBlockTypeForGrid();
        Vector2Int origin = CalculateCenteredOrigin(coreBlockType);
        Vector2 anchoredPosition = _gridBoard.GetAnchoredPositionFromOrigin(origin);
        var piece = CreateBlock(coreBlockType, _gridBoard.GridRoot, anchoredPosition, GetColor(coreBlockType));
        if (piece == null)
            return;

        if (!piece.ForcePlaceOnGrid(origin))
        {
            Debug.LogWarning("[IncomeInventory] Fixed start block could not be placed on the grid.");
            Destroy(piece.gameObject);
            _spawnedPieces.Remove(piece);
            return;
        }

        piece.SetInteractionLocked(true);
        _fixedPieces.Add(piece);
    }

    private IncomeBlockPiece ResolvePrefab(IncomeBlockType type)
    {
        if (type == IncomeBlockType.CoreCross && _coreCrossBlockPrefab != null)
            return _coreCrossBlockPrefab;

        if (type == IncomeBlockType.CoreSquare && _coreSquareBlockPrefab != null)
            return _coreSquareBlockPrefab;

        if (_blockPrefabs == null)
            return null;

        for (int i = 0; i < _blockPrefabs.Count; i++)
        {
            var entry = _blockPrefabs[i];
            if (entry != null && entry.Type == type && entry.Prefab != null)
                return entry.Prefab;
        }

        return null;
    }

    private void EnsureLayoutInitialized()
    {
        if (_layoutInitialized)
            return;

        _nextCursor = _startPosition;
        _currentRowMaxHeight = 0f;
        _layoutInitialized = true;
    }

    private void ResetLayoutCursor()
    {
        _layoutInitialized = false;
        _nextCursor = _startPosition;
        _currentRowMaxHeight = 0f;
    }

    private Vector2 GetNextSpawnPosition(Vector2 pieceSize)
    {
        float maxWidth = GetLayoutWidth();
        float maxX = Mathf.Max(_startPosition.x + pieceSize.x, maxWidth - _startPosition.x);

        if (_nextCursor.x > _startPosition.x && _nextCursor.x + pieceSize.x > maxX)
        {
            _nextCursor.x = _startPosition.x;
            _nextCursor.y += _currentRowMaxHeight + _spacing;
            _currentRowMaxHeight = 0f;
        }

        return _nextCursor;
    }

    private void AdvanceLayout(Vector2 pieceSize)
    {
        _nextCursor.x += pieceSize.x + _spacing;
        if (pieceSize.y > _currentRowMaxHeight)
            _currentRowMaxHeight = pieceSize.y;
    }

    private void EnsureContentHeight()
    {
        if (_inventoryRoot == null)
            return;

        var size = _inventoryRoot.sizeDelta;

        float minWidth = GetLayoutWidth();
        float requiredHeight = _nextCursor.y + _currentRowMaxHeight + _spacing;
        float currentHeight = _inventoryRoot.rect.height > 1f ? _inventoryRoot.rect.height : size.y;

        size.x = Mathf.Max(size.x, minWidth);
        size.y = Mathf.Max(currentHeight, requiredHeight);

        _inventoryRoot.sizeDelta = size;
    }

    private void ScrollToBlock(Vector2 blockPosition, Vector2 blockSize)
    {
        if (!_autoScrollToNewBlock)
            return;

        var scrollRect = ResolveScrollRect();
        if (scrollRect == null || scrollRect.viewport == null || scrollRect.content == null)
            return;

        Canvas.ForceUpdateCanvases();

        float contentHeight = scrollRect.content.rect.height;
        float viewportHeight = scrollRect.viewport.rect.height;
        float scrollableHeight = contentHeight - viewportHeight;
        if (scrollableHeight <= 0.01f)
            return;

        float blockCenterY = blockPosition.y + (blockSize.y * 0.5f);
        float targetFromBottom = blockCenterY - (viewportHeight * 0.5f);
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(targetFromBottom / scrollableHeight);
    }

    private ScrollRect ResolveScrollRect()
    {
        if (_scrollRect != null)
            return _scrollRect;

        if (_inventoryRoot != null)
            _scrollRect = _inventoryRoot.GetComponentInParent<ScrollRect>();

        return _scrollRect;
    }

    private float GetLayoutWidth()
    {
        if (_layoutWidthReference != null && _layoutWidthReference.rect.width > 1f)
            return _layoutWidthReference.rect.width;

        if (_inventoryRoot != null && _inventoryRoot.rect.width > 1f)
            return _inventoryRoot.rect.width;

        return 640f;
    }

    private Vector2 CalculatePieceSize(IncomeBlockType type, float cellSize)
    {
        var cells = IncomeShapeLibrary.GetBaseCells(type);

        int maxX = 0;
        int maxY = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y > maxY) maxY = cell.y;
        }

        return new Vector2((maxX + 1) * cellSize, (maxY + 1) * cellSize);
    }

    private Color GetColor(IncomeBlockType type)
    {
        return type switch
        {
            IncomeBlockType.I => _iColor,
            IncomeBlockType.J => _jColor,
            IncomeBlockType.L => _lColor,
            IncomeBlockType.O => _oColor,
            IncomeBlockType.S => _sColor,
            IncomeBlockType.T => _tColor,
            IncomeBlockType.Z => _zColor,
            IncomeBlockType.CoreCross => _coreCrossColor,
            IncomeBlockType.CoreSquare => _coreSquareColor,
            _ => Color.white
        };
    }

    private IncomeBlockType ResolveCoreBlockTypeForGrid()
    {
        // 그리드 가로 길이가 짝수면 스퀘어, 홀수면 크로스
        if (_gridBoard != null && _gridBoard.Width % 2 == 0)
            return IncomeBlockType.CoreSquare;

        return IncomeBlockType.CoreCross;
    }

    private Vector2Int CalculateCenteredOrigin(IncomeBlockType type)
    {
        var cells = IncomeShapeLibrary.GetBaseCells(type);
        int maxX = 0;
        int maxY = 0;

        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y > maxY) maxY = cell.y;
        }

        int shapeWidth = maxX + 1;
        int shapeHeight = maxY + 1;
        int originX = Mathf.Max(0, (_gridBoard.Width - shapeWidth) / 2);
        int originY = Mathf.Max(0, (_gridBoard.Height - shapeHeight) / 2);

        return new Vector2Int(originX, originY);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if ((_blockPrefabs == null || _blockPrefabs.Count == 0) && AssetDatabase.IsValidFolder(DefaultBlockPrefabFolder))
        {
            _blockPrefabs = new List<IncomeBlockPrefabEntry>();

            for (int i = 0; i < AllBlockTypes.Length; i++)
            {
                var type = AllBlockTypes[i];
                string path = $"{DefaultBlockPrefabFolder}/Income_{type}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<IncomeBlockPiece>(path);
                if (prefab == null)
                    continue;

                _blockPrefabs.Add(new IncomeBlockPrefabEntry
                {
                    Type = type,
                    Prefab = prefab
                });
            }
        }

        if (_coreCrossBlockPrefab == null)
            _coreCrossBlockPrefab = AssetDatabase.LoadAssetAtPath<IncomeBlockPiece>(DefaultCoreCrossBlockPrefabPath);

        if (_coreSquareBlockPrefab == null)
            _coreSquareBlockPrefab = AssetDatabase.LoadAssetAtPath<IncomeBlockPiece>(DefaultCoreSquareBlockPrefabPath);
    }
#endif
}
