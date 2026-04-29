using UnityEngine;

public class DirectProjectile : ProjectileBase
{
    private Vector3 _startPos;
    private Vector3 _targetPos;
    private float _speed;
    private float _elapsedTime;
    private float _duration;
    private bool _isInitialized;

    public void Initialize(AttackModule data, TeamType team, Vector3 start, Vector3 target, float speed, GameObject owner = null)
    {
        base.Launch(data, team, owner);
        _startPos = start;
        _targetPos = target;
        _speed = speed;

        float distance = Vector3.Distance(start, target);
        _duration = distance / Mathf.Max(0.01f, _speed);
        _elapsedTime = 0f;
        _isInitialized = true;

        transform.position = _startPos;
        transform.rotation = ResolveRotation();
    }

    private Quaternion ResolveRotation()
    {
        if (_attackerTeam == TeamType.Player)
            return Quaternion.identity;

        Vector3 direction = (_targetPos - _startPos).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f;
        return Quaternion.AngleAxis(angle, Vector3.forward);
    }

    protected virtual void Update()
    {
        if (!_isInitialized) return;

        _elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsedTime / _duration);
        transform.position = Vector3.Lerp(_startPos, _targetPos, t);
        transform.rotation = ResolveRotation();

        if (t >= 1f)
        {
            OnImpact(transform.position);
            Despawn();
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isInitialized) return;

        if (other.CompareTag("RitualWall") && _attackerTeam == TeamType.Enemy)
        {
            Despawn();
            return;
        }

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null) return;

        if (target.Category == UnitCategory.Wheel) return;

        ProcessHit(target, transform.position);
    }

    protected override void Despawn()
    {
        _isInitialized = false;
        base.Despawn();
    }
}
