using UnityEngine;

/// <summary>
/// 투사체나 충돌체로부터 전달되는 타격 정보를 담는 데이터 구조체입니다.
/// </summary>

public struct DamageData
{
    public float Damage;
    public TeamType AttackerTeam;
    public Vector2 HitPoint;
    public bool IsPiercing;
}

public interface IDamageable
{
    TeamType Team { get; }
    UnitCategory Category { get; }
    bool IsDead { get; }
    void TakeDamage(DamageData damageData);
}