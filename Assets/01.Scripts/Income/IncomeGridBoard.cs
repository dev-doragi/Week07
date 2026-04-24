using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IncomeGridBoard : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private RectTransform _gridRoot;
    [SerializeField] private int _width = 5;
    [SerializeField] private int _height = 5;
    [SerializeField] private float _cellSize = 72f;
    [SerializeField] private float _cellPadding = 2f;
    [SerializeField] private bool _autoGenerateBaseCells = true;

    [Header("Visual")]
    [SerializeField] private bool _buildVisualCellsOnAwake = true;
    [SerializeField] private Color _emptyCellColor = new Color(0.18f, 0.18f, 0.18f, 0.75f);
    [SerializeField] private Color _occupiedCellColor = new Color(0.35f, 0.75f, 0.35f, 0.9f);
    [SerializeField] private Color _previewValidColor = new Color(0.25f, 1f, 0.25f, 0.4f);
    [SerializeField] private Color _previewInvalidColor = new Color(1f, 0.25f, 0.25f, 0.4f);

    private IncomeBlockPiece[,] _occupied;
    private Image[,] _cellImages;
    private Image[,] _previewImages;
    private readonly Dictionary<IncomeBlockPiece, List<Vector2Int>> _placements = new();
    private bool _isRebuildingVisual;

    public RectTransform GridRoot => _gridRoot;
    public float CellSize => Mathf.Min(GetCellWidth(), GetCellHeight());
    public int Width => _width;
    public int Height => _height;
    public bool AutoGenerateBaseCells => _autoGenerateBaseCells;

    private void Awake()
    {
        EnsureInitialized();

        if (_buildVisualCellsOnAwake)
            BuildVisualGrid();
    }

    [ContextMenu("Rebuild Grid Visual")]
    public void BuildVisualGrid()
    {
        EnsureInitialized();

        if (_isRebuildingVisual)
            return;

        _isRebuildingVisual = true;

        if (_gridRoot == null)
        {
            Debug.LogWarning("[IncomeGridBoard] GridRoot is not assigned.");
            _isRebuildingVisual = false;
            return;
        }

        var existingVisual = _gridRoot.Find("__IncomeGridVisual");
        if (existingVisual != null)
        {
            if (Application.isPlaying)
                Destroy(existingVisual.gameObject);
            else
                DestroyImmediate(existingVisual.gameObject);
        }

        var visualRoot = new GameObject("__IncomeGridVisual", typeof(RectTransform)).GetComponent<RectTransform>();
        visualRoot.SetParent(_gridRoot, false);
        visualRoot.anchorMin = Vector2.zero;
        visualRoot.anchorMax = Vector2.zero;
        visualRoot.pivot = Vector2.zero;
        visualRoot.anchoredPosition = Vector2.zero;
        visualRoot.SetAsFirstSibling();

        float cellWidth = GetCellWidth();
        float cellHeight = GetCellHeight();

        RectTransform baseRoot = null;
        if (_autoGenerateBaseCells)
        {
            baseRoot = new GameObject("__BaseCells", typeof(RectTransform)).GetComponent<RectTransform>();
            baseRoot.SetParent(visualRoot, false);
            baseRoot.anchorMin = Vector2.zero;
            baseRoot.anchorMax = Vector2.zero;
            baseRoot.pivot = Vector2.zero;
            baseRoot.anchoredPosition = Vector2.zero;
        }

        var previewRoot = new GameObject("__PreviewCells", typeof(RectTransform)).GetComponent<RectTransform>();
        previewRoot.SetParent(visualRoot, false);
        previewRoot.anchorMin = Vector2.zero;
        previewRoot.anchorMax = Vector2.zero;
        previewRoot.pivot = Vector2.zero;
        previewRoot.anchoredPosition = Vector2.zero;

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (_autoGenerateBaseCells && baseRoot != null)
                {
                    var cellGo = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image));
                    var cellRect = cellGo.GetComponent<RectTransform>();
                    var image = cellGo.GetComponent<Image>();

                    cellRect.SetParent(baseRoot, false);
                    cellRect.anchorMin = Vector2.zero;
                    cellRect.anchorMax = Vector2.zero;
                    cellRect.pivot = Vector2.zero;

                    var width = Mathf.Max(2f, cellWidth - (_cellPadding * 2f));
                    var height = Mathf.Max(2f, cellHeight - (_cellPadding * 2f));
                    cellRect.sizeDelta = new Vector2(width, height);
                    cellRect.anchoredPosition = new Vector2(
                        x * cellWidth + _cellPadding,
                        y * cellHeight + _cellPadding);

                    image.raycastTarget = false;
                    image.color = _emptyCellColor;

                    _cellImages[x, y] = image;
                }
                else
                {
                    _cellImages[x, y] = null;
                }

                var previewGo = new GameObject($"Preview_{x}_{y}", typeof(RectTransform), typeof(Image));
                var previewRect = previewGo.GetComponent<RectTransform>();
                var previewImage = previewGo.GetComponent<Image>();

                previewRect.SetParent(previewRoot, false);
                previewRect.anchorMin = Vector2.zero;
                previewRect.anchorMax = Vector2.zero;
                previewRect.pivot = Vector2.zero;
                previewRect.sizeDelta = new Vector2(
                    Mathf.Max(2f, cellWidth - (_cellPadding * 2f)),
                    Mathf.Max(2f, cellHeight - (_cellPadding * 2f)));
                previewRect.anchoredPosition = new Vector2(
                    x * cellWidth + _cellPadding,
                    y * cellHeight + _cellPadding);

                previewImage.raycastTarget = false;
                previewImage.color = Color.clear;

                _previewImages[x, y] = previewImage;
            }
        }

        RefreshCellVisuals();
        ClearPlacementPreview();
        RepositionPlacedBlocks();
        _isRebuildingVisual = false;
    }

    public bool TryGetCellFromScreenPoint(Vector2 screenPoint, Camera eventCamera, out Vector2Int cell)
    {
        cell = default;

        if (_gridRoot == null)
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_gridRoot, screenPoint, eventCamera, out var localPoint))
            return false;

        Vector2 localFromLowerLeft = localPoint + new Vector2(
            _gridRoot.rect.width * _gridRoot.pivot.x,
            _gridRoot.rect.height * _gridRoot.pivot.y);

        if (localFromLowerLeft.x < 0f || localFromLowerLeft.y < 0f)
            return false;

        float cellWidth = GetCellWidth();
        float cellHeight = GetCellHeight();

        if (localFromLowerLeft.x >= _width * cellWidth || localFromLowerLeft.y >= _height * cellHeight)
            return false;

        int x = Mathf.FloorToInt(localFromLowerLeft.x / cellWidth);
        int y = Mathf.FloorToInt(localFromLowerLeft.y / cellHeight);

        var result = new Vector2Int(x, y);
        if (!IsInBounds(result))
            return false;

        cell = result;
        return true;
    }

    public Vector2 GetAnchoredPositionFromOrigin(Vector2Int origin)
    {
        return new Vector2(origin.x * GetCellWidth(), origin.y * GetCellHeight());
    }

    public bool TryPlace(IncomeBlockPiece piece, Vector2Int origin, IReadOnlyList<Vector2Int> shapeCells)
    {
        if (!CanPlace(piece, origin, shapeCells))
            return false;

        RemovePlacement(piece, false);

        var placedCells = new List<Vector2Int>(shapeCells.Count);
        for (int i = 0; i < shapeCells.Count; i++)
        {
            Vector2Int cell = origin + shapeCells[i];
            _occupied[cell.x, cell.y] = piece;
            placedCells.Add(cell);
        }

        _placements[piece] = placedCells;
        RefreshCellVisuals();
        return true;
    }

    public bool CanPlace(IncomeBlockPiece piece, Vector2Int origin, IReadOnlyList<Vector2Int> shapeCells)
    {
        if (piece == null || shapeCells == null || shapeCells.Count == 0)
            return false;

        for (int i = 0; i < shapeCells.Count; i++)
        {
            Vector2Int cell = origin + shapeCells[i];
            if (!IsInBounds(cell))
                return false;

            var occupant = _occupied[cell.x, cell.y];
            if (occupant != null && occupant != piece)
                return false;
        }

        return true;
    }

    public void ShowPlacementPreview(Vector2Int origin, IReadOnlyList<Vector2Int> shapeCells, bool canPlace)
    {
        ClearPlacementPreview();

        if (shapeCells == null || _previewImages == null)
            return;

        var previewColor = canPlace ? _previewValidColor : _previewInvalidColor;

        for (int i = 0; i < shapeCells.Count; i++)
        {
            var cell = origin + shapeCells[i];
            if (!IsInBounds(cell))
                continue;

            var image = _previewImages[cell.x, cell.y];
            if (image != null)
                image.color = previewColor;
        }
    }

    public void ClearPlacementPreview()
    {
        if (_previewImages == null)
            return;

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                var image = _previewImages[x, y];
                if (image != null)
                    image.color = Color.clear;
            }
        }
    }

    public void RemovePlacement(IncomeBlockPiece piece)
    {
        RemovePlacement(piece, true);
    }

    public bool TryGetPlacementOrigin(IncomeBlockPiece piece, out Vector2Int origin)
    {
        origin = default;

        if (piece == null || !_placements.TryGetValue(piece, out var cells) || cells.Count == 0)
            return false;

        int minX = int.MaxValue;
        int minY = int.MaxValue;

        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell.x < minX) minX = cell.x;
            if (cell.y < minY) minY = cell.y;
        }

        origin = new Vector2Int(minX, minY);
        return true;
    }

    public int GetOccupiedCellCount()
    {
        int count = 0;
        foreach (var pair in _placements)
        {
            count += pair.Value.Count;
        }

        return count;
    }

    private void RemovePlacement(IncomeBlockPiece piece, bool refresh)
    {
        if (piece == null)
            return;

        if (!_placements.TryGetValue(piece, out var cells))
            return;

        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (IsInBounds(cell) && _occupied[cell.x, cell.y] == piece)
            {
                _occupied[cell.x, cell.y] = null;
            }
        }

        _placements.Remove(piece);

        if (refresh)
            RefreshCellVisuals();
    }

    private void RefreshCellVisuals()
    {
        if (_cellImages == null)
            return;

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                var image = _cellImages[x, y];
                if (image == null)
                    continue;

                bool occupied = _occupied[x, y] != null;
                image.color = occupied ? _occupiedCellColor : _emptyCellColor;
            }
        }
    }

    public Vector2 GetBoardSize()
    {
        if (_gridRoot != null && _gridRoot.rect.width > 1f && _gridRoot.rect.height > 1f)
            return _gridRoot.rect.size;

        return new Vector2(_width * _cellSize, _height * _cellSize);
    }

    public void SetAutoGenerateBaseCells(bool enabled)
    {
        if (_autoGenerateBaseCells == enabled)
            return;

        _autoGenerateBaseCells = enabled;
        BuildVisualGrid();
    }

    private void OnRectTransformDimensionsChange()
    {
        EnsureInitialized();

        if (_gridRoot == null || _gridRoot != transform as RectTransform)
            return;

        if (!isActiveAndEnabled)
            return;

        BuildVisualGrid();
    }

    private void RepositionPlacedBlocks()
    {
        foreach (var pair in _placements)
        {
            if (pair.Key == null)
                continue;

            if (!TryGetPlacementOrigin(pair.Key, out var origin))
                continue;

            var rect = pair.Key.transform as RectTransform;
            if (rect == null)
                continue;

            rect.SetParent(_gridRoot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = GetAnchoredPositionFromOrigin(origin);
        }
    }

    private float GetCellWidth()
    {
        if (_gridRoot != null && _gridRoot.rect.width > 1f)
            return _gridRoot.rect.width / _width;

        return _cellSize;
    }

    private float GetCellHeight()
    {
        if (_gridRoot != null && _gridRoot.rect.height > 1f)
            return _gridRoot.rect.height / _height;

        return _cellSize;
    }

    private bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < _width && cell.y >= 0 && cell.y < _height;
    }

    private void EnsureInitialized()
    {
        if (_gridRoot == null)
            _gridRoot = transform as RectTransform;

        if (_width <= 0) _width = 5;
        if (_height <= 0) _height = 5;
        if (_cellSize <= 1f) _cellSize = 72f;

        bool sizeMismatch =
            _occupied == null || _occupied.GetLength(0) != _width || _occupied.GetLength(1) != _height ||
            _cellImages == null || _cellImages.GetLength(0) != _width || _cellImages.GetLength(1) != _height ||
            _previewImages == null || _previewImages.GetLength(0) != _width || _previewImages.GetLength(1) != _height;

        if (sizeMismatch)
        {
            _occupied = new IncomeBlockPiece[_width, _height];
            _cellImages = new Image[_width, _height];
            _previewImages = new Image[_width, _height];
        }

    }
}
