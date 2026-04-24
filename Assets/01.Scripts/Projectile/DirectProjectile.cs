using UnityEngine;

public class DirectProjectile : ProjectileBase
{
    private Vector3 _startPos;
    private Vector3 _targetPos;
    private float _speed;
    private float _elapsedTime;
    private float _duration;
    private bool _isInitialized;

    public void Initialize(AttackModule data, TeamType team, Vector3 start, Vector3 target, float speed)
    {
        base.Launch(data, team);
        _startPos = start;
        _targetPos = target;
        _speed = speed;

        float distance = Vector3.Distance(start, target);
        _duration = distance / Mathf.Max(0.01f, _speed);
        _elapsedTime = 0f;
        _isInitialized = true;

        transform.position = _startPos;

        // 목표 방향에 맞게 초기 회전 설정
        Vector3 direction = (_targetPos - _startPos).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    private void Update()
    {
        if (!_isInitialized) return;

        _elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsedTime / _duration);
        transform.position = Vector3.Lerp(_startPos, _targetPos, t);

        // 이동 방향에 맞게 지속적으로 회전 업데이트
        Vector3 direction = (_targetPos - _startPos).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        if (t >= 1f) Despawn();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isInitialized) return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null)
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