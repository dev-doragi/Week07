using UnityEngine;

/// <summary>
/// 유닛의 충돌 데미지 및 방어 관련 로직을 담당하는 실행 모듈입니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - DefenseModule 기반의 충돌 파워(CollisionPower) 관리
/// - 물리 충돌 발생 시 상대 유닛에게 데미지 전달
/// 
/// [이벤트 흐름]
/// - Subscribe: (물리 충돌 이벤트 수신 시 작동)
/// </remarks>
public class EntityDefender : MonoBehaviour
{
    private Unit _owner;
    private DefenseModule _data;

    /// <summary>
    /// Unit 컨트롤러에 의해 호출되어 모듈을 초기화합니다.
    /// </summary>
    public void Setup(Unit owner, DefenseModule data)
    {
        _owner = owner;
        _data = data;
    }

    /// <summary>
    /// 물리적 충돌이 발생했을 때 호출됩니다. (PartCell 또는 자체 Collider에서 연동)
    /// </summary>
    /// <param name="target">충돌한 대상 Unit</param>
    public void OnCollisionDetected(Unit target)
    {
        if (_owner == null || _owner.IsDead || _data == null) return;
        if (target == null || target.IsDead) return;

        // 상대방과 팀이 다를 경우에만 데미지 적용
        if (target.Team != _owner.Team)
        {
            ApplyCollisionDamage(target);
        }
    }

    private void ApplyCollisionDamage(Unit target)
    {
        // DefenseModule에 설정된 CollisionPower만큼 상대방에게 데미지 전달
        target.TakeDamage(_data.CollisionPower);

        Debug.Log($"[{_owner.Data.UnitName}]이 [{target.Data.UnitName}]에게 충돌 데미지({_data.CollisionPower})를 입혔습니다.");
    }
}