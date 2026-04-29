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

        if (obj.TryGetComponent(out RatArcProjectile ratBomb))
        {
            ratBomb.Initialize(attackData, attacker.Team, startPos, target.transform.position, _travelTime, _arcHeight, attacker.gameObject);
            return true;
        }

        if (obj.TryGetComponent(out ArcProjectile projectile))
        {
            projectile.Initialize(attackData, attacker.Team, startPos, target.transform.position, _travelTime, _arcHeight, attacker.gameObject);
            return true;
        }
        return false;
    }

    // Ensure the spawn point is assigned. If null, try to find a child transform
    // with the provided name (searching recursively) and assign it.
    public void EnsureSpawnPoint(string childName)
    {
        if (_spawnPoint != null) return;

        // Quick non-recursive lookup first
        var direct = transform.Find(childName);
        if (direct != null)
        {
            _spawnPoint = direct;
            return;
        }

        // Recursive search
        Transform found = FindInChildren(transform, childName);
        if (found != null)
        {
            _spawnPoint = found;
        }
    }

    private Transform FindInChildren(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var f = FindInChildren(child, name);
            if (f != null) return f;
        }
        return null;
    }
}
