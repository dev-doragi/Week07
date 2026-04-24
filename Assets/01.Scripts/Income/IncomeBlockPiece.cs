using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 드래그/회전/배치를 담당하는 개별 수익 블록 UI.
/// 내부 셀 이미지는 고정 색상으로 유지하고, 타입 구분은 모양 외곽선 색상으로 처리한다.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class IncomeBlockPiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private static readonly Vector2Int Left = new Vector2Int(-1, 0);
    private static readonly Vector2Int Right = new Vector2Int(1, 0);
    private static readonly Vector2Int Down = new Vector2Int(0, -1);
    private static readonly Vector2Int Up = new Vector2Int(0, 1);

    [Header("Data")]
    [SerializeField] private IncomeBlockType _blockType = IncomeBlockType.T;

    [Header("References")]
    [SerializeField] private IncomeGridBoard _gridBoard;
    [SerializeField] private RectTransform _dragRoot;

    [Header("Visual")]
    // 인벤토리에서 블록 타입을 구분하기 위한 외곽선 기본 색.
    [SerializeField] private Color _blockColor = new Color(0.72f, 0.88f, 1f, 0.95f);
    // 드래그 중 상태 강조용 외곽선 색.
    [SerializeField] private Color _draggingColor = new Color(1f, 0.95f, 0.55f, 0.95f);
    // 셀 내부는 항상 동일 색으로 유지한다.
    [SerializeField] private Color _cellFillColor = Color.white;
    // 모양 아웃라인 두께.
    [SerializeField] private float _outlineWidth = 4f;
    [SerializeField] private float _cellPadding = 4f;
    [SerializeField] private Sprite _cellSprite;

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;

    private IReadOnlyList<Vector2Int> _baseCells;
    private readonly List<Vector2Int> _rotatedCells = new List<Vector2Int>();

    private readonly List<RectTransform> _cellRoots = new List<RectTransform>();
    private readonly List<Image> _cellFillImages = new List<Image>();

    // 모양 외곽선 전용 UI.
    private RectTransform _outlineRoot;
    private readonly List<Image> _outlineSegments = new List<Image>();
    private readonly HashSet<Vector2Int> _cellLookup = new HashSet<Vector2Int>();

    private readonly Vector3[] _worldCorners = new Vector3[4];

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
        // blockColor 파라미터는 내부 채움색이 아닌 외곽선 색상으로 사용한다.
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

        // 드래그 중 블록 중심이 마우스 중심을 정확히 따라가도록 중앙 피벗 사용.
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);

        ApplyVisualState(_draggingColor);
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

        ApplyVisualState(_blockColor);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (_isDragging)
            return;

        if (_gridBoard != null && _gridBoard.TryGetPlacementOrigin(this, out _))
        {
            // 그리드에서 회전된 상태로 돌아와도 인벤토리에서는 원본 모양으로 복귀.
            ResetToDefaultRotation();
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

    // 회전 상태를 기준으로 현재 블록의 셀 UI와 모양 외곽선을 다시 생성/배치한다.
    private void RefreshShapeVisual()
    {
        BuildRotatedCells(_rotationStep, _rotatedCells);
        EnsureCellVisualCount(_rotatedCells.Count);

        float cellSize = _gridBoard != null ? _gridBoard.CellSize : 72f;
        float fillSize = Mathf.Max(2f, cellSize - (_cellPadding * 2f));

        int maxX = 0;
        int maxY = 0;

        for (int i = 0; i < _rotatedCells.Count; i++)
        {
            var offset = _rotatedCells[i];
            if (offset.x > maxX) maxX = offset.x;
            if (offset.y > maxY) maxY = offset.y;

            var rootRect = _cellRoots[i];
            var fillImage = _cellFillImages[i];

            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.zero;
            rootRect.pivot = Vector2.zero;
            rootRect.sizeDelta = new Vector2(fillSize, fillSize);
            rootRect.anchoredPosition = new Vector2(
                offset.x * cellSize + _cellPadding,
                offset.y * cellSize + _cellPadding);
            rootRect.gameObject.SetActive(true);

            var fillRect = fillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.zero;
            fillRect.pivot = Vector2.zero;
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(fillSize, fillSize);

            fillImage.sprite = _cellSprite;
            fillImage.type = Image.Type.Simple;
            fillImage.preserveAspect = false;
            fillImage.raycastTarget = true;
        }

        for (int i = _rotatedCells.Count; i < _cellRoots.Count; i++)
        {
            _cellRoots[i].gameObject.SetActive(false);
        }

        _rectTransform.sizeDelta = new Vector2((maxX + 1) * cellSize, (maxY + 1) * cellSize);

        RebuildShapeOutline(cellSize, fillSize);
        ApplyVisualState(_isDragging ? _draggingColor : _blockColor);
    }

    private void RebuildShapeOutline(float cellSize, float fillSize)
    {
        EnsureOutlineRoot();
        _cellLookup.Clear();

        for (int i = 0; i < _rotatedCells.Count; i++)
        {
            _cellLookup.Add(_rotatedCells[i]);
        }

        int requiredSegments = CountRequiredOutlineSegments();
        EnsureOutlineSegmentCount(requiredSegments);

        float thickness = Mathf.Max(1f, _outlineWidth);
        int segmentIndex = 0;

        for (int i = 0; i < _rotatedCells.Count; i++)
        {
            var cell = _rotatedCells[i];
            float x = cell.x * cellSize + _cellPadding;
            float y = cell.y * cellSize + _cellPadding;

            if (!HasCell(cell + Left))
            {
                ConfigureOutlineSegment(
                    segmentIndex++,
                    new Vector2(x - thickness, y - thickness),
                    new Vector2(thickness, fillSize + thickness * 2f));
            }

            if (!HasCell(cell + Right))
            {
                ConfigureOutlineSegment(
                    segmentIndex++,
                    new Vector2(x + fillSize, y - thickness),
                    new Vector2(thickness, fillSize + thickness * 2f));
            }

            if (!HasCell(cell + Down))
            {
                ConfigureOutlineSegment(
                    segmentIndex++,
                    new Vector2(x - thickness, y - thickness),
                    new Vector2(fillSize + thickness * 2f, thickness));
            }

            if (!HasCell(cell + Up))
            {
                ConfigureOutlineSegment(
                    segmentIndex++,
                    new Vector2(x - thickness, y + fillSize),
                    new Vector2(fillSize + thickness * 2f, thickness));
            }
        }

        for (int i = segmentIndex; i < _outlineSegments.Count; i++)
        {
            _outlineSegments[i].gameObject.SetActive(false);
        }
    }

    private int CountRequiredOutlineSegments()
    {
        int count = 0;

        for (int i = 0; i < _rotatedCells.Count; i++)
        {
            var cell = _rotatedCells[i];
            if (!HasCell(cell + Left)) count++;
            if (!HasCell(cell + Right)) count++;
            if (!HasCell(cell + Down)) count++;
            if (!HasCell(cell + Up)) count++;
        }

        return count;
    }

    private bool HasCell(Vector2Int cell)
    {
        return _cellLookup.Contains(cell);
    }

    private void ConfigureOutlineSegment(int index, Vector2 anchoredPosition, Vector2 size)
    {
        var image = _outlineSegments[index];
        var rect = image.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        // 외곽선은 텍스처를 쓰지 않고 단색 라인으로 그려서 모양 구분을 선명하게 한다.
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        image.gameObject.SetActive(true);
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

    private void EnsureCellVisualCount(int count)
    {
        while (_cellRoots.Count < count)
        {
            var cellRootGo = new GameObject("Cell", typeof(RectTransform));
            var cellRoot = cellRootGo.GetComponent<RectTransform>();
            cellRoot.SetParent(transform, false);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = fillGo.GetComponent<RectTransform>();
            var fillImage = fillGo.GetComponent<Image>();
            fillRect.SetParent(cellRoot, false);
            fillImage.raycastTarget = true;

            _cellRoots.Add(cellRoot);
            _cellFillImages.Add(fillImage);
        }
    }

    private void EnsureOutlineRoot()
    {
        if (_outlineRoot != null)
            return;

        var outlineGo = new GameObject("__ShapeOutline", typeof(RectTransform));
        _outlineRoot = outlineGo.GetComponent<RectTransform>();
        _outlineRoot.SetParent(transform, false);
        _outlineRoot.anchorMin = Vector2.zero;
        _outlineRoot.anchorMax = Vector2.zero;
        _outlineRoot.pivot = Vector2.zero;
        _outlineRoot.anchoredPosition = Vector2.zero;
        _outlineRoot.sizeDelta = Vector2.zero;
        _outlineRoot.SetAsFirstSibling();
    }

    private void EnsureOutlineSegmentCount(int count)
    {
        EnsureOutlineRoot();

        while (_outlineSegments.Count < count)
        {
            var segGo = new GameObject("Segment", typeof(RectTransform), typeof(Image));
            var segRect = segGo.GetComponent<RectTransform>();
            var segImage = segGo.GetComponent<Image>();

            segRect.SetParent(_outlineRoot, false);
            segImage.raycastTarget = false;

            _outlineSegments.Add(segImage);
        }
    }

    // 현재 상태(기본/드래그)에 맞춰 외곽선 색만 변경한다.
    private void ApplyVisualState(Color outlineColor)
    {
        var visibleOutline = outlineColor;
        visibleOutline.a = 1f;

        for (int i = 0; i < _outlineSegments.Count; i++)
        {
            var segment = _outlineSegments[i];
            if (segment != null)
            {
                segment.color = visibleOutline;
            }
        }

        for (int i = 0; i < _cellFillImages.Count; i++)
        {
            var fill = _cellFillImages[i];
            if (fill != null)
            {
                fill.color = _cellFillColor;
                fill.sprite = _cellSprite;
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

        // 드롭 시점에는 블록 좌하단 월드 코너를 기준으로 그리드 셀을 계산한다.
        _rectTransform.GetWorldCorners(_worldCorners);
        Vector2 rootScreenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, _worldCorners[0]);
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

    // 드래그 중 블록 중심이 항상 포인터 중심에 오도록 고정한다.
    private void StickCenterToPointer(Vector2 screenPoint, Camera eventCamera)
    {
        if (TryGetPointerWorldPosition(screenPoint, eventCamera, out var pointerWorld))
            _rectTransform.position = pointerWorld;
    }

    private void ResetToDefaultRotation()
    {
        _rotationStep = 0;
        RefreshShapeVisual();
    }
}
