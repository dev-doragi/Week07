using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("Visual & Animation")]
    [SerializeField] private Color _blockColor = new Color(0.72f, 0.88f, 1f, 0.95f);
    [SerializeField] private Color _draggingColor = new Color(1f, 0.95f, 0.55f, 0.95f);
    [SerializeField] private Color _cellFillColor = Color.white;
    [SerializeField] private float _outlineWidth = 4f;
    [SerializeField] private float _cellPadding = 4f;
    [SerializeField] private Sprite _cellSprite;
    [SerializeField] private float _rotationTweenDuration = 0.15f; // 부드러운 회전 지속 시간

    [Header("Core Block Sprites")]
    [SerializeField] private CoreBlockSprites _coreBlockSprites;

    [Header("Drag Hint")]
    [SerializeField] private string _dragHintLabel = "R 회전";
    [SerializeField] private Vector2 _dragHintOffset = new Vector2(28f, 34f);
    [SerializeField] private Vector2 _dragHintSize = new Vector2(120f, 40f);
    [SerializeField] private int _dragHintFontSize = 24;
    [SerializeField] private Color _dragHintColor = Color.white;
    [SerializeField] private Color _dragHintOutlineColor = new Color(0f, 0f, 0f, 0.85f);
    [SerializeField] private Vector2 _dragHintOutlineDistance = new Vector2(1.5f, -1.5f);
    [SerializeField] private TMP_FontAsset _dragHintFontAsset;

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;

    private RectTransform _visualRoot; // 시각적 회전을 담당하는 컨테이너
    private Image _hitboxImage; // 후한 클릭 판정을 위한 투명 히트박스

    private IReadOnlyList<Vector2Int> _baseCells;
    private readonly List<Vector2Int> _rotatedCells = new List<Vector2Int>();

    private readonly List<RectTransform> _cellRoots = new List<RectTransform>();
    private readonly List<Image> _cellFillImages = new List<Image>();

    private RectTransform _outlineRoot;
    private readonly List<Image> _outlineSegments = new List<Image>();
    private readonly HashSet<Vector2Int> _cellLookup = new HashSet<Vector2Int>();
    private RectTransform _dragHintRect;
    private TextMeshProUGUI _dragHintText;

    private RectTransform _homeParent;
    private Vector2 _homePosition;

    private int _rotationStep;
    private bool _isDragging;
    private bool _isInteractionLocked;
    private Camera _dragEventCamera;
    private Vector2 _lastPointerScreenPosition;

    private DragSnapshot _dragSnapshot;
    private Coroutine _rotationRoutine;

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
        if (!_isDragging) return;

        if (StageMapController.IsMapVisible())
        {
            CancelDrag();
            return;
        }

        // v1.9 규정: Unity Input System (New) 사용
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            TriggerRotation();
        }
    }

    public void Initialize(
        IncomeBlockType blockType,
        IncomeGridBoard gridBoard,
        RectTransform dragRoot,
        RectTransform homeParent,
        Vector2 homePosition,
        Color blockColor)
    {
        EnsureComponents();

        _gridBoard = gridBoard;
        _dragRoot = dragRoot;
        _blockColor = blockColor;

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
        if (_isInteractionLocked) return;
        if (StageMapController.IsMapVisible()) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

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

        // 드래그 중에는 항상 중심 피벗으로 고정
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);

        ApplyVisualState(_draggingColor);
        StickCenterToPointer(eventData.position, eventData.pressEventCamera);
        ShowDragHint(eventData.position, eventData.pressEventCamera);
        UpdatePlacementPreview();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging || StageMapController.IsMapVisible()) return;

        _dragEventCamera = eventData.pressEventCamera;
        _lastPointerScreenPosition = eventData.position;
        StickCenterToPointer(eventData.position, eventData.pressEventCamera);
        UpdateDragHintPosition(eventData.position, eventData.pressEventCamera);
        UpdatePlacementPreview();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        if (StageMapController.IsMapVisible())
        {
            CancelDrag();
            return;
        }

        _isDragging = false;
        _canvasGroup.blocksRaycasts = true;
        _gridBoard?.ClearPlacementPreview();
        HideDragHint();

        bool placed = TryPlaceAtCurrentTransform(_dragEventCamera != null ? _dragEventCamera : eventData.pressEventCamera);
        if (!placed)
        {
            RestoreFromSnapshot();
        }

        ApplyVisualState(_blockColor);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isInteractionLocked) return;
        if (StageMapController.IsMapVisible()) return;
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (_isDragging) return;

        if (_gridBoard != null && _gridBoard.TryGetPlacementOrigin(this, out _))
        {
            ResetToDefaultRotation();
            ReturnToHome();
        }
    }

    private void CancelDrag()
    {
        if (!_isDragging) return;

        _isDragging = false;
        _canvasGroup.blocksRaycasts = true;
        _gridBoard?.ClearPlacementPreview();
        HideDragHint();
        RestoreFromSnapshot();
        ApplyVisualState(_blockColor);
    }

    private void TriggerRotation()
    {
        if (_rotationRoutine != null) StopCoroutine(_rotationRoutine);

        _rotationStep = (_rotationStep + 1) & 3;

        // 내부 셀 레이아웃은 즉시 재계산 (논리적 충돌 처리용)
        RefreshShapeVisual();
        StickCenterToPointer(_lastPointerScreenPosition, _dragEventCamera);
        UpdateDragHintPosition(_lastPointerScreenPosition, _dragEventCamera);
        UpdatePlacementPreview();

        // 껍데기(VisualRoot)만 역방향으로 틀어 부드럽게 돌아오는 트윈 실행
        _rotationRoutine = StartCoroutine(RotateVisualRoutine());
    }

    private IEnumerator RotateVisualRoutine()
    {
        float elapsed = 0f;
        Vector3 startRot = new Vector3(0f, 0f, 90f);
        Vector3 endRot = Vector3.zero;

        while (elapsed < _rotationTweenDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _rotationTweenDuration;
            t = 1f - Mathf.Pow(1f - t, 3f); // Ease Out Cubic

            _visualRoot.localEulerAngles = Vector3.Lerp(startRot, endRot, t);
            yield return null;
        }

        _visualRoot.localEulerAngles = endRot;
        _rotationRoutine = null;
    }

    private bool TryPlaceAtCurrentTransform(Camera eventCamera)
    {
        if (_gridBoard == null || _gridBoard.GridRoot == null) return false;

        if (!TryGetDropOriginCell(eventCamera, out var origin)) return false;

        if (!_gridBoard.TryPlace(this, origin, _rotatedCells)) return false;

        SnapToGrid(origin);
        return true;
    }

    public bool ForcePlaceOnGrid(Vector2Int origin)
    {
        EnsureComponents();

        if (_gridBoard == null || _gridBoard.GridRoot == null)
            return false;

        if (!_gridBoard.TryPlace(this, origin, _rotatedCells))
            return false;

        SnapToGrid(origin);
        return true;
    }

    public void SetInteractionLocked(bool locked)
    {
        _isInteractionLocked = locked;

        if (_isDragging)
            CancelDrag();

        if (_canvasGroup != null)
            _canvasGroup.blocksRaycasts = !locked;
    }

    private void SnapToGrid(Vector2Int origin)
    {
        if (_gridBoard == null || _gridBoard.GridRoot == null) return;

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
        if (_gridBoard != null) _gridBoard.RemovePlacement(this);
        if (_homeParent == null) return;

        _rectTransform.SetParent(_homeParent, false);
        _rectTransform.anchorMin = Vector2.zero;
        _rectTransform.anchorMax = Vector2.zero;
        _rectTransform.pivot = Vector2.zero;
        _rectTransform.anchoredPosition = _homePosition;
        _rectTransform.SetAsLastSibling();
    }

    private DragSnapshot CaptureSnapshot()
    {
        var snapshot = new DragSnapshot { HadPlacement = false, PlacementOrigin = default, RotationStep = _rotationStep };

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
        EnsureCellVisualCount(_rotatedCells.Count);

        float cellSize = _gridBoard != null ? _gridBoard.CellSize : 72f;
        bool isCore = IsCoreBlockType(_blockType);
        float effectivePadding = isCore ? 0f : _cellPadding;
        float fillSize = Mathf.Max(2f, cellSize - (effectivePadding * 2f));

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
                offset.x * cellSize + effectivePadding,
                offset.y * cellSize + effectivePadding);
            rootRect.gameObject.SetActive(true);

            var fillRect = fillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.zero;
            fillRect.pivot = Vector2.zero;
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(fillSize, fillSize);

            fillImage.sprite = ResolveCellSprite(offset);
            fillImage.type = Image.Type.Simple;
            fillImage.preserveAspect = false;
        }

        for (int i = _rotatedCells.Count; i < _cellRoots.Count; i++)
            _cellRoots[i].gameObject.SetActive(false);

        _rectTransform.sizeDelta = new Vector2((maxX + 1) * cellSize, (maxY + 1) * cellSize);

        RebuildShapeOutline(cellSize, fillSize, effectivePadding);
        ApplyVisualState(_isDragging ? _draggingColor : _blockColor);
    }

    private void RebuildShapeOutline(float cellSize, float fillSize, float padding)
    {
        // 코어는 한 덩어리로 보여야 하므로 외곽선 비활성화
        if (IsCoreBlockType(_blockType))
        {
            if (_outlineRoot != null)
                _outlineRoot.gameObject.SetActive(false);

            for (int i = 0; i < _outlineSegments.Count; i++)
                _outlineSegments[i].gameObject.SetActive(false);

            return;
        }

        EnsureOutlineRoot();
        _outlineRoot.gameObject.SetActive(true);
        _cellLookup.Clear();

        for (int i = 0; i < _rotatedCells.Count; i++)
            _cellLookup.Add(_rotatedCells[i]);

        int requiredSegments = CountRequiredOutlineSegments();
        EnsureOutlineSegmentCount(requiredSegments);

        float thickness = Mathf.Max(1f, _outlineWidth);
        int segmentIndex = 0;

        for (int i = 0; i < _rotatedCells.Count; i++)
        {
            var cell = _rotatedCells[i];
            float x = cell.x * cellSize + padding;
            float y = cell.y * cellSize + padding;

            if (!HasCell(cell + Left)) ConfigureOutlineSegment(segmentIndex++, new Vector2(x - thickness, y - thickness), new Vector2(thickness, fillSize + thickness * 2f));
            if (!HasCell(cell + Right)) ConfigureOutlineSegment(segmentIndex++, new Vector2(x + fillSize, y - thickness), new Vector2(thickness, fillSize + thickness * 2f));
            if (!HasCell(cell + Down)) ConfigureOutlineSegment(segmentIndex++, new Vector2(x - thickness, y - thickness), new Vector2(fillSize + thickness * 2f, thickness));
            if (!HasCell(cell + Up)) ConfigureOutlineSegment(segmentIndex++, new Vector2(x - thickness, y + fillSize), new Vector2(fillSize + thickness * 2f, thickness));
        }

        for (int i = segmentIndex; i < _outlineSegments.Count; i++)
            _outlineSegments[i].gameObject.SetActive(false);
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

    private bool HasCell(Vector2Int cell) => _cellLookup.Contains(cell);

    private void ConfigureOutlineSegment(int index, Vector2 anchoredPosition, Vector2 size)
    {
        var image = _outlineSegments[index];
        var rect = image.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

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
            Vector2Int rotated = (rotationStep & 3) switch
            {
                1 => new Vector2Int(cell.y, -cell.x),
                2 => new Vector2Int(-cell.x, -cell.y),
                3 => new Vector2Int(-cell.y, cell.x),
                _ => cell
            };

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
            cellRoot.SetParent(_visualRoot, false); // 시각적 컨테이너 하위로 이동

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = fillGo.GetComponent<RectTransform>();
            var fillImage = fillGo.GetComponent<Image>();
            fillRect.SetParent(cellRoot, false);

            // 개별 셀의 레이캐스트를 끄고 투명 히트박스가 전체 판정을 받도록 함
            fillImage.raycastTarget = false;

            _cellRoots.Add(cellRoot);
            _cellFillImages.Add(fillImage);
        }
    }

    private void EnsureOutlineRoot()
    {
        if (_outlineRoot != null) return;

        var outlineGo = new GameObject("__ShapeOutline", typeof(RectTransform));
        _outlineRoot = outlineGo.GetComponent<RectTransform>();
        _outlineRoot.SetParent(_visualRoot, false);
        _outlineRoot.anchorMin = Vector2.zero;
        _outlineRoot.anchorMax = Vector2.zero;
        _outlineRoot.pivot = Vector2.zero;
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

    private void ApplyVisualState(Color outlineColor)
    {
        var visibleOutline = outlineColor;
        visibleOutline.a = 1f;

        for (int i = 0; i < _outlineSegments.Count; i++)
        {
            if (_outlineSegments[i] != null) _outlineSegments[i].color = visibleOutline;
        }

        for (int i = 0; i < _cellFillImages.Count; i++)
        {
            var fill = _cellFillImages[i];
            if (fill != null)
            {
                fill.color = _cellFillColor;

                if (i < _rotatedCells.Count)
                    fill.sprite = ResolveCellSprite(_rotatedCells[i]);
            }
        }
    }

    private void ShowDragHint(Vector2 screenPoint, Camera eventCamera)
    {
        EnsureDragHint();
        if (_dragHintRect == null)
            return;

        _dragHintRect.gameObject.SetActive(true);
        _dragHintRect.SetAsLastSibling();
        UpdateDragHintPosition(screenPoint, eventCamera);
    }

    private void HideDragHint()
    {
        if (_dragHintRect != null)
            _dragHintRect.gameObject.SetActive(false);
    }

    private void UpdateDragHintPosition(Vector2 screenPoint, Camera eventCamera)
    {
        if (_dragHintRect == null || !_dragHintRect.gameObject.activeSelf)
            return;

        RectTransform parent = _dragHintRect.parent as RectTransform;
        if (parent == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, eventCamera, out Vector2 localPoint))
            _dragHintRect.anchoredPosition = localPoint + _dragHintOffset;
    }

    private void EnsureDragHint()
    {
        RectTransform hintParent = _dragRoot != null ? _dragRoot : _rectTransform.root as RectTransform;
        if (hintParent == null)
            return;

        if (_dragHintRect != null && _dragHintRect.parent != hintParent)
            _dragHintRect.SetParent(hintParent, false);

        if (_dragHintRect == null)
        {
            var hintGo = new GameObject("__IncomeDragHint", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Outline));
            _dragHintRect = hintGo.GetComponent<RectTransform>();
            _dragHintRect.SetParent(hintParent, false);
            _dragHintText = hintGo.GetComponent<TextMeshProUGUI>();
        }
        else if (_dragHintText == null)
        {
            _dragHintText = _dragHintRect.GetComponent<TextMeshProUGUI>();
        }

        _dragHintRect.anchorMin = new Vector2(0.5f, 0.5f);
        _dragHintRect.anchorMax = new Vector2(0.5f, 0.5f);
        _dragHintRect.pivot = new Vector2(0f, 0.5f);
        _dragHintRect.sizeDelta = _dragHintSize;

        if (_dragHintText != null)
        {
            _dragHintText.text = _dragHintLabel;
            _dragHintText.fontSize = _dragHintFontSize;
            _dragHintText.color = _dragHintColor;
            _dragHintText.alignment = TextAlignmentOptions.Left;
            _dragHintText.raycastTarget = false;
            if (_dragHintFontAsset != null)
                _dragHintText.font = _dragHintFontAsset;
        }

        var outline = _dragHintRect.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = _dragHintOutlineColor;
            outline.effectDistance = _dragHintOutlineDistance;
            outline.useGraphicAlpha = true;
        }
    }

    private void EnsureComponents()
    {
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

        if (_rectTransform == null || _canvasGroup == null)
        {
            Debug.LogError("[IncomeBlockPiece] 필수 컴포넌트가 누락되었습니다.");
            return;
        }

        // 트윈 연출을 위한 시각적 래퍼
        if (_visualRoot == null)
        {
            var visualGo = new GameObject("__VisualRoot", typeof(RectTransform));
            _visualRoot = visualGo.GetComponent<RectTransform>();
            _visualRoot.SetParent(_rectTransform, false);
            _visualRoot.anchorMin = Vector2.zero;
            _visualRoot.anchorMax = Vector2.one;
            _visualRoot.offsetMin = Vector2.zero;
            _visualRoot.offsetMax = Vector2.zero;
            _visualRoot.pivot = new Vector2(0.5f, 0.5f); // 중심축 회전을 위함
        }

        // 후한 클릭 판정을 위한 전체 덮개
        if (_hitboxImage == null)
        {
            var hitboxGo = new GameObject("__Hitbox", typeof(RectTransform), typeof(Image));
            var hitboxRect = hitboxGo.GetComponent<RectTransform>();
            hitboxRect.SetParent(_rectTransform, false);
            hitboxRect.SetAsFirstSibling();
            hitboxRect.anchorMin = Vector2.zero;
            hitboxRect.anchorMax = Vector2.one;
            hitboxRect.offsetMin = Vector2.zero;
            hitboxRect.offsetMax = Vector2.zero;

            _hitboxImage = hitboxGo.GetComponent<Image>();
            _hitboxImage.color = Color.clear;
            _hitboxImage.raycastTarget = true;
        }

        _rectTransform.anchorMin = Vector2.zero;
        _rectTransform.anchorMax = Vector2.zero;
        _rectTransform.pivot = Vector2.zero;
    }

    private bool IsCoreBlockType(IncomeBlockType type)
    {
        return type == IncomeBlockType.CoreCross || type == IncomeBlockType.CoreSquare;
    }

    private Sprite ResolveCellSprite(Vector2Int position)
    {
        if (IsCoreBlockType(_blockType))
        {
            Sprite coreSprite = GetCoreBlockSpriteByPosition(position);
            if (coreSprite != null)
                return coreSprite;
        }

        return _cellSprite;
    }

    private Sprite GetCoreBlockSpriteByPosition(Vector2Int position)
    {
        if (_coreBlockSprites == null)
            return null;

        if (_blockType == IncomeBlockType.CoreSquare)
        {
            // 2x2: (0,0) ~ (1,1)
            if (position.y == 1 && position.x == 0) return _coreBlockSprites.SquareTopLeft;
            if (position.y == 1 && position.x == 1) return _coreBlockSprites.SquareTopRight;
            if (position.y == 0 && position.x == 0) return _coreBlockSprites.SquareBottomLeft;
            if (position.y == 0 && position.x == 1) return _coreBlockSprites.SquareBottomRight;
        }
        else if (_blockType == IncomeBlockType.CoreCross)
        {
            // 십자: 중앙(1,1), 위(1,2), 아래(1,0), 좌(0,1), 우(2,1)
            if (position.x == 1 && position.y == 1) return _coreBlockSprites.CrossCenter;
            if (position.x == 1 && position.y == 2) return _coreBlockSprites.CrossTop;
            if (position.x == 1 && position.y == 0) return _coreBlockSprites.CrossBottom;
            if (position.x == 0 && position.y == 1) return _coreBlockSprites.CrossLeft;
            if (position.x == 2 && position.y == 1) return _coreBlockSprites.CrossRight;
        }

        return null;
    }

    private bool TryGetPointerWorldPosition(Vector2 screenPoint, Camera eventCamera, out Vector3 worldPoint)
    {
        var plane = _rectTransform.parent as RectTransform;
        if (plane != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(plane, screenPoint, eventCamera, out worldPoint))
            return true;

        worldPoint = screenPoint;
        return false;
    }

    // ★ 핵심 수정: 스내핑을 마우스 중심 + RoundToInt 방식으로 매우 후하게 변경
    private bool TryGetDropOriginCell(Camera eventCamera, out Vector2Int origin)
    {
        origin = default;

        if (_gridBoard == null || _gridBoard.GridRoot == null) return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _gridBoard.GridRoot, _lastPointerScreenPosition, eventCamera, out Vector2 localPointer))
        {
            return false;
        }

        // 보드 좌하단(0,0)을 기준으로 한 마우스 좌표
        Vector2 localFromLowerLeft = localPointer + new Vector2(
            _gridBoard.GridRoot.rect.width * _gridBoard.GridRoot.pivot.x,
            _gridBoard.GridRoot.rect.height * _gridBoard.GridRoot.pivot.y);

        float blockWidth = _rectTransform.rect.width;
        float blockHeight = _rectTransform.rect.height;

        // 마우스가 항상 블록의 중앙을 잡고 있으므로 역산하여 좌하단을 유추
        Vector2 blockLowerLeft = localFromLowerLeft - new Vector2(blockWidth * 0.5f, blockHeight * 0.5f);

        // FloorToInt(픽셀 단위의 엄격함) 대신 RoundToInt(절반만 겹쳐도 스냅) 사용
        int originX = Mathf.RoundToInt(blockLowerLeft.x / _gridBoard.CellSize);
        int originY = Mathf.RoundToInt(blockLowerLeft.y / _gridBoard.CellSize);

        origin = new Vector2Int(originX, originY);
        return true;
    }

    private void UpdatePlacementPreview()
    {
        if (!_isDragging || _gridBoard == null) return;

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
        HideDragHint();
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

    private void ResetToDefaultRotation()
    {
        if (_rotationRoutine != null) StopCoroutine(_rotationRoutine);
        _rotationStep = 0;
        RefreshShapeVisual();
        _visualRoot.localEulerAngles = Vector3.zero;
    }
}
