using System.Collections.Generic;
using UnityEngine;

public class RitualWallHitCounter : MonoBehaviour
{
    private readonly HashSet<int> _countedProjectileIds = new HashSet<int>();
    private int _hitCount;
    private bool _isRecording;

    public void BeginRecord()
    {
        _hitCount = 0;
        _countedProjectileIds.Clear();
        _isRecording = true;
    }

    public int EndRecord()
    {
        _isRecording = false;
        int result = _hitCount;
        _countedProjectileIds.Clear();
        _hitCount = 0;
        return result;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isRecording || other == null)
            return;

        ProjectileBase projectile = other.GetComponentInParent<ProjectileBase>();
        if (projectile == null || projectile.AttackerTeam != TeamType.Enemy)
            return;

        int projectileId = projectile.gameObject.GetInstanceID();
        if (!_countedProjectileIds.Add(projectileId))
            return;

        _hitCount++;
    }
}
