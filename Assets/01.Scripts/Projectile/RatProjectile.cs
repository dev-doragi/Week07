using UnityEngine;

/// <summary>
/// DirectProjectile 기반 단일 대상 투사체.
/// 이동/데미지 판정은 DirectProjectile에 완전히 위임하고,
/// OnImpact에서 파편 생성만 담당합니다.
/// </summary>
public class RatProjectile : DirectProjectile
{
    [SerializeField] private string _fragmentPoolKey;
    [SerializeField] private int _fragmentCount = 3;

    public void Initialize(AttackModule data, TeamType team, Vector3 startPosition, Vector3 targetPosition, float speed, GameObject owner = null)
    {
        if (data == null)
        {
            Debug.LogError($"{name}: Initialize failed - AttackModule is null.");
            return;
        }

        base.Initialize(data, team, startPosition, targetPosition, speed, owner);
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
