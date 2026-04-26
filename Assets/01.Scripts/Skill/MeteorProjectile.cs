using UnityEngine;

public class MeteorProjectile : MonoBehaviour
{
    private float _damage;
    private float _splashRadius;
    private Vector3 _from;
    private Vector3 _to;
    private float _duration;
    private float _elapsed;
    private bool _launched;

    private void OnEnable()
    {
        _launched = false;
    }

    public void Launch(Vector3 from, Vector3 to, float duration, float damage, float splashRadius)
    {
        Debug.Log("[Meteor] Launch 호출됨");
        _from = from;
        _to = to;
        _duration = Mathf.Max(0.01f, duration);
        _damage = damage;
        _splashRadius = splashRadius;
        _elapsed = 0f;
        _launched = true;
        transform.position = from;
    }

    private void Update()
    {
        if(!_launched) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        // 낙하 중 약간의 X 흔들림으로 각 메테오마다 다른 느낌
        Vector3 linear = Vector3.Lerp(_from, _to, t);
        float wobble = Mathf.Sin(t * Mathf.PI * 2f) * 0.3f;
        transform.position = linear + new Vector3(wobble, 0f, 0f);

        if(t >= 1f) Explode();
    }

    private void Explode()
    {
        _launched = false;
        Vector3 explodePos = transform.position;    // _to 대신 현재 위치

        var hits = Physics2D.OverlapCircleAll(explodePos, _splashRadius);
        foreach(var col in hits)
        {
            if(!col.TryGetComponent(out IDamageable target)) continue;
            if(target.Team != TeamType.Enemy || target.IsDead) continue;

            target.TakeDamage(new DamageData
            {
                Damage = _damage,
                AttackerTeam = TeamType.Player,
                HitPoint = _to
            });
        }

        if(PoolManager.Instance != null)
            PoolManager.Instance.Despawn(gameObject);
        else
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!_launched) return;
        if(!other.TryGetComponent(out IDamageable target)) return;
        if(target.Team != TeamType.Enemy || target.IsDead) return;

        Explode();
    }

}
