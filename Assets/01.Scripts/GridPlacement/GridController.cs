using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ================================================================
// 플레이어의 그리드 상호작용을 전부 담당 (씬에 1개만 존재)
// 담당:
//   1) 게임 시작 시 바퀴/코어 초기 배치
//   2) 1/2/3 키로 설치할 유닛 선택
//   3) 마우스 위치에 고스트 프리뷰 표시 (파란색=설치 가능, 빨간색=불가)
//   4) 좌클릭 → 설치, 우클릭 → 제거
// 의존: GridManager (실제 로직은 거기에 위임)
// ================================================================
public class GridController : MonoBehaviour
{
    // ==========================================
    // 참조
    // ==========================================
    [Header("References")]
    [SerializeField] private GridManager _grid;
    [SerializeField] private Camera _camera;

    // ==========================================
    // 초기 배치 (바퀴 3개 + 코어 1개)
    // ==========================================
    [Header("Initial Setup")]
    [SerializeField] private UnitDataSO _wheelData;
    [SerializeField] private UnitDataSO _coreData;
    [SerializeField] private Vector2Int[] _wheelCells =
    {
        new(12, 0), new(15, 0), new(18, 0)
    };
    [SerializeField] private Vector2Int _coreCell = new(15, 1);

    // ==========================================
    // 디버그 입력 (1/2/3 키로 설치할 유닛 선택)
    // 실제 게임에선 UI 버튼으로 교체 예정
    // ==========================================
    // [Header("Debug Unit Prefabs")]
    // [SerializeField] private GameObject _attackPrefab;   // 1번 키
    // [SerializeField] private GameObject _defensePrefab;  // 2번 키
    // [SerializeField] private GameObject _supportPrefab;  // 3번 키

    // ==========================================
    // 고스트 프리뷰 설정
    // ==========================================
    [Header("Ghost Preview")]
    [Tooltip("1x1 크기 반투명 스프라이트 프리팹. footprint 크기에 맞춰 복제돼 사용됨.")]
    [SerializeField] private SpriteRenderer _ghostCellPrefab;
    [SerializeField] private Color _validColor = new(0.2f, 0.5f, 1f, 0.5f);   // 설치 가능
    [SerializeField] private Color _invalidColor = new(1f, 0.2f, 0.2f, 0.5f); // 설치 불가

    [Tooltip("설치 가능 시 표시되는 유닛 스프라이트 프리뷰의 알파값 (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _spritePreviewAlpha = 0.7f;
    // ==========================================
    // 런타임 상태
    // ==========================================
    private UnitDataSO _selected;              // 현재 선택된 설치용 데이터
    private Transform _ghostRoot;              // 고스트 셀들의 부모 오브젝트
    private readonly List<SpriteRenderer> _ghostCells = new(); // 풀링된 고스트 셀들
    private SpriteRenderer _spritePreview;  // 유닛 미리보기 스프라이트

    // ==========================================
    // Unity 생명주기
    // ==========================================
    // 시작 시: 고스트 루트 생성 + 초기 유닛 배치
    private void Start()
    {
        BuildGhostHierarchy();

        foreach (var pos in _wheelCells)
            _grid.PlaceInitial(_wheelData, pos);
        _grid.PlaceInitial(_coreData, _coreCell);
    }

    // 매 프레임: 선택 → 프리뷰 → 클릭 순
    // GetCellUnderMouse는 1번만 호출하고 캐싱 (성능)
    private void Update()
    {
        HandleSelection();

        var cell = GetCellUnderMouse();
        UpdateGhost(cell);
        HandleClicks(cell);
    }

    private void BuildGhostHierarchy()
    {
        _ghostRoot = new GameObject("GhostRoot").transform;
        _ghostRoot.SetParent(transform);
        _ghostRoot.gameObject.SetActive(false);

        // 유닛 스프라이트 프리뷰용 오브젝트 (1개만 필요함)
        var previewGo = new GameObject("SpritePreview");
        previewGo.transform.SetParent(_ghostRoot, false);
        _spritePreview = previewGo.AddComponent<SpriteRenderer>();
        _spritePreview.sortingOrder = 11;   //배경 셀보다 위        
    }

    // ==========================================
    // 입력 처리
    // ==========================================
    // 1/2/3 키로 유닛 선택, ESC로 해제
    private void HandleSelection()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // if (kb.digit1Key.wasPressedThisFrame)      Select(_attackPrefab);
        // else if (kb.digit2Key.wasPressedThisFrame) Select(_defensePrefab);
        // else if (kb.digit3Key.wasPressedThisFrame) Select(_supportPrefab);
        // else if (kb.escapeKey.wasPressedThisFrame) Deselect();
    }

    // 좌클릭 → 설치, 우클릭 → 제거
    private void HandleClicks(Vector2Int cell)
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame && _selected != null)
            _grid.TryPlace(_selected, cell);

        if (mouse.rightButton.wasPressedThisFrame)
            _grid.TryRemove(cell);
    }

    // 마우스 화면 좌표 → 셀 좌표
    // 카메라는 Orthographic 권장 (Perspective면 Z 보정 필요)
    private Vector2Int GetCellUnderMouse()
    {
        var screen = Mouse.current.position.ReadValue();
        var world = _camera.ScreenToWorldPoint(new Vector3(
            screen.x, screen.y, -_camera.transform.position.z));
        return _grid.WorldToCell(world);
    }

    // 프리팹에서 Unit 컴포넌트를 꺼내 SO 추출 → 선택 상태로 저장 + 고스트 크기 세팅
    private void Select(GameObject prefab)
    {
        if (prefab == null || !prefab.TryGetComponent(out Unit unit))
        {
            Debug.LogWarning("[GridController] 프리팹에 Unit 컴포넌트가 없음");
            return;
        }
        _selected = unit.Data;
        var prefabSprite = prefab.GetComponent<SpriteRenderer>();

        Debug.Log($"[Select] {prefab.name} / SpriteRenderer : {prefabSprite} / Sprite : {prefabSprite?.sprite}");
        //프리팹에서 스프라이트 추출
        ShowGhost(_selected.Size, prefabSprite != null ? prefabSprite.sprite : null);
    }

    public void SelectByData(UnitDataSO data)
    {
        if (data == null) { Deselect(); return; }
        _selected = data;
        Sprite sprite = null;
        if (data.Prefab != null && data.Prefab.TryGetComponent(out SpriteRenderer sr))
            sprite = sr.sprite;
        ShowGhost(data.Size, sprite);
    }

    private void Deselect()
    {
        _selected = null;
        _ghostRoot.gameObject.SetActive(false);
    }

    // ==========================================
    // 고스트 프리뷰 (파란색/빨간색 미리보기)
    // ==========================================
    // footprint 크기에 맞춰 고스트 셀 개수를 조정하고 배치
    // 이미 만들어진 셀은 재활용 (풀링)
    private void ShowGhost(Vector2Int size, Sprite unitSprite)
    {
        float cellSize = _grid.CellSize;
        int needed = size.x * size.y;

        // 부족한 개수만큼 생성
        while (_ghostCells.Count < needed)
        {
            var cell = Instantiate(_ghostCellPrefab, _ghostRoot);
            _ghostCells.Add(cell);
        }

        // 필요한 개수만 활성화 + 위치 세팅, 나머지는 비활성화
        int i = 0;
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {   //셀 위치 : 좌하단 기준 + 셀 중앙 오프셋
                _ghostCells[i].transform.localPosition = new Vector3(
                    x * cellSize + cellSize * 0.5f,
                    y * cellSize + cellSize * 0.5f, 0f);
                // 셀 자체도 CellSize에 맞춤
                _ghostCells[i].transform.localScale = new Vector3(cellSize, cellSize, 1f);
                _ghostCells[i].gameObject.SetActive(true);
                i++;
            }
        }
        for (; i < _ghostCells.Count; i++)
            _ghostCells[i].gameObject.SetActive(false);
        
        // -- 유닛 스프라이트 프리뷰 --
        _spritePreview.sprite = unitSprite;
        // footprint 전체 크기에 맞춤
        _spritePreview.transform.localScale = new Vector3(size.x * cellSize, size.y * cellSize, 1f);
        // footprint 중앙에 배치
        _spritePreview.transform.localPosition = new Vector3(size.x * cellSize * 0.5f, size.y * cellSize * 0.5f, 0f);

        _ghostRoot.gameObject.SetActive(true);
    }

    // 매 프레임 마우스 셀로 이동 + 설치 가능 여부로 색 결정
    private void UpdateGhost(Vector2Int cell)
    {
        if (_selected == null) return;

        //고스트 위치 : 셀의 좌하단 코너 (중앙 오프셋은 ShowGhost 내부 셀 배치에서 처리하기)
        _ghostRoot.position = _grid.CellToWorld(cell)
                            - new Vector3(_grid.CellSize * 0.5f, _grid.CellSize * 0.5f, 0f);
        
        bool CanPlace = _grid.CanPlace(_selected, cell); 
        var color = CanPlace ? _validColor : _invalidColor;

        for (int i = 0; i < _ghostCells.Count; i++)
        {
            if (_ghostCells[i].gameObject.activeSelf)
                _ghostCells[i].color = color;
        }

        // 스프라이트 프리뷰 : 설치 가능할 때만 표시
        if(CanPlace && _spritePreview.sprite != null)
        {
            _spritePreview.enabled = true;
            var c = Color.white;
            c.a = _spritePreviewAlpha;
            _spritePreview.color = c;
        }
        else
        {
            _spritePreview.enabled = false;
        }
    }
}