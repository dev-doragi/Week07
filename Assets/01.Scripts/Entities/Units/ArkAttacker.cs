using UnityEngine;

public class ArcAttacker : MonoBehaviour, IAttacker
{
    [SerializeField] private float _arcHeight = 3f;
    [SerializeField] private float _travelTime = 0.7f;
    [SerializeField] private Transform _spawnPoint;

    public bool TryPerformAttack(Unit attacker, Unit target, AttackModule attackData)
    {
        if (attackData.ProjectilePrefab == null) return false;

        Vector3 startPos = _spawnPoint != null ? _spawnPoint.position : transform.position;
        GameObject obj = PoolManager.Instance.Spawn(attackData.ProjectilePrefab.name, startPos, Quaternion.identity);

        if (obj.TryGetComponent(out ArcProjectile projectile))
        {
            projectile.Initialize(attackData, attacker.Team, startPos, target.transform.position, _travelTime, _arcHeight);
            return true;
        }
        return false;
    }
}