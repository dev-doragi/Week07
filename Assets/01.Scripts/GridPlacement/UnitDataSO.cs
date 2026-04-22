using UnityEngine;

// ================================================================
// 유닛 카테고리 - 유닛의 종류를 구분하는 용도
// ================================================================
public enum UnitCategory
{
    Wheel,    // 탑승체의 바퀴 
    Core,     // 플레이어가 지켜야 할 핵심 (초기 배치)
    Attack,   // 공격 유닛 (아래에 받침대가 있어야 설치 가능)
    Defense,  // 방어 유닛 (인접 유닛이 있으면 공중에도 설치 가능, 받침대 역할 O)
    Support   // 지원 유닛 (공격 유닛과 동일한 설치 규칙)
}

// ================================================================
// 배치 규칙 - "이 유닛을 설치하려면 어떤 조건이 필요한가"
// GridManager.CanPlace()가 이 값을 보고 분기 처리
// ================================================================
public enum PlacementRule
{
    InitialOnly,          // 게임 시작 시에만 배치 (플레이어는 설치/제거 불가)
    NeedsFoundationBelow, // footprint 최하단 셀들 바로 아래가 전부 받침대여야 함
    NeedsAdjacent         // footprint 주변(상하좌우)에 다른 유닛이 하나라도 있어야 함
}

// ================================================================
// 유닛의 설계 데이터 (에셋으로 저장되는 불변 정보)
// 스탯/규칙/크기 등 "이 유닛은 무엇인가"의 정보를 담음
// 런타임에 바뀌는 값(현재 HP 등)은 절대 여기에 두지 말 것
// ================================================================
[CreateAssetMenu(menuName = "Units/Unit Data")]
public class UnitDataSO : ScriptableObject
{
    [SerializeField] private string _unitName;
    [SerializeField] private UnitCategory _category;

    [Tooltip("유닛 footprint 크기. (2, 1)이면 가로 2칸 세로 1칸.")]
    [SerializeField] private Vector2Int _size = Vector2Int.one;

    [SerializeField] private PlacementRule _placementRule;

    [Tooltip("이 유닛 위에 공격/지원 유닛을 올릴 수 있는지. 바퀴=O, 방어=O, 나머지=X")]
    [SerializeField] private bool _actsAsFoundation;

    [Tooltip("실제 씬에 생성될 시각적 프리팹")]
    [SerializeField] private GameObject _prefab;

    public string UnitName => _unitName;
    public UnitCategory Category => _category;
    public Vector2Int Size => _size;
    public PlacementRule PlacementRule => _placementRule;
    public bool ActsAsFoundation => _actsAsFoundation;
    public GameObject Prefab => _prefab;
}