using UnityEngine;

/// <summary>
/// 메테오 투사체 - 낙하 후 폭발 이펙트 스폰 및 데미지 처리
/// </summary>
public class MeteorProjectile : MonoBehaviour
{
    [Header("Explosion")]
    [SerializeField] private string _explosionEffectPoolKey;

    [Header("Audio")]
    [SerializeField] private AudioClip _impactSFX;

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
        if (!_launched) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        // 낙하 중 약간의 X 흔들림으로 각 메테오마다 다른 느낌
        Vector3 linear = Vector3.Lerp(_from, _to, t);
        float wobble = Mathf.Sin(t * Mathf.PI * 2f) * 0.3f;
        transform.position = linear + new Vector3(wobble, 0f, 0f);

        if (t >= 1f) OnImpact();
    }

    private void OnImpact()
    {
        _launched = false;
        Vector3 explodePos = transform.position;

        // 폭발 이펙트 스폰 (풀링)
        if (!string.IsNullOrEmpty(_explosionEffectPoolKey) && PoolManager.Instance != null)
        {
            GameObject fx = PoolManager.Instance.Spawn(_explosionEffectPoolKey, explodePos, Quaternion.identity);
            if (fx != null)
            {
                var ps = fx.GetComponent<ParticleSystem>();
                if (ps != null && fx.GetComponent<DespawnController>() == null)
                    fx.AddComponent<DespawnController>().Setup(ps);
            }
        }

        // SFX 재생
        if (_impactSFX != null)
            SoundManager.Instance.PlaySFX(_impactSFX, explodePos, 1f);

        // 카메라 쉐이크 (작게)
        CameraManager.Instance?.ShakeWeak();

        // 데미지 처리
        ApplyDamage(explodePos);

        // 디스폰
        if (PoolManager.Instance != null)
            PoolManager.Instance.Despawn(gameObject);
        else
            Destroy(gameObject);
    }

    private void ApplyDamage(Vector3 explodePos)
    {
        var hits = Physics2D.OverlapCircleAll(explodePos, _splashRadius);
        foreach (var col in hits)
        {
            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            if (target.Team != TeamType.Enemy || target.IsDead) continue;

            target.TakeDamage(new DamageData
            {
                Damage = _damage,
                AttackerTeam = TeamType.Player,
                HitPoint = explodePos
            });
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_launched) return;
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null) return;
        if (target.Team != TeamType.Enemy || target.IsDead) return;

        OnImpact();
    }
}
