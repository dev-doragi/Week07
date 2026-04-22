using UnityEngine;

// ================================================================
// 유닛 프리팹 루트에 붙는 컴포넌트
// 역할: "이 프리팹이 어떤 UnitDataSO를 쓰는지" 연결하는 다리
// 사용처:
//   - GridController가 프리팹에서 Unit을 꺼내 → Unit.Data로 SO 획득
//   - 나중에 전투 시스템이 런타임 HP/쿨다운 같은 인스턴스 상태를 여기 들고 있게 확장 가능
// ================================================================
public class Unit : MonoBehaviour
{
    [SerializeField] private UnitDataSO _data;
    public UnitDataSO Data => _data;
}