using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Enemy-specific grid manager.
// Keeps the same placement and collapse logic as GridManager, but with its own runtime type.
public class EnemyGridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int _width = 30;
    [SerializeField] private int _height = 20;
    [SerializeField] private float _cellSize = 1f;
    [SerializeField] private Vector3 _origin = Vector3.zero;

    private EnemyPlacedUnit[,] _cells;
    private Rigidbody2D _rb;
    private bool _runtimeUnitsRegistered = false;

    private readonly Vector2Int[] _fourDirections =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public int Width => _width;
    public int Height => _height;
    public float CellSize => _cellSize;

    private void Awake()
    {
        EnsureGridStorage();
    }

    //코어 파괴시 유닛 전체 제거 코드
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

        // 적 코어가 파괴됐을 때만 적 유닛 전체 제거
        if (e.IsPlayerBase) return;

        // 씬에 있는 모든 적 Unit 찾아서 즉사 
        var allEnemies = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        foreach(var unit in allEnemies)
        {
            if(unit.Team == TeamType.Enemy && !unit.IsDead)
            {
                unit.ForceKill();
            }
        }
    }

    private void EnsureGridStorage()
    {
        if (_cells == null || _cells.GetLength(0) != _width || _cells.GetLength(1) != _height)
        {
            _cells = new EnemyPlacedUnit[_width, _height];
        }

        if (_rb == null)
        {
            _rb = GetComponent<Rigidbody2D>();
        }
    }

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

    public bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < _width
            && cell.y >= 0 && cell.y < _height;
    }

    public bool IsEmpty(Vector2Int cell)
    {
        EnsureGridStorage();
        return IsInBounds(cell) && _cells[cell.x, cell.y] == null;
    }

    public EnemyPlacedUnit GetUnitAt(Vector2Int cell)
    {
        EnsureGridStorage();
        return IsInBounds(cell) ? _cells[cell.x, cell.y] : null;
    }

    public List<EnemyPlacedUnit> GetAllPlacedUnitsSnapshot()
    {
        EnsureGridStorage();
        var set = new HashSet<EnemyPlacedUnit>();
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                if (_cells[x, y] != null)
                {
                    set.Add(_cells[x, y]);
                }
            }
        }

        return new List<EnemyPlacedUnit>(set);
    }

    public bool CanPlace(UnitDataSO data, Vector2Int origin)
    {
        if (data == null) return false;
        if (data.PlacementRule == PlacementRule.InitialOnly) return false;

        for (int x = 0; x < data.Size.x; x++)
        {
            for (int y = 0; y < data.Size.y; y++)
            {
                var cell = new Vector2Int(origin.x + x, origin.y + y);
                if (!IsInBounds(cell) || !IsEmpty(cell)) return false;
            }
        }

        return data.PlacementRule switch
        {
            PlacementRule.NeedsFoundationBelow => HasFoundationBelow(data, origin),
            PlacementRule.NeedsAdjacent => HasAnyAdjacent(data, origin),
            _ => false
        };
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

    public bool TryPlace(UnitDataSO data, Vector2Int origin)
    {
        if (!CanPlace(data, origin)) return false;
        if (data.Cost > 0 && ResourceManager.Instance != null)
        {
            int before = ResourceManager.Instance.CurrentMouse;
            if (!ResourceManager.Instance.SubtractMouseCount(data.Cost))
            {
                Debug.Log($"[EnemyGridManager] {data.UnitName} place failed | own: {before} / need: {data.Cost}");
                return false;
            }
            Debug.Log($"[EnemyGridManager] {data.UnitName} place success | used: {data.Cost} | {before} -> {ResourceManager.Instance.CurrentMouse}");
        }
        CreateAndRegister(data, origin);
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
                    Debug.LogError($"[EnemyGridManager] initial placement failed: {data.UnitName} @ {origin}");
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

        StartCollapse(unit);
        ScheduleCollapseCheck();

        return true;
    }

    /// <summary>
    /// Registers already-instantiated child Units (from authored enemy prefab)
    /// into runtime grid cells and collapse callbacks.
    /// Does not call Unit.InitializeRuntime().
    /// </summary>
    public void RegisterExistingUnitsFromChildren()
    {
        if (_runtimeUnitsRegistered) return;

        EnsureGridStorage();
        ClearGridCells();

        Unit[] units = GetComponentsInChildren<Unit>(includeInactive: true);
        foreach (Unit unit in units)
        {
            if (unit == null || unit.Data == null) continue;

            UnitDataSO data = unit.Data;
            Vector2Int origin = InferOriginFromCurrentTransform(unit.transform.position, data);
            if (!CanRegisterAt(origin, data))
            {
                Debug.LogWarning($"[EnemyGridManager] register skipped: {unit.name} @ {origin}");
                continue;
            }

            var placed = new EnemyPlacedUnit(data, origin, unit.gameObject);
            for (int x = 0; x < data.Size.x; x++)
            {
                for (int y = 0; y < data.Size.y; y++)
                {
                    _cells[origin.x + x, origin.y + y] = placed;
                }
            }

            unit.OnDead += _ => OnUnitDied(placed);
            unit.SetOnGrid(true);
        }

        _runtimeUnitsRegistered = true;

        // 적 유닛 등록 완료 후 공성력 UI 갱신 이벤트 발행
        EventBus.Instance?.Publish(new EnemyGridChangedEvent());
    }

    private void CreateAndRegister(UnitDataSO data, Vector2Int origin)
    {
        Vector3 footprintCenterOffset = new Vector3(
            (data.Size.x - 1) * _cellSize * 0.5f,
            (data.Size.y - 1) * _cellSize * 0.5f,
            0f);
        Vector3 spawnPosition = CellToWorld(origin) + footprintCenterOffset;
        var instance = Instantiate(data.Prefab, spawnPosition, Quaternion.identity, transform);

        var placed = new EnemyPlacedUnit(data, origin, instance);
        for (int x = 0; x < data.Size.x; x++)
        {
            for (int y = 0; y < data.Size.y; y++)
            {
                _cells[origin.x + x, origin.y + y] = placed;
            }
        }

        var unit = instance.GetComponentInChildren<Unit>();
        if (unit != null)
        {
            unit.InitializeRuntime();
            //unit.SetOnGrid(true);
            unit.OnDead += _ => OnUnitDied(placed);
        }
        // notify listeners that enemy grid changed (for CP UI refresh)
        EventBus.Instance?.Publish(new EnemyGridChangedEvent());
    }

    private void OnUnitDied(EnemyPlacedUnit placed)
    {
        if (GetUnitAt(placed.OriginCell) != placed) return;
        ForceRemove(placed);
    }

    public void ForceRemove(EnemyPlacedUnit unit)
    {
        if (unit == null) return;
        StartCollapse(unit);
        ScheduleCollapseCheck();
    }

    public int CalculateRightmostColumnDamage()
    {
        var counted = new HashSet<EnemyPlacedUnit>();
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
    /// 적 그리드에 배치된 전체 유닛의 CollisionPower 합산을 반환합니다.
    /// </summary>
    public float CalculateTotalCollisionPower()
    {
        var counted = new HashSet<EnemyPlacedUnit>();
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
    /// 적 그리드에 배치된 살아있는 Unit 컴포넌트 목록을 반환합니다.
    /// </summary>
    public List<Unit> GetAllLivingUnits()
    {
        var counted = new HashSet<EnemyPlacedUnit>();
        var result  = new List<Unit>();

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

    private Vector2Int InferOriginFromCurrentTransform(Vector3 worldPosition, UnitDataSO data)
    {
        Vector3 offset = new Vector3(
            (data.Size.x - 1) * _cellSize * 0.5f,
            (data.Size.y - 1) * _cellSize * 0.5f,
            0f);

        return WorldToCell(worldPosition - offset);
    }

    private bool CanRegisterAt(Vector2Int origin, UnitDataSO data)
    {
        for (int x = 0; x < data.Size.x; x++)
        {
            for (int y = 0; y < data.Size.y; y++)
            {
                var cell = new Vector2Int(origin.x + x, origin.y + y);
                if (!IsInBounds(cell)) return false;
                if (_cells[cell.x, cell.y] != null) return false;
            }
        }

        return true;
    }

    private void ClearGridCells()
    {
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                _cells[x, y] = null;
            }
        }
    }

    private const float COLLAPSE_DELAY = 0.15f;
    private bool _collapseScheduled = false;

    private void StartCollapse(EnemyPlacedUnit unit)
    {
        for (int x = 0; x < unit.Data.Size.x; x++)
        {
            for (int y = 0; y < unit.Data.Size.y; y++)
            {
                _cells[unit.OriginCell.x + x, unit.OriginCell.y + y] = null;
            }
        }

        var go = unit.Instance;
        go.transform.SetParent(null, true);

        var col = go.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        var falling = go.AddComponent<FallingUnit>();
        falling.Begin();
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

    private List<EnemyPlacedUnit> CollectAllPlaced()
    {
        EnsureGridStorage();
        var set = new HashSet<EnemyPlacedUnit>();
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                if (_cells[x, y] != null) set.Add(_cells[x, y]);
            }
        }
        return new List<EnemyPlacedUnit>(set);
    }

    private List<EnemyPlacedUnit> FindUnsupportedUnits()
    {
        var allPlaced = CollectAllPlaced();
        var supported = new HashSet<EnemyPlacedUnit>();

        foreach (var unit in allPlaced)
        {
            if (unit.Data.PlacementRule == PlacementRule.InitialOnly)
                supported.Add(unit);
        }

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

        var result = new List<EnemyPlacedUnit>();
        foreach (var unit in allPlaced)
        {
            if (!supported.Contains(unit)) result.Add(unit);
        }
        return result;
    }

    private bool IsSupportedBy(EnemyPlacedUnit unit, HashSet<EnemyPlacedUnit> supported)
    {
        return unit.Data.PlacementRule switch
        {
            PlacementRule.InitialOnly => true,
            PlacementRule.NeedsFoundationBelow => HasSupportedFoundationBelow(unit, supported),
            PlacementRule.NeedsAdjacent => HasSupportedAdjacent(unit, supported),
            _ => false
        };
    }

    private bool HasSupportedFoundationBelow(EnemyPlacedUnit unit, HashSet<EnemyPlacedUnit> supported)
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

    private bool HasSupportedAdjacent(EnemyPlacedUnit unit, HashSet<EnemyPlacedUnit> supported)
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
}

public class EnemyPlacedUnit
{
    public UnitDataSO Data { get; }
    public Vector2Int OriginCell { get; }
    public GameObject Instance { get; }

    public EnemyPlacedUnit(UnitDataSO data, Vector2Int origin, GameObject instance)
    {
        Data = data;
        OriginCell = origin;
        Instance = instance;
    }
}
