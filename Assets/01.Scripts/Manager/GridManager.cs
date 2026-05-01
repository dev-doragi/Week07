using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// ================================================================
// 그리드의 모든 것을 담당하는 싱글톤 매니저 (씬에 1개만 존재)
// 담당:
//    1) 그리드 데이터 저장 (어떤 셀에 뭐가 있는지)
//    2) 좌표 변환 (월드 ↔ 셀)
//    3) 설치 가능 여부 검증 (규칙 체크)
//    4) 실제 설치/제거 (프리팹 Instantiate/Destroy)
//    5) 에디터에서 그리드 라인 시각화 (Gizmo)
// ================================================================
[DefaultExecutionOrder(-150)]
public class GridManager : Singleton<GridManager>
{
    // ==========================================
    // 인스펙터 설정
    // ==========================================
    [Header("Grid Settings")]
    [SerializeField] private int _width = 30;
    [SerializeField] private int _height = 20;
    [SerializeField] private float _cellSize = 1f;
    [SerializeField] private Vector3 _origin = Vector3.zero; // 그리드 (0,0)의 월드 위치

    [Header("Placement Failure Feedback")]
    [SerializeField] private TMP_FontAsset _failureTextFont;
    [SerializeField, Min(0f)] private float _failureFeedbackCooldown = 0.5f;
    [SerializeField] private Color _capacityFailureTextColor = Color.red;
    [SerializeField, Min(0.1f)] private float _capacityFailureTextSize = 2f;
    [SerializeField] private Color _resourceFailureTextColor = Color.red;
    [SerializeField, Min(0.1f)] private float _resourceFailureTextSize = 2f;
    [SerializeField, Min(1f)] private float _uiFailureTextSizeMultiplier = 10f;
    [SerializeField, Min(0.1f)] private float _failureTextDuration = 0.75f;
    [SerializeField] private float _failureTextRiseDistance = 0.6f;

    // ==========================================
    // 내부 상태
    // ==========================================
    private PlacedUnit[,] _cells;
    private Rigidbody2D _rb;
    private float _lastFailureFeedbackTime = -999f;
    private int _maxHpBonus = 0;
    private int _capacityBonus = 0;

    private readonly Vector2Int[] _fourDirections =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public int Width => _width;
    public event System.Action OnCapacityChanged;
    public int MaxCapacity => SumWheelCapacities() + _capacityBonus;
    public int CurrentUnitCount => CountPlacedUnits();
    public int Height => _height;
    public float CellSize => _cellSize;

    // ==========================================
    // Unity 생명주기
    // ==========================================
    protected override void Awake()
    {
        base.Awake(); // Singleton 등록
        _cells = new PlacedUnit[_width, _height];
        _rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<CoreDestroyedEvent>(OnCoreDestroyed);
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<CoreDestroyedEvent>(OnCoreDestroyed);
    }

    private void OnCoreDestroyed(CoreDestroyedEvent e)
    {
        if (StageLoadContext.IsTutorial)
        {
            Debug.Log("[StageManager] 튜토리얼 중이므로 코어 파괴 시 스테이지 클리어를 무시합니다.");
            return;
        }

        if (!e.IsPlayerBase) return;

        Debug.Log("[GridManager] 플레이어 코어 파괴 -> 전체 유닛 제거");

        var allUnits = CollectAllPlaced();
        foreach (var unit in allUnits)
        {
            StartCollapse(unit);
        }
    }

    public void SetCapacityBonus(int bonus)
    {
        _capacityBonus = bonus;
        OnCapacityChanged?.Invoke();
    }

    // ==========================================
    // 좌표 변환 (마우스/카메라 ↔ 그리드)
    // ==========================================
    public Vector3 CellToWorld(Vector2Int cell)
    {
        float half = _cellSize * 0.5f;
        return _origin + new Vector3(
            cell.x * _cellSize + half,
            cell.y * _cellSize + half, 0f);
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        var local = world - _origin;
        return new Vector2Int(
            Mathf.FloorToInt(local.x / _cellSize),
            Mathf.FloorToInt(local.y / _cellSize));
    }

    // ==========================================
    // 그리드 상태 조회
    // ==========================================
    public bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < _width
            && cell.y >= 0 && cell.y < _height;
    }

    public bool IsEmpty(Vector2Int cell)
    {
        return IsInBounds(cell) && _cells[cell.x, cell.y] == null;
    }

    public PlacedUnit GetUnitAt(Vector2Int cell)
    {
        return IsInBounds(cell) ? _cells[cell.x, cell.y] : null;
    }

    // ==========================================
    // 설치 가능 여부 검증
    // ==========================================
    public bool CanPlace(UnitDataSO data, Vector2Int origin)
    {
        if (data == null) return false;
        if (data.PlacementRule == PlacementRule.InitialOnly) return false;

        if (origin.y == 0 && data.Category != UnitCategory.Wheel)
        {
            return false;
        }

        if (data.Category == UnitCategory.Wheel && data.PlacementRule != PlacementRule.InitialOnly)
        {
            if (origin.y != 0)
            {
                Debug.Log("[GridManager] 바퀴는 최하단 행에만 설치 가능함!!");
                return false;
            }
        }

        for (int x = 0; x < data.Size.x; x++)
        {
            for (int y = 0; y < data.Size.y; y++)
            {
                var cell = new Vector2Int(origin.x + x, origin.y + y);
                if (!IsInBounds(cell) || !IsEmpty(cell)) 
                {
                    return false;
                }
            }
        }

        return data.PlacementRule switch
        {
            PlacementRule.NeedsFoundationBelow => HasFoundationBelow(data, origin),
            PlacementRule.NeedsAdjacent => HasAnyAdjacent(data, origin),
            _ => false
        };
    }

    public void SetMaxHpBonus(int newBonus)
    {
        int delta = newBonus - _maxHpBonus;     //증가 감소 변화량 계산
        _maxHpBonus = newBonus;
        // 현재 살아있는 모든 Player 유닛에 반영
        foreach(var unit in GetAllLivingUnits())
            unit.ApplyMaxHpBonusDelta(delta);
        OnCapacityChanged?.Invoke();
    }

    private bool HasFoundationBelow(UnitDataSO data, Vector2Int origin)
    {
        for (int x = 0; x < data.Size.x; x++)
        {
            var below = new Vector2Int(origin.x + x, origin.y - 1);
            var unit = GetUnitAt(below);
            if (unit == null || !unit.Data.ActsAsFoundation) return false;
        }
        return true;
    }

    private bool HasAnyAdjacent(UnitDataSO data, Vector2Int origin)
    {
        for (int x = 0; x < data.Size.x; x++)
        {
            for (int y = 0; y < data.Size.y; y++)
            {
                var cell = new Vector2Int(origin.x + x, origin.y + y);
                for (int i = 0; i < _fourDirections.Length; i++)
                {
                    var n = cell + _fourDirections[i];
                    if (n.x >= origin.x && n.x < origin.x + data.Size.x
                     && n.y >= origin.y && n.y < origin.y + data.Size.y) continue;
                    if (GetUnitAt(n) != null) return true;
                }
            }
        }
        return false;
    }

    // ==========================================
    // 설치 / 제거
    // ==========================================
    public bool TryPlace(UnitDataSO data, Vector2Int origin)
    {
        if (!CanPlace(data, origin)) return false;

        if (data.Category != UnitCategory.Wheel && data.Category != UnitCategory.Core)
        {
            if (CurrentUnitCount >= MaxCapacity)
            {
                Debug.Log($"[GridManager] 수용량 초과 | {CurrentUnitCount}/{MaxCapacity}");
                ShowPlacementFailureFeedback(
                    "수용량 초과",
                    data,
                    origin,
                    _capacityFailureTextColor,
                    _capacityFailureTextSize);
                return false;
            }
        }

        if (data.Cost > 0 && ResourceManager.Instance != null)
        {
            int before = ResourceManager.Instance.CurrentMouse;
            if (!ResourceManager.Instance.SubtractMouseCount(data.Cost, "grid_place:" + data.UnitName))
            {
                Debug.Log($"[GridManager] {data.UnitName} 배치 실패 | 보유: {before} / 필요: {data.Cost}");
                ShowPlacementFailureFeedback(
                    "자원 부족",
                    data,
                    origin,
                    _resourceFailureTextColor,
                    _resourceFailureTextSize);
                return false;
            }
            Debug.Log($"[GridManager] {data.UnitName} 배치 완료 | 사용: {data.Cost} | {before} → {ResourceManager.Instance.CurrentMouse}");
        }
        CreateAndRegister(data, origin);
        LogPlacementEvent(data, origin, true, false);
        return true;
    }

    public void PlaceInitial(UnitDataSO data, Vector2Int origin)
    {
        for (int x = 0; x < data.Size.x; x++)
        {
            for (int y = 0; y < data.Size.y; y++)
            {
                var cell = new Vector2Int(origin.x + x, origin.y + y);
                if (!IsInBounds(cell) || !IsEmpty(cell))
                {
                    Debug.LogError($"[GridManager] 초기 배치 실패: {data.UnitName} @ {origin}");
                    return;
                }
            }
        }
        CreateAndRegister(data, origin);
    }

    public bool TryRemove(Vector2Int cell)
    {
        var unit = GetUnitAt(cell);
        if (unit == null) return false;
        if (unit.Data.PlacementRule == PlacementRule.InitialOnly) return false;

        if (WouldCauseCollapse(unit))
        {
            Debug.Log($"[GridManager] {unit.Data.UnitName} 제거 거부 | 연쇄 붕괴 발생 위험");
            CameraManager.Instance?.ShakeWeak();

            return false;
        }

        if (unit.Data.Cost > 0 && ResourceManager.Instance != null)
        {
            int refund = Mathf.CeilToInt(unit.Data.Cost * 0.5f);
            ResourceManager.Instance.AddMouseCount(refund, "grid_remove_refund:" + unit.Data.UnitName);
            Debug.Log($"[GridManager] {unit.Data.UnitName} 제거 | 환불: {refund} (원가: {unit.Data.Cost})");
        }

        LogPlacementEvent(unit.Data, unit.OriginCell, false, false);
        StartCollapse(unit);
        ScheduleCollapseCheck();
        return true;
    }

    private bool WouldCauseCollapse(PlacedUnit unitToRemove)
    {
        for (int x = 0; x < unitToRemove.Data.Size.x; x++)
            for (int y = 0; y < unitToRemove.Data.Size.y; y++)
                _cells[unitToRemove.OriginCell.x + x, unitToRemove.OriginCell.y + y] = null;

        var unsupported = FindUnsupportedUnits();

        for (int x = 0; x < unitToRemove.Data.Size.x; x++)
            for (int y = 0; y < unitToRemove.Data.Size.y; y++)
                _cells[unitToRemove.OriginCell.x + x, unitToRemove.OriginCell.y + y] = unitToRemove;

        return unsupported.Count > 0;
    }

    private Vector3 FootprintCenter(UnitDataSO data, Vector2Int origin)
    {
        return _origin + new Vector3(
            (origin.x + data.Size.x * 0.5f) * _cellSize,
            (origin.y + data.Size.y * 0.5f) * _cellSize,
            0f);
    }

    private void ShowPlacementFailureFeedback(string message, UnitDataSO data, Vector2Int origin, Color color, float fontSize)
    {
        ShowFailureFeedback(message, FootprintCenter(data, origin), color, fontSize);
    }

    public void ShowResourceFailureFeedback(Vector3 worldPosition)
    {
        ShowFailureFeedback("자원 부족", worldPosition, _resourceFailureTextColor, _resourceFailureTextSize);
    }

    public void ShowResourceFailureFeedback(Vector2 screenPosition)
    {
        ShowUiFailureFeedback("자원 부족", screenPosition, _resourceFailureTextColor, _resourceFailureTextSize);
    }

    private void ShowFailureFeedback(string message, Vector3 worldPosition, Color color, float fontSize)
    {
        if (!TryBeginFailureFeedback()) return;
        StartCoroutine(PlacementFailureTextRoutine(message, worldPosition, color, fontSize));
    }

    private void ShowUiFailureFeedback(string message, Vector2 screenPosition, Color color, float fontSize)
    {
        if (!TryBeginFailureFeedback()) return;

        Canvas canvas = ResolveFeedbackCanvas();
        if (canvas == null)
        {
            StartCoroutine(PlacementFailureTextRoutine(message, Vector3.zero, color, fontSize));
            return;
        }

        StartCoroutine(UiFailureTextRoutine(message, screenPosition, color, fontSize, canvas));
    }

    private bool TryBeginFailureFeedback()
    {
        if (Time.unscaledTime - _lastFailureFeedbackTime < _failureFeedbackCooldown) return false;

        _lastFailureFeedbackTime = Time.unscaledTime;
        CameraManager.Instance?.ShakeWeak();
        return true;
    }

    private Canvas ResolveFeedbackCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas best = null;

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || !canvas.isActiveAndEnabled || !canvas.isRootCanvas) continue;

            if (best == null
                || canvas.renderMode == RenderMode.ScreenSpaceOverlay && best.renderMode != RenderMode.ScreenSpaceOverlay
                || canvas.sortingOrder > best.sortingOrder)
            {
                best = canvas;
            }
        }

        return best;
    }

    private IEnumerator PlacementFailureTextRoutine(string message, Vector3 worldPosition, Color textColor, float fontSize)
    {
        var go = new GameObject("PlacementFailureText");
        go.transform.SetParent(transform, true);
        go.transform.position = worldPosition;

        var text = go.AddComponent<TextMeshPro>();
        if (_failureTextFont != null)
            text.font = _failureTextFont;

        text.text = message;
        text.color = textColor;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        var renderer = text.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sortingOrder = 100;

        Vector3 start = worldPosition;
        Vector3 end = worldPosition + Vector3.up * _failureTextRiseDistance;
        float elapsed = 0f;

        while (elapsed < _failureTextDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _failureTextDuration);
            go.transform.position = Vector3.Lerp(start, end, t);

            Color fadeColor = textColor;
            fadeColor.a *= 1f - t;
            text.color = fadeColor;

            yield return null;
        }

        Destroy(go);
    }

    private IEnumerator UiFailureTextRoutine(string message, Vector2 screenPosition, Color textColor, float fontSize, Canvas canvas)
    {
        var go = new GameObject("PlacementFailureUIText", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        var rect = go.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(360f, 90f);

        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (canvasRect != null
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector2 localPoint))
        {
            rect.anchoredPosition = localPoint;
        }

        var text = go.AddComponent<TextMeshProUGUI>();
        if (_failureTextFont != null)
            text.font = _failureTextFont;

        text.text = message;
        text.color = textColor;
        text.fontSize = fontSize * _uiFailureTextSizeMultiplier;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        Vector2 start = rect.anchoredPosition;
        Vector2 end = start + Vector2.up * (_failureTextRiseDistance * 80f);
        float elapsed = 0f;

        while (elapsed < _failureTextDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _failureTextDuration);
            rect.anchoredPosition = Vector2.Lerp(start, end, t);

            Color fadeColor = textColor;
            fadeColor.a *= 1f - t;
            text.color = fadeColor;

            yield return null;
        }

        Destroy(go);
    }

    //수용량 헬퍼 메서드
    private int SumWheelCapacities()
    {
        var counted = new HashSet<PlacedUnit>();
        int total = 0;
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                var unit = _cells[x, y];
                if (unit != null && unit.Data.Category == UnitCategory.Wheel && counted.Add(unit))
                    total += unit.Data.WheelCapacity;
            }
        }

        return total;
    }

    private int CountPlacedUnits()
    {
        var counted = new HashSet<PlacedUnit>();
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                var unit = _cells[x, y];
                if (unit != null
                && unit.Data.Category != UnitCategory.Wheel
                && unit.Data.Category != UnitCategory.Core)
                {
                    counted.Add(unit);
                }
            }
        }
        return counted.Count;
    }

    private void CreateAndRegister(UnitDataSO data, Vector2Int origin)
    {
        var instance = Instantiate(data.Prefab, FootprintCenter(data, origin), Quaternion.identity, transform);
        var unit = instance.GetComponentInChildren<Unit>();

        var placed = new PlacedUnit(data, origin, instance);
        for (int x = 0; x < data.Size.x; x++)
            for (int y = 0; y < data.Size.y; y++)
                _cells[origin.x + x, origin.y + y] = placed;

        if (unit != null)
        {
            unit.InitializeRuntime();
            unit.OnDead += (deadUnit) => OnUnitDied(placed);
        }

        // CompositeCollider2D 갱신
        var compositeCollider = GetComponent<CompositeCollider2D>();
        if (compositeCollider != null)
        {
            compositeCollider.GenerateGeometry();
        }

        OnCapacityChanged?.Invoke();
        EventBus.Instance?.Publish(new PartPlacedEvent
        {
            PartKey = data.Key,
            GridPos = origin,
            costUp = data.Cost > 0
        });

        switch (data.Category)
        {
            case UnitCategory.Support:
                EventBus.Instance?.Publish(new TutorialProductionFacilityPlacedEvent
                {
                    PartKey = data.Key,
                    GridPos = origin
                });
                break;
            case UnitCategory.Defense:
                EventBus.Instance?.Publish(new TutorialDefenseUnitPlacedEvent
                {
                    PartKey = data.Key,
                    GridPos = origin
                });
                break;
            case UnitCategory.Attack:
                EventBus.Instance?.Publish(new TutorialAttackUnitPlacedEvent
                {
                    PartKey = data.Key,
                    GridPos = origin
                });
                break;
        }

        EventBus.Instance?.Publish(new PlayerGridChangedEvent());
    }

    private void OnUnitDied(PlacedUnit placed)
    {
        if (GetUnitAt(placed.OriginCell) != placed) return;
        ForceRemove(placed);
    }

    public void ForceRemove(PlacedUnit unit)
    {
        if (unit == null) return;
        Debug.Log($"[GridManager] {unit.Data.UnitName} 전투 파괴 | 연쇄 붕괴 체크 시작");
        LogPlacementEvent(unit.Data, unit.OriginCell, false, true);
        StartCollapse(unit);
        ScheduleCollapseCheck();
    }

    private void LogPlacementEvent(UnitDataSO data, Vector2Int origin, bool isPlaced, bool byCombat)
    {
        if (data == null) return;

        bool isFacility = data.Category == UnitCategory.Support;
        GameLogEventType eventType;
        if (isPlaced)
            eventType = isFacility ? GameLogEventType.FacilityPlaced : GameLogEventType.UnitPlaced;
        else
            eventType = isFacility ? GameLogEventType.FacilityRemoved : GameLogEventType.UnitRemoved;

        Vector3 world = FootprintCenter(data, origin);
        GameCsvLogger.Instance.LogEvent(
            eventType: eventType,
            value: data.Cost,
            metadata: new Dictionary<string, object>
            {
                { "unitKey", data.Key },
                { "unitName", data.UnitName },
                { "category", data.Category.ToString() },
                { "gridX", origin.x },
                { "gridY", origin.y },
                { "byCombat", byCombat },
                { "worldX", world.x },
                { "worldY", world.y }
            });
    }

    /// <summary>
    /// 돌진 데미지 계산 (가장 오른쪽 열의 공격력 합)
    /// </summary>
    public int CalculateRightmostColumnDamage()
    {
        var counted = new HashSet<PlacedUnit>();
        int totalDamage = 0;

        for (int y = 0; y < _height; y++)
        {
            for (int x = _width - 1; x >= 0; x--)
            {
                var unit = _cells[x, y];
                if (unit == null) continue;

                if (unit.Data.Category == UnitCategory.Attack && counted.Add(unit))
                {
                    totalDamage += unit.Data.Attack != null ? (int)unit.Data.Attack.Damage : 0;
                }
                break;
            }
        }
        return totalDamage;
    }

    /// <summary>
    /// 그리드에 배치된 전체 유닛의 CollisionPower 합산을 반환합니다.
    /// </summary>
    public float CalculateTotalCollisionPower()
    {
        var counted = new HashSet<PlacedUnit>();
        float total = 0f;

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                var unit = _cells[x, y];
                if (unit == null) continue;
                if (!counted.Add(unit)) continue;

                if (unit.Data.Defense != null)
                    total += unit.Data.Defense.CollisionPower;
            }
        }
        return total;
    }

    /// <summary>
    /// 그리드에 배치된 살아있는 Unit 컴포넌트 목록을 반환합니다.
    /// </summary>
    public List<Unit> GetAllLivingUnits()
    {
        var counted = new HashSet<PlacedUnit>();
        var result = new List<Unit>();

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                var placed = _cells[x, y];
                if (placed == null) continue;
                if (!counted.Add(placed)) continue;

                var unit = placed.Instance != null
                    ? placed.Instance.GetComponentInChildren<Unit>()
                    : null;

                if (unit != null && !unit.IsDead)
                    result.Add(unit);
            }
        }
        return result;
    }

    public List<PlacedUnit> GetPlacedUnitsSnapshot(bool includeInitialUnits = true)
    {
        var placedUnits = CollectAllPlaced();
        if (includeInitialUnits)
        {
            return placedUnits;
        }

        for (int i = placedUnits.Count - 1; i >= 0; i--)
        {
            UnitDataSO data = placedUnits[i]?.Data;
            if (data == null || data.Category == UnitCategory.Wheel || data.Category == UnitCategory.Core)
            {
                placedUnits.RemoveAt(i);
            }
        }

        return placedUnits;
    }

    #region 디버그 시각화

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.gray;
        for (int x = 0; x <= _width; x++)
        {
            Gizmos.DrawLine(
                _origin + new Vector3(x * _cellSize, 0, 0),
                _origin + new Vector3(x * _cellSize, _height * _cellSize, 0));
        }
        for (int y = 0; y <= _height; y++)
        {
            Gizmos.DrawLine(
                _origin + new Vector3(0, y * _cellSize, 0),
                _origin + new Vector3(_width * _cellSize, y * _cellSize, 0));
        }
    }

    #endregion

    #region 연쇄붕괴 시스템

    private const float COLLAPSE_DELAY = 0.15f;
    private bool _collapseScheduled = false;

    private void StartCollapse(PlacedUnit unit)
    {
        for (int x = 0; x < unit.Data.Size.x; x++)
            for (int y = 0; y < unit.Data.Size.y; y++)
                _cells[unit.OriginCell.x + x, unit.OriginCell.y + y] = null;

        var go = unit.Instance;
        go.transform.SetParent(null, true);

        var col = go.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        var falling = go.AddComponent<FallingUnit>();
        falling.Begin();

        // [Merge Resolved] 브랜치별 알림 로직 통합
        OnCapacityChanged?.Invoke();
        EventBus.Instance?.Publish(new PlayerGridChangedEvent());
    }

    private void ScheduleCollapseCheck()
    {
        if (_collapseScheduled) return;
        _collapseScheduled = true;
        StartCoroutine(CollapseCheckLoop());
    }

    private IEnumerator CollapseCheckLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(COLLAPSE_DELAY);
            var toCollapse = FindUnsupportedUnits();
            if (toCollapse.Count == 0) break;
            foreach (var unit in toCollapse) StartCollapse(unit);
        }
        _collapseScheduled = false;
    }

    private List<PlacedUnit> CollectAllPlaced()
    {
        var set = new HashSet<PlacedUnit>();
        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++)
                if (_cells[x, y] != null) set.Add(_cells[x, y]);
        return new List<PlacedUnit>(set);
    }

    private List<PlacedUnit> FindUnsupportedUnits()
    {
        var allPlaced = CollectAllPlaced();
        var supported = new HashSet<PlacedUnit>();

        foreach (var unit in allPlaced)
            if (unit.Data.PlacementRule == PlacementRule.InitialOnly)
                supported.Add(unit);

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var unit in allPlaced)
            {
                if (supported.Contains(unit)) continue;
                if (IsSupportedBy(unit, supported))
                {
                    supported.Add(unit);
                    changed = true;
                }
            }
        }

        var result = new List<PlacedUnit>();
        foreach (var unit in allPlaced)
            if (!supported.Contains(unit)) result.Add(unit);
        return result;
    }

    private bool IsSupportedBy(PlacedUnit unit, HashSet<PlacedUnit> supported)
    {
        return unit.Data.PlacementRule switch
        {
            PlacementRule.InitialOnly => true,
            PlacementRule.NeedsFoundationBelow => HasSupportedFoundationBelow(unit, supported),
            PlacementRule.NeedsAdjacent => HasSupportedAdjacent(unit, supported),
            _ => false
        };
    }

    private bool HasSupportedFoundationBelow(PlacedUnit unit, HashSet<PlacedUnit> supported)
    {
        for (int x = 0; x < unit.Data.Size.x; x++)
        {
            var below = new Vector2Int(unit.OriginCell.x + x, unit.OriginCell.y - 1);
            var belowUnit = GetUnitAt(below);
            if (belowUnit == null) return false;
            if (!belowUnit.Data.ActsAsFoundation) return false;
            if (!supported.Contains(belowUnit)) return false;
        }
        return true;
    }

    private bool HasSupportedAdjacent(PlacedUnit unit, HashSet<PlacedUnit> supported)
    {
        for (int x = 0; x < unit.Data.Size.x; x++)
        {
            for (int y = 0; y < unit.Data.Size.y; y++)
            {
                var cell = new Vector2Int(unit.OriginCell.x + x, unit.OriginCell.y + y);
                for (int i = 0; i < _fourDirections.Length; i++)
                {
                    var n = cell + _fourDirections[i];
                    if (n.x >= unit.OriginCell.x && n.x < unit.OriginCell.x + unit.Data.Size.x
                     && n.y >= unit.OriginCell.y && n.y < unit.OriginCell.y + unit.Data.Size.y) continue;

                    var neighbor = GetUnitAt(n);
                    if (neighbor != null && supported.Contains(neighbor)) return true;
                }
            }
        }
        return false;
    }

    #endregion
}

// ================================================================
// 설치된 유닛 1개를 나타내는 경량 데이터 객체 (MonoBehaviour 아님)
// GridManager 내부에서만 사용하므로 같은 파일에 선언
// ================================================================
public class PlacedUnit
{
    public UnitDataSO Data { get; }
    public Vector2Int OriginCell { get; }  // footprint 좌하단 셀
    public GameObject Instance { get; }    // 씬에 생성된 실제 오브젝트

    public PlacedUnit(UnitDataSO data, Vector2Int origin, GameObject instance)
    {
        Data = data;
        OriginCell = origin;
        Instance = instance;
    }
}
