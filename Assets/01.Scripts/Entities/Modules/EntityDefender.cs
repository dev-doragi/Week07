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

    public void Setup(Unit owner, DefenseModule data)
    {
        if (owner == null)
        {
            Debug.LogError($"[EntityDefender] {gameObject.name}: Owner Unit이 null입니다.");
            return;
        }
        _owner = owner;
        _data = data;
    }

    public void OnCollisionDetected(Unit target)
    {
        if (_owner == null || _owner.IsDead || _data == null) return;
        if (target == null || target.IsDead) return;

        if (target.Team != _owner.Team)
        {
            ApplyCollisionDamage(target);
        }
    }

    private void ApplyCollisionDamage(Unit target)
    {
        // 변경된 규격에 따라 DamageData 구조체 생성
        DamageData damageData = new DamageData
        {
            Damage = _data.CollisionPower,
            AttackerTeam = _owner.Team,
            HitPoint = transform.position,
            IsPiercing = false // 충돌 공격은 기본적으로 관통 판정 제외
        };

        // 수정한 TakeDamage 메서드 호출
        target.TakeDamage(damageData);

        Debug.Log($"[{_owner.Data.UnitName}] 충돌 공격 -> [{target.Data.UnitName}] 피해량: {_data.CollisionPower}");
    }
}