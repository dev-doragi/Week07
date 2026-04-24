using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================================================
// 그리드의 모든 것을 담당하는 매니저 (씬에 1개만 존재)
// 담당:
//   1) 그리드 데이터 저장 (어떤 셀에 뭐가 있는지)
//   2) 좌표 변환 (월드 ↔ 셀)
//   3) 설치 가능 여부 검증 (규칙 체크)
//   4) 실제 설치/제거 (프리팹 Instantiate/Destroy)
//   5) 에디터에서 그리드 라인 시각화 (Gizmo)
// ================================================================
public class GridManager : MonoBehaviour
{
    // ==========================================
    // 인스펙터 설정
    // ==========================================
    [Header("Grid Settings")]
    [SerializeField] private int _width = 30;
    [SerializeField] private int _height = 20;
    [SerializeField] private float _cellSize = 1f;
    [SerializeField] private Vector3 _origin = Vector3.zero; // 그리드 (0,0)의 월드 위치

    // ==========================================
    // 내부 상태
    // ==========================================
    // 각 셀에 어떤 유닛이 있는지. null이면 비어있음.
    // 같은 유닛이 여러 셀을 차지하면 그 셀들 전부에 동일 참조가 들어감.
    private PlacedUnit[,] _cells;
    private Rigidbody2D _rb;

    // 인접 방향
    private readonly Vector2Int[] _fourDirections =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public int Width => _width;
    public int Height => _height;
    public float CellSize => _cellSize;

    // ==========================================
    // Unity 생명주기
    // ==========================================
    // 그리드 배열을 주어진 크기로 초기화. 모든 셀은 null 상태로 시작.
    private void Awake()
    {
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

    private void OnCoreDestroyed(CoreDestroyedEvent e)  // 코어 파괴 시 해당 팀의 모든 그리드 유닛 제거
    {   //플레이어 코어가 파괴됐을 때만 플레이어 그리드 유닛 전체 제거
        if(!e.IsPlayerBase) return;
        
        Debug.Log("[GridManager] 플레이어 코어 파괴 -> 전체 유닛 제거");

        var allUnits = CollectAllPlaced();
        foreach (var unit in allUnits)
        {
            StartCollapse(unit);
        }
    }

    // ==========================================
    // 좌표 변환 (마우스/카메라 ↔ 그리드)
    // ==========================================
    // 셀 좌표 → 월드 좌표 (셀의 좌하단 모서리 기준)
    // GridController가 프리뷰/유닛 위치 잡을 때 사용
    public Vector3 CellToWorld(Vector2Int cell)
    {
        float half = _cellSize * 0.5f;
        return _origin + new Vector3(
            cell.x * _cellSize + half,
            cell.y * _cellSize + half, 0f);
    }

    // 월드 좌표 → 셀 좌표
    // 마우스 위치를 셀로 변환할 때 사용
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
    // 셀이 그리드 범위 안에 있는지. 배열 접근 전 항상 체크
    public bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < _width
            && cell.y >= 0 && cell.y < _height;
    }

    // 셀이 비어있는지 (범위 밖이면 false)
    public bool IsEmpty(Vector2Int cell)
    {
        return IsInBounds(cell) && _cells[cell.x, cell.y] == null;
    }

    // 셀에 있는 유닛 반환 (비어있거나 범위 밖이면 null)
    public PlacedUnit GetUnitAt(Vector2Int cell)
    {
        return IsInBounds(cell) ? _cells[cell.x, cell.y] : null;
    }

    // ==========================================
    // 설치 가능 여부 검증
    // GridController가 매 프레임(프리뷰) + 클릭 시(실제 설치) 호출
    // ==========================================
    // 이 위치에 이 유닛을 놓을 수 있는가?
    public bool CanPlace(UnitDataSO data, Vector2Int origin)
    {
        if (data == null) return false;
        if (data.PlacementRule == PlacementRule.InitialOnly) return false;

        // footprint 내 모든 셀이 범위 안 + 비어있는지
        for (int x = 0; x < data.Size.x; x++)
        {
            for (int y = 0; y < data.Size.y; y++)
            {
                var cell = new Vector2Int(origin.x + x, origin.y + y);
                if (!IsInBounds(cell) || !IsEmpty(cell)) return false;
            }
        }

        // 규칙별 추가 검증
        return data.PlacementRule switch
        {
            PlacementRule.NeedsFoundationBelow => HasFoundationBelow(data, origin),
            PlacementRule.NeedsAdjacent        => HasAnyAdjacent(data, origin),
            _ => false
        };
    }

    // footprint 최하단 셀들 바로 아래가 전부 받침대인지 체크
    // 예: 2x2 유닛이 (5,5)에 놓일 때 → (5,4), (6,4)가 전부 ActsAsFoundation이어야 함
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

    // footprint 주변에 아무 유닛이라도 있으면 OK (방어 유닛용)
    // footprint 바깥 테두리를 한 칸씩 체크
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
                    // 자기 footprint 내부면 스킵
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
    // 플레이어의 일반 설치 (규칙 검증 후 생성)
    public bool TryPlace(UnitDataSO data, Vector2Int origin)
    {
        if (!CanPlace(data, origin)) return false;
        if (data.Cost > 0 && ResourceManager.Instance != null)
        {
            int before = ResourceManager.Instance.CurrentMouse;
            if (!ResourceManager.Instance.SubtractMouseCount(data.Cost))
            {
                Debug.Log($"[GridManager] {data.UnitName} 배치 실패 | 보유: {before} / 필요: {data.Cost}");
                return false;
            }
            Debug.Log($"[GridManager] {data.UnitName} 배치 완료 | 사용: {data.Cost} | {before} → {ResourceManager.Instance.CurrentMouse}");
        }
        CreateAndRegister(data, origin);
        return true;
    }

    // 게임 시작 시 바퀴/코어 강제 배치 (규칙 검증 생략, 범위/겹침만 체크)
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

    // 해당 셀의 유닛 제거 (InitialOnly는 제거 불가)
    public bool TryRemove(Vector2Int cell)
    {
        var unit = GetUnitAt(cell);
        if (unit == null) return false;
        if (unit.Data.PlacementRule == PlacementRule.InitialOnly) return false;

        //사전 검증 : 이 유닛을 빼면 붕괴가 발생하는지 판단
        if(WouldCauseCollapse(unit))
        {
            Debug.Log($"[GridManager] {unit.Data.UnitName} 제거 거부 | 연쇄 붕괴 발생 위험");
            return false;
        }

        // 환불
        if(unit.Data.Cost > 0 && ResourceManager.Instance != null)
        {
            int refund = Mathf.CeilToInt(unit.Data.Cost * 0.5f);
            ResourceManager.Instance.AddMouseCount(refund);
            Debug.Log($"[GridManager] {unit.Data.UnitName} 제거 | 환불: {refund} (원가: {unit.Data.Cost})");
        }

        StartCollapse(unit);
        ScheduleCollapseCheck();

        return true;
    }

    // 안전 제거 판단
    private bool WouldCauseCollapse(PlacedUnit unitToRemove)
    {   
        //임시로 그리드에서 제거 해보기
        for(int x = 0; x < unitToRemove.Data.Size.x; x++)
        {
            for(int y = 0; y < unitToRemove.Data.Size.y; y++)
            {
                _cells[unitToRemove.OriginCell.x + x, unitToRemove.OriginCell.y + y] = null;
            }
        }
        
        //이 상태에서 붕괴 대상이 있는지 체크
        var unsupported = FindUnsupportedUnits();

        for(int x = 0; x < unitToRemove.Data.Size.x; x++)
        {
            for(int y = 0; y < unitToRemove.Data.Size.y; y++)
            {
                _cells[unitToRemove.OriginCell.x + x, unitToRemove.OriginCell.y + y] = unitToRemove;
            }
        }
        return unsupported.Count > 0;
    }

    // footprint 전체의 월드 중심 좌표 반환
    private Vector3 FootprintCenter(UnitDataSO data, Vector2Int origin)
    {
        return _origin + new Vector3(
            (origin.x + data.Size.x * 0.5f) * _cellSize,
            (origin.y + data.Size.y * 0.5f) * _cellSize,
            0f);
    }

    // 실제 프리팹 생성 + 그리드 배열에 등록 (TryPlace/PlaceInitial 공용)
    private void CreateAndRegister(UnitDataSO data, Vector2Int origin)
    {
        var instance = Instantiate(data.Prefab, FootprintCenter(data, origin), Quaternion.identity, transform);

        float targetW = data.Size.x * _cellSize;
        float targetH = data.Size.y * _cellSize;

        var sr = instance.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            // 스프라이트 고유 월드 크기(scale=1일 때)로 나눠서 정규화
            var natural = sr.sprite.bounds.size;
            instance.transform.localScale = new Vector3(
                targetW / natural.x,
                targetH / natural.y, 1f);
        }
        else
        {
            instance.transform.localScale = new Vector3(targetW, targetH, 1f);
        }

        var placed = new PlacedUnit(data, origin, instance);
        for (int x = 0; x < data.Size.x; x++)
        {
            for (int y = 0; y < data.Size.y; y++)
            {
                _cells[origin.x + x, origin.y + y] = placed;
            }
        }

        // Uit의 사망 이벤트 구독 -> 전투 파괴 시 연쇄 붕괴 발동
        var unit = instance.GetComponentInChildren<Unit>();
        if(unit != null)
        {
            unit.InitializeRuntime();
            unit.OnDead += (deadUnit) => OnUnitDied(placed);
        }
    }

    private void OnUnitDied(PlacedUnit placed)
    {
        if(GetUnitAt(placed.OriginCell) != placed) return;
        
        ForceRemove(placed);
    }

    public void ForceRemove(PlacedUnit unit)
    {
        if (unit == null) return;

        Debug.Log($"[Gridmanager] {unit.Data.UnitName} 전투 파괴 | 연쇄 붕괴 체크 시작");
        
        StartCollapse(unit);
        ScheduleCollapseCheck();
    }
    /// <summary>
    /// 돌진 데미지 계산 (가장 오른쪽 열의 공격력 합)
    /// </summary>
    /// <returns></returns>
    public int CalculateRightmostColumnDamage()
    {
        var counted = new HashSet<PlacedUnit>();
        int totalDamage = 0;

        // 각 행y 마다 맨 오른쪽에 있는 Attack 유닛을 찾음
        for(int y = 0; y < _height; y++)
        {
            for(int x = _width - 1; x >= 0; x--)
            {
                var unit = _cells[x, y];
                if(unit == null) continue;

                // 이 행에서 맨 오른쪽 유닛 발견 -> Attack 이면 카운트
                if(unit.Data.Category == UnitCategory.Attack && counted.Add(unit))
                {
                    totalDamage += unit.Data.Attack != null ? (int)unit.Data.Attack.Damage : 0;
                }
                break;
            }
        }
        return totalDamage;
    }

#region 디버그 시각화

    // ==========================================
    // 디버그 시각화 (에디터에서 그리드 라인)
    // ==========================================
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
    //! ==========================================================
    //! 연쇄 붕괴 시스템
    //! ==========================================================
    private const float COLLAPSE_DELAY = 0.15f;
    private bool _collapseScheduled = false;
    
    //유닛 하나를 그리드에서 제거하고 떨어지는 연출로 전환
    private void StartCollapse(PlacedUnit unit)
    {
        // 1 그리드 배열에서 즉시 제거 (셀 비움 -> 재설치 가능 & 재검증에서 제외)
        for(int x = 0; x < unit.Data.Size.x; x++)
        {
            for(int y = 0; y < unit.Data.Size.y; y++)
            {
                _cells[unit.OriginCell.x + x, unit.OriginCell.y + y] = null;
            }
        }
        // 2 오브젝트를 그리드에서 분리 (그리드 이동과 독립)
        var go = unit.Instance;
        go.transform.SetParent(null, true);     //월드 좌표 유지
        
        // 3. 콜라이더 비활성화 (다른 유닛과 간섭 방지)
        var col = go.GetComponent<Collider2D>();
        if(col != null) col.enabled = false;

        // 4. 낙하 연출 시작
        var falling = go.AddComponent<FallingUnit>();
        falling.Begin();
    }

    //한 층이 연쇄 체크 (이미 돌고 있으면 중복 실행 방지)
    private void ScheduleCollapseCheck()
    {
        if(_collapseScheduled) return;
        _collapseScheduled = true;
        StartCoroutine(CollapseCheckLoop());
    }
    
    private IEnumerator CollapseCheckLoop()
    {
        while(true)
        {
            yield return new WaitForSeconds(COLLAPSE_DELAY);
            //전역 연결성 체크 (엥커로부터 flood fill)
            var toCollapse = FindUnsupportedUnits();

            if(toCollapse.Count == 0) break;

            //같은 층의 위반 유닛들은 도잇에 붕괴 (도미노 한 단계)
            foreach (var unit in toCollapse) StartCollapse(unit);
        }
        _collapseScheduled = false;
    }

    //이 유닛이 현재도 규칙을 만족하는가? (재 검증용)
    

    private List<PlacedUnit> CollectAllPlaced()
    {
        var set = new HashSet<PlacedUnit>();
        for(int x = 0; x < _width; x++)
        {
            for(int y = 0; y < _height; y++)
            {
                if(_cells[x, y] != null) set.Add(_cells[x, y]);
            }
        }
        return new List<PlacedUnit>(set);
    }

    private List<PlacedUnit> FindUnsupportedUnits()
    {
        var allPlaced = CollectAllPlaced();
        var supported = new HashSet<PlacedUnit>();

        // 1단계 : 앵커(Wheel/Core)는 항상 지지됨
        foreach(var unit in allPlaced)
        {
            if(unit.Data.PlacementRule == PlacementRule.InitialOnly)
                supported.Add(unit);
        }

        // 2단계 : 지지됨 유닛으로 부터 전파, 더 이상 추가 안될 때까지 반복
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach(var unit in allPlaced)
            {
                if(supported.Contains(unit)) continue;
                if(IsSupportedBy(unit, supported))
                {
                    supported.Add(unit);
                    changed = true;
                }
            }
        }

        // 3단계 : 지지됨 집합에 못 들어간 유닛들을 붕괴 대상으로 반환
        var result = new List<PlacedUnit>();
        foreach(var unit in allPlaced)
        {
            if(!supported.Contains(unit)) result.Add(unit);
        }
        return result;
    }

    // 이 유닛이 "이미 지지됨"으로 확정된 유닛들에 의해 지지받는가
    private bool IsSupportedBy(PlacedUnit unit, HashSet<PlacedUnit> supported)
    {
        return unit.Data.PlacementRule switch
        {
            PlacementRule.InitialOnly          => true,
            PlacementRule.NeedsFoundationBelow => HasSupportedFoundationBelow(unit, supported),
            PlacementRule.NeedsAdjacent        => HasSupportedAdjacent(unit, supported),
            _ => false
        };
    }

    // footprint 최하단 셀 바로 아래가 전부 "지지됨 Foundation"인지
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

    // footprint 주변 4방향에 "지지됨" 유닛이 하나라도 있는지
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

                    // 자기 footprint 내부면 스킵
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