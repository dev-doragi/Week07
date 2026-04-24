using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class IncomeBlockPiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Data")]
    [SerializeField] private IncomeBlockType _blockType = IncomeBlockType.T;

    [Header("References")]
    [SerializeField] private IncomeGridBoard _gridBoard;
    [SerializeField] private RectTransform _dragRoot;

    [Header("Visual")]
    [SerializeField] private Color _blockColor = new Color(0.72f, 0.88f, 1f, 0.95f);
    [SerializeField] private Color _draggingColor = new Color(1f, 0.95f, 0.55f, 0.95f);
    [SerializeField] private float _cellPadding = 4f;
    [SerializeField] private Sprite _cellSprite;

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;

    private IReadOnlyList<Vector2Int> _baseCells;
    private readonly List<Vector2Int> _rotatedCells = new();
    private readonly List<Image> _cellImages = new();

    private RectTransform _homeParent;
    private Vector2 _homePosition;

    private int _rotationStep;
    private bool _isDragging;
    private Camera _dragEventCamera;
    private Vector2 _lastPointerScreenPosition;

    private DragSnapshot _dragSnapshot;

    public IncomeBlockType BlockType => _blockType;
    public Vector2 Size => _rectTransform != null ? _rectTransform.sizeDelta : Vector2.zero;

    private struct DragSnapshot
    {
        public bool HadPlacement;
        public Vector2Int PlacementOrigin;
        public int RotationStep;
    }

    private void Awake()
    {
        EnsureComponents();
        _baseCells = IncomeShapeLibrary.GetBaseCells(_blockType);
        RefreshShapeVisual();
    }

    private void Update()
    {
        if (!_isDragging)
            return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            _rotationStep = (_rotationStep + 1) & 3;
            RefreshShapeVisual();
            StickCenterToPointer(_lastPointerScreenPosition, _dragEventCamera);
            UpdatePlacementPreview();
        }
    }

    public void Initialize(
        IncomeBlockType blockType,
        IncomeGridBoard gridBoard,
        RectTransform dragRoot,
        RectTransform homeParent,
        Vector2 homePosition,
        Color blockColor,
        Sprite cellSprite = null)
    {
        EnsureComponents();

        _gridBoard = gridBoard;
        _dragRoot = dragRoot;
        _blockColor = blockColor;
        _cellSprite = cellSprite;

        _blockType = blockType;
        _baseCells = IncomeShapeLibrary.GetBaseCells(_blockType);

        _rotationStep = 0;
        RefreshShapeVisual();

        SetHome(homeParent, homePosition);
        ReturnToHome();
    }

    public void SetHome(RectTransform homeParent, Vector2 homePosition)
    {
        _homeParent = homeParent;
        _homePosition = homePosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        EnsureComponents();

        _dragSnapshot = CaptureSnapshot();

        if (_dragSnapshot.HadPlacement && _gridBoard != null)
        {
            _gridBoard.RemovePlacement(this);
        }

        _isDragging = true;
        _dragEventCamera = eventData.pressEventCamera;
        _lastPointerScreenPosition = eventData.position;
        _canvasGroup.blocksRaycasts = false;

        if (_dragRoot != null)
            _rectTransform.SetParent(_dragRoot, true);

        // Dragging 동안에는 중앙 피벗으로 전환해서 블록 중심이 마우스에 정확히 붙게 한다.
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);

        SetColor(_draggingColor);
        StickCenterToPointer(eventData.position, eventData.pressEventCamera);

        UpdatePlacementPreview();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging)
            return;

        _dragEventCamera = eventData.pressEventCamera;
        _lastPointerScreenPosition = eventData.position;
        StickCenterToPointer(eventData.position, eventData.pressEventCamera);

        UpdatePlacementPreview();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        _canvasGroup.blocksRaycasts = true;
        _gridBoard?.ClearPlacementPreview();

        bool placed = TryPlaceAtCurrentTransform(_dragEventCamera != null ? _dragEventCamera : eventData.pressEventCamera);
        if (!placed)
        {
            RestoreFromSnapshot();
        }

        SetColor(_blockColor);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (_isDragging)
            return;

        if (_gridBoard != null && _gridBoard.TryGetPlacementOrigin(this, out _))
        {
            ReturnToHome();
        }
    }

    private bool TryPlaceAtCurrentTransform(Camera eventCamera)
    {
        if (_gridBoard == null || _gridBoard.GridRoot == null)
            return false;

        if (!TryGetDropOriginCell(eventCamera, out var origin))
            return false;

        if (!_gridBoard.TryPlace(this, origin, _rotatedCells))
            return false;

        SnapToGrid(origin);
        return true;
    }

    private void SnapToGrid(Vector2Int origin)
    {
        if (_gridBoard == null || _gridBoard.GridRoot == null)
            return;

        _rectTransform.SetParent(_gridBoard.GridRoot, false);
        _rectTransform.anchorMin = Vector2.zero;
        _rectTransform.anchorMax = Vector2.zero;
        _rectTransform.pivot = Vector2.zero;
        _rectTransform.anchoredPosition = _gridBoard.GetAnchoredPositionFromOrigin(origin);
        _rectTransform.SetAsLastSibling();
    }

    private void RestoreFromSnapshot()
    {
        _rotationStep = _dragSnapshot.RotationStep;
        RefreshShapeVisual();

        if (_dragSnapshot.HadPlacement && _gridBoard != null)
        {
            bool restored = _gridBoard.TryPlace(this, _dragSnapshot.PlacementOrigin, _rotatedCells);
            if (restored)
            {
                SnapToGrid(_dragSnapshot.PlacementOrigin);
                return;
            }
        }

        ReturnToHome();
    }

    private void ReturnToHome()
    {
        if (_gridBoard != null)
            _gridBoard.RemovePlacement(this);

        if (_homeParent == null)
            return;

        _rectTransform.SetParent(_homeParent, false);
        _rectTransform.anchorMin = Vector2.zero;
        _rectTransform.anchorMax = Vector2.zero;
        _rectTransform.pivot = Vector2.zero;
        _rectTransform.anchoredPosition = _homePosition;
        _rectTransform.SetAsLastSibling();
    }

    private DragSnapshot CaptureSnapshot()
    {
        var snapshot = new DragSnapshot
        {
            HadPlacement = false,
            PlacementOrigin = default,
            RotationStep = _rotationStep
        };

        if (_gridBoard != null && _gridBoard.TryGetPlacementOrigin(this, out var origin))
        {
            snapshot.HadPlacement = true;
            snapshot.PlacementOrigin = origin;
        }

        return snapshot;
    }

    private void RefreshShapeVisual()
    {
        BuildRotatedCells(_rotationStep, _rotatedCells);
        EnsureCellImageCount(_rotatedCells.Count);

        float cellSize = _gridBoard != null ? _gridBoard.CellSize : 72f;
        float size = Mathf.Max(2f, cellSize - (_cellPadding * 2f));

        int maxX = 0;
        int maxY = 0;

        for (int i = 0; i < _rotatedCells.Count; i++)
        {
            var offset = _rotatedCells[i];
            if (offset.x > maxX) maxX = offset.x;
            if (offset.y > maxY) maxY = offset.y;

            var image = _cellImages[i];
            var cellRect = image.rectTransform;
            cellRect.anchorMin = Vector2.zero;
            cellRect.anchorMax = Vector2.zero;
            cellRect.pivot = Vector2.zero;
            cellRect.sizeDelta = new Vector2(size, size);
            cellRect.anchoredPosition = new Vector2(
                offset.x * cellSize + _cellPadding,
                offset.y * cellSize + _cellPadding);

            image.gameObject.SetActive(true);
            image.raycastTarget = true;
            image.sprite = _cellSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
        }

        for (int i = _rotatedCells.Count; i < _cellImages.Count; i++)
        {
            _cellImages[i].gameObject.SetActive(false);
        }

        _rectTransform.sizeDelta = new Vector2((maxX + 1) * cellSize, (maxY + 1) * cellSize);
        SetColor(_isDragging ? _draggingColor : _blockColor);
    }

    private void BuildRotatedCells(int rotationStep, List<Vector2Int> output)
    {
        output.Clear();

        if (_baseCells == null)
            _baseCells = IncomeShapeLibrary.GetBaseCells(_blockType);

        int minX = int.MaxValue;
        int minY = int.MaxValue;

        for (int i = 0; i < _baseCells.Count; i++)
        {
            Vector2Int cell = _baseCells[i];
            Vector2Int rotated;

            switch (rotationStep & 3)
            {
                case 1:
                    rotated = new Vector2Int(cell.y, -cell.x);
                    break;
                case 2:
                    rotated = new Vector2Int(-cell.x, -cell.y);
                    break;
                case 3:
                    rotated = new Vector2Int(-cell.y, cell.x);
                    break;
                default:
                    rotated = cell;
                    break;
            }

            if (rotated.x < minX) minX = rotated.x;
            if (rotated.y < minY) minY = rotated.y;

            output.Add(rotated);
        }

        for (int i = 0; i < output.Count; i++)
        {
            var cell = output[i];
            output[i] = new Vector2Int(cell.x - minX, cell.y - minY);
        }
    }

    private void EnsureCellImageCount(int count)
    {
        while (_cellImages.Count < count)
        {
            var cellGo = new GameObject("Cell", typeof(RectTransform), typeof(Image));
            var image = cellGo.GetComponent<Image>();

            cellGo.transform.SetParent(transform, false);
            image.raycastTarget = true;

            _cellImages.Add(image);
        }
    }

    private void SetColor(Color color)
    {
        for (int i = 0; i < _cellImages.Count; i++)
        {
            if (_cellImages[i] != null)
            {
                _cellImages[i].color = color;
                _cellImages[i].sprite = _cellSprite;
            }
        }
    }

    private void EnsureComponents()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        _rectTransform.anchorMin = Vector2.zero;
        _rectTransform.anchorMax = Vector2.zero;
        _rectTransform.pivot = Vector2.zero;
    }

    private bool TryGetPointerWorldPosition(Vector2 screenPoint, Camera eventCamera, out Vector3 worldPoint)
    {
        var plane = _rectTransform.parent as RectTransform;
        if (plane != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(plane, screenPoint, eventCamera, out worldPoint))
            return true;

        worldPoint = screenPoint;
        return false;
    }

    private bool TryGetDropOriginCell(Camera eventCamera, out Vector2Int origin)
    {
        origin = default;

        if (_gridBoard == null || _gridBoard.GridRoot == null)
            return false;

        // 드롭 시점엔 블록의 실제 좌하단 코너 위치를 기준으로 셀을 계산한다.
        var corners = new Vector3[4];
        _rectTransform.GetWorldCorners(corners);
        Vector2 rootScreenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
        return _gridBoard.TryGetCellFromScreenPoint(rootScreenPoint, eventCamera, out origin);
    }

    private void UpdatePlacementPreview()
    {
        if (!_isDragging || _gridBoard == null)
            return;

        if (!TryGetDropOriginCell(_dragEventCamera, out var origin))
        {
            _gridBoard.ClearPlacementPreview();
            return;
        }

        bool canPlace = _gridBoard.CanPlace(this, origin, _rotatedCells);
        _gridBoard.ShowPlacementPreview(origin, _rotatedCells, canPlace);
    }

    private void OnDisable()
    {
        _gridBoard?.ClearPlacementPreview();
    }

    public void SetCellSprite(Sprite sprite)
    {
        _cellSprite = sprite;
        RefreshShapeVisual();
    }

    private void StickCenterToPointer(Vector2 screenPoint, Camera eventCamera)
    {
        if (TryGetPointerWorldPosition(screenPoint, eventCamera, out var pointerWorld))
            _rectTransform.position = pointerWorld;
    }
}
