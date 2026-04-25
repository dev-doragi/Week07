using UnityEngine;

public class ArcProjectile : ProjectileBase
{
    private Vector3 _startPos;
    private Vector3 _targetPos;
    private float _arcHeight;
    private float _duration;
    private float _elapsedTime;
    private bool _isInitialized;

    public void Initialize(AttackModule data, TeamType team, Vector3 start, Vector3 target, float duration, float arcHeight)
    {
        base.Launch(data, team);
        _startPos = start;
        _targetPos = target;
        _duration = Mathf.Max(0.01f, duration);
        _arcHeight = arcHeight;
        _elapsedTime = 0f;
        _isInitialized = true;
        transform.position = _startPos;
    }

    protected virtual void Update()
    {
        if (!_isInitialized) return;

        _elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsedTime / _duration);

        Vector3 linear = Vector3.Lerp(_startPos, _targetPos, t);
        float height = 4f * _arcHeight * t * (1f - t);
        transform.position = linear + Vector3.up * height;

        if (t >= 1f)
        {
            if (_attackData.Area == AreaType.Splash)
            {
                Explode(transform.position);
            }
            OnImpact(transform.position);
            Despawn();
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isInitialized) return;

        if (other.TryGetComponent(out IDamageable target))
        {
            ProcessHit(target, transform.position);
        }
    }

    protected override void Despawn()
    {
        _isInitialized = false;
        base.Despawn();
    }
}