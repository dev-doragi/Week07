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
        // 풀에서 꺼낼 때 이전 위치가 남아 있을 수 있으므로 초기 위치를 명시적으로 설정
        transform.position = _startPos;
    }

    private void Update()
    {
        if (!_isInitialized) return;

        _elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsedTime / _duration);

        Vector3 linear = Vector3.Lerp(_startPos, _targetPos, t);
        float height = 4f * _arcHeight * t * (1f - t);
        transform.position = linear + Vector3.up * height;

        if (t >= 1f)
        {
            // 도착 시 Splash인 경우 직접 폭발 처리(부모의 Explode를 호출)
            if (_attackData.Area == AreaType.Splash)
            {
                Explode(transform.position);
            }
            Despawn();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isInitialized) return;

        if(other.CompareTag("RitualWall") && _attackerTeam == TeamType.Enemy)
        {
            Despawn();
            return;
        }

        // 비행 중 다른 대상과 부딪히는 판정
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