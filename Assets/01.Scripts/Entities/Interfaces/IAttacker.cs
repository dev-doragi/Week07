using UnityEngine;

/// <summary>
/// 투사체 생성 및 발사(직사, 곡사 등)의 실제 물리적 처리를 담당하는 인터페이스
/// 기존 BaseAttackPerformer를 대체합니다.
/// </summary>
public interface IAttacker
{
    /// <summary>
    /// 공격을 물리적으로 실행(투사체 생성 등)합니다.
    /// </summary>
    /// <param name="attacker">공격하는 유닛</param>
    /// <param name="target">타겟 컴포넌트(유닛/영웅 등)</param>
    /// <param name="attackData">공격 스탯 (데미지, 관통력 등)</param>
    /// <returns>정상 실행 여부</returns>
    bool TryPerformAttack(Unit attacker, Component target, AttackModule attackData);
}
