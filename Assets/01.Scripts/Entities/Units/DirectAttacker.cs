using UnityEngine;

public class DirectAttacker : MonoBehaviour, IAttacker
{
    [SerializeField] private float _projectileSpeed = 15f;
    [SerializeField] private Transform _spawnPoint;

    public bool TryPerformAttack(Unit attacker, Unit target, AttackModule attackData)
    {
        if (attackData.ProjectilePrefab == null) return false;

        Vector3 startPos = _spawnPoint != null ? _spawnPoint.position : transform.position;
        GameObject obj = PoolManager.Instance.Spawn(attackData.ProjectilePrefab.name, startPos, Quaternion.identity);

        if (obj.TryGetComponent(out RatProjectile ratProjectile))
        {
            ratProjectile.Initialize(attackData, attacker.Team, startPos, target.transform.position, _projectileSpeed);
            return true;
        }

        if (obj.TryGetComponent(out DirectProjectile projectile))
        {
            projectile.Initialize(attackData, attacker.Team, startPos, target.transform.position, _projectileSpeed);
            return true;
        }
        return false;
    }
}
