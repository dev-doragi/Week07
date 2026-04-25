using System.Collections;
using UnityEngine;

public abstract class ProjectileBase : MonoBehaviour
{
    [Header("Lifecycle Settings")]
    [SerializeField] protected float _lifeTime = 5f;

    protected AttackModule _attackData;
    protected TeamType _attackerTeam;
    protected float _currentDamage;
    protected int _remainingPiercing;
    private Coroutine _lifeTimeCoroutine;

    protected virtual void OnEnable()
    {
        EventBus.Instance.Subscribe<StageCleanedUpEvent>(HandleStageCleanedUp);
        _lifeTimeCoroutine = StartCoroutine(LifeTimeRoutine());

        var rb = GetComponent<Rigidbody2D>();
        var col = GetComponent<Collider2D>();
        //Debug.Log($"[ProjectileBase.OnEnable] Rigidbody2D: {(rb != null ? $"✓ {rb.bodyType}" : "✗ NULL")}, " +
        //          $"Collider2D: {(col != null ? $"✓ IsTrigger={col.isTrigger}" : "✗ NULL")}, " +
        //          $"GameObject: {gameObject.name}", gameObject);
    }

    protected virtual void OnDisable()
    {
        EventBus.Instance.Unsubscribe<StageCleanedUpEvent>(HandleStageCleanedUp);
        if (_lifeTimeCoroutine != null) StopCoroutine(_lifeTimeCoroutine);
    }

    public virtual void Launch(AttackModule data, TeamType team)
    {
        _attackData = data;
        _attackerTeam = team;
        _currentDamage = data.Damage;
        _remainingPiercing = data.PiercingCount;
    }

    protected virtual void OnImpact(Vector2 hitPoint) { }

    protected virtual void ProcessHit(IDamageable target, Vector2 hitPoint)
    {
        if (target == null || target.Team == _attackerTeam || target.IsDead) return;

        if (target.Category == UnitCategory.Wheel) return;

        DamageData data = new DamageData
        {
            Damage = _currentDamage,
            AttackerTeam = _attackerTeam,
            HitPoint = hitPoint
        };

        switch (_attackData.Area)
        {
            case AreaType.Single:
                target.TakeDamage(data);
                if (_attackerTeam == TeamType.Player)
                    EventBus.Instance?.Publish(new EnemyHitEvent { AttackerTeam = _attackerTeam });
                OnImpact(hitPoint);
                Despawn();
                break;

            case AreaType.Splash:
                Explode(hitPoint);
                OnImpact(hitPoint);
                Despawn();
                break;

            case AreaType.Piercing:
                data.IsPiercing = true;
                target.TakeDamage(data);
                if (_attackerTeam == TeamType.Player)
                    EventBus.Instance?.Publish(new EnemyHitEvent { AttackerTeam = _attackerTeam });
                _currentDamage *= _attackData.PiercingDecay;
                if (_remainingPiercing-- <= 0)
                {
                    OnImpact(hitPoint);
                    Despawn();
                }
                break;
        }
    }

    protected void Explode(Vector2 center)
    {
        Collider2D[] targets = Physics2D.OverlapCircleAll(center, _attackData.RangeRadius);
        int hitCount = 0;

        foreach (var col in targets)
        {
            if (col.TryGetComponent(out IDamageable target)
                && target.Team != _attackerTeam
                && !target.IsDead)
            {
                target.TakeDamage(new DamageData { Damage = _currentDamage, HitPoint = center });
                hitCount++;
            }
        }

        if (_attackerTeam == TeamType.Player && hitCount > 0)
        {
            for (int i = 0; i < hitCount; i++)
                EventBus.Instance?.Publish(new EnemyHitEvent { AttackerTeam = _attackerTeam });
        }
    }

    private IEnumerator LifeTimeRoutine()
    {
        yield return new WaitForSeconds(_lifeTime);
        Despawn();
    }

    private void HandleStageCleanedUp(StageCleanedUpEvent evt) => Despawn();

    protected virtual void Despawn()
    {
        if (gameObject.activeInHierarchy) PoolManager.Instance.Despawn(gameObject);
    }
}