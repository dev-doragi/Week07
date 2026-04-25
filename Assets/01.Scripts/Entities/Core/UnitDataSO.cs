using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;

// ================================================================
// UnitDataSO 클래스
// ================================================================
[CreateAssetMenu(fileName = "NewUnitData", menuName = "Data/Modular Unit Data")]
public class UnitDataSO : ScriptableObject
{
    [Header("Identity (기본 식별 정보)")]
    public int Key;
    public string UnitName;
    public TeamType Team;
    public UnitCategory Category;
    public GameObject Prefab;
    public Sprite Icon;

    [Header("Grid & Placement (그리드 배치 규칙)")]
    [Tooltip("유닛 footprint 크기. (2, 1)이면 가로 2칸 세로 1칸.")]
    public Vector2Int Size = Vector2Int.one;

    [Tooltip("그리드 매니저가 이 유닛을 설치할 때 검사할 조건")]
    public PlacementRule PlacementRule;

    [Tooltip("이 유닛 위에 다른 유닛(NeedsFoundationBelow)을 올릴 수 있는지 여부")]
    public bool ActsAsFoundation;

    [Header("Wheel Settings")]
    [Tooltip("Category가 Wheel일 때만 사용. 이 바퀴 1개가 제공하는 유닛 수용량.")]
    public int WheelCapacity = 5;

    [Header("Core Stats (공통 스탯)")]
    public float MaxHp;

    [Tooltip("기본 피해 경감률(0~1). 0.2 = 20% 감소")]
    [Range(0f, 1f)] public float BaseDefenseRate;

    [Tooltip("설치 코스트")]
    public int Cost;

    [Header("Death Event Data")]
    [Tooltip("유닛 사망시 스폰시킬 개체 코어면 DropRat, 일반 개체면 파티클 프리팹 하나")]
    public string DeathSpawnKey = "DropRat";
    public int BaseDeathSpawnCount = 25;

    // =======================================================================
    // 전투 및 행동 모듈 (Modules)
    // 인스펙터에서 필요한 기능에만 데이터를 할당하면, 유닛의 역할이 자동으로 결정됩니다.
    // =======================================================================

    [Header("Combat & Behavior Modules")]
    [Tooltip("공격 능력이 없다면 비워두세요(Null).")]
    public AttackModule Attack;

    [Tooltip("적에게 부딪혔을 때 충돌 피해를 주지 않는다면 비워두세요(Null).")]
    public DefenseModule Defense;

    [Tooltip("주변 아군에게 버프/힐을 주지 않는다면 비워두세요(Null).")]
    public SupportModule Support;

    // -----------------------------------------------------------------------
    // 편의성 프로퍼티 (외부 매니저나 핸들러가 호출할 때 사용)
    // -----------------------------------------------------------------------
    public bool CanAttack => Attack != null && Attack.Damage > 0;
    public bool CanHeal =>
        Category == UnitCategory.Support
        && Attack != null
        && Attack.Damage < 0
        && !Mathf.Approximately(Attack.Speed, 0f)
        && Attack.Distance > 0f;
    public bool CanCollide => Defense != null && Defense.CollisionPower > 0;
    public bool CanSupport => Support != null && Support.Radius > 0;
}

// ================================================================
// 3. 모듈 데이터 구조체 (Serializable)
// ================================================================

[System.Serializable]
public class AttackModule
{
    [Header("Base Stats")]
    [Tooltip("기본 피해량입니다.")]
    public float Damage;

    [Tooltip("공격 속도 (공격 간격 = 1 / Speed)")]
    public float Speed;

    [Tooltip("공격 사거리. 1단위당 그리드 한 칸")]
    public float Distance;

    [Tooltip("코스트")]
    public int AttackCost;

    [Header("Combat Policy")]
    [Tooltip("투사체가 날아가는 물리적 궤적 방식")]
    public AttackTrajectoryType Trajectory;

    [Tooltip("적을 탐색하는 AI 규칙")]
    public TargetingPolicy Targeting;

    [Tooltip("공격의 타격 범위 판정 방식")]
    public AreaType Area;

    [Header("Area Options")]
    [Tooltip("범위(Splash) 공격 시, 충돌 지점을 기준으로 타격을 입힐 원형 반경")]
    public float RangeRadius;

    [Tooltip("관통(Piercing) 공격 시, 투사체가 사라지기 전까지 관통할 수 있는 최대 타겟 수")]
    public int PiercingCount;

    [Tooltip("상대방의 방어력을 무시하는 비율(0~1)입니다. 1에 가까울수록 방어력을 완전무시")]
    [Range(0f, 1f)] public float Penetration;

    [Tooltip("관통 시마다 적용되는 데미지 감쇠율입니다. (예: 0.8이면 관통할 때마다 이전 데미지의 80%만 적용)")]
    [Range(0f, 1f)] public float PiercingDecay;

    [Tooltip("공격 실행 시 실제로 생성되어 날아갈 투사체 오브젝트")]
    public GameObject ProjectilePrefab;
}

[System.Serializable]
public class DefenseModule
{
    [Tooltip("접촉한 적에게 입히는 충돌 데미지")]
    public float CollisionPower;
}

[System.Serializable]
public class SupportModule
{
    [Tooltip("버프가 적용되는 반경")]
    public int Radius;

    [Tooltip("적용될 버프 효과들의 목록")]
    public List<PartSupportEffectData> Effects = new List<PartSupportEffectData>();
}
