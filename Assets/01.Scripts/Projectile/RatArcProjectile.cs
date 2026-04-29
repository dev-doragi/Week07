using UnityEngine;

/// <summary>
/// ArcProjectile 기반 폭탄 투사체.
/// 이동/데미지 판정은 ArcProjectile에 완전히 위임하고,
/// OnImpact에서 파편 생성만 담당합니다.
/// AttackModule.Area == Splash이면 ProjectileBase.Explode가 범위 데미지를 처리합니다.
/// </summary>
public class RatArcProjectile : ArcProjectile
{
    [SerializeField] private string _fragmentPoolKey;
    [SerializeField] private int _fragmentCount = 3;

    public void Initialize(AttackModule data, TeamType team, Vector3 startPosition, Vector3 targetPosition, float travelTime, float arcHeight, GameObject owner = null)
    {
        if (data == null)
        {
            Debug.LogError($"{name}: Initialize failed - AttackModule is null.");
            return;
        }

        base.Initialize(data, team, startPosition, targetPosition, travelTime, arcHeight, owner);
    }

    protected override void OnImpact(Vector2 hitPoint)
    {
        if (!string.IsNullOrEmpty(_fragmentPoolKey))
        {
            for (int i = 0; i < _fragmentCount; i++)
            {
                PoolManager.Instance.Spawn(_fragmentPoolKey, transform.position, Quaternion.identity);
            }
        }

        base.OnImpact(hitPoint);
    }
}
