using System.Collections.Generic;
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
    public E_TeamType Team;
    public E_UnitCategory Category;
    public GameObject Prefab;
    public Sprite Icon;

    [Header("Grid & Placement (그리드 배치 규칙)")]
    [Tooltip("유닛 footprint 크기. (2, 1)이면 가로 2칸 세로 1칸.")]
    public Vector2Int Size = Vector2Int.one;

    [Tooltip("그리드 매니저가 이 유닛을 설치할 때 검사할 조건")]
    public PlacementRule PlacementRule;

    [Tooltip("이 유닛 위에 다른 유닛(NeedsFoundationBelow)을 올릴 수 있는지 여부")]
    public bool ActsAsFoundation;

    [Header("Core Stats (공통 스탯)")]
    public float MaxHp;
    [Range(0f, 1f)] public float BaseDefenseRate;
    public int Cost;

    [Header("Death Event Data")]
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
    public bool CanCollide => Defense != null && Defense.CollisionPower > 0;
    public bool CanSupport => Support != null && Support.Radius > 0;
}

// ================================================================
// 3. 모듈 데이터 구조체 (Serializable)
// ================================================================

[System.Serializable]
public class AttackModule
{
    public float Damage;
    public float Speed;
    public float Distance;       // 사거리 (거리 계산용)
    public int RangeRadius;      // 광역 공격 반경 (0이면 단일 타겟)
    public E_AttackTrajectoryType Trajectory; // Direct / Arc
    [Range(0f, 1f)] public float Penetration; // 방어력 관통 비율
    public int AttackCost; // 공격 시 소모 비용
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