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

        // 이 유닛이 차지한 모든 셀을 비움
        for (int x = 0; x < unit.Data.Size.x; x++)
        {
            for (int y = 0; y < unit.Data.Size.y; y++)
            {
                _cells[unit.OriginCell.x + x, unit.OriginCell.y + y] = null;
            }
        }
        Destroy(unit.Instance);
        return true;
    }

    // 실제 프리팹 생성 + 그리드 배열에 등록 (TryPlace/PlaceInitial 공용)
    private void CreateAndRegister(UnitDataSO data, Vector2Int origin)
    {
        var instance = Instantiate(data.Prefab, CellToWorld(origin), Quaternion.identity, transform);
        var placed = new PlacedUnit(data, origin, instance);
        for (int x = 0; x < data.Size.x; x++)
        {
            for (int y = 0; y < data.Size.y; y++)
            {
                _cells[origin.x + x, origin.y + y] = placed;
            }
        }
    }

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