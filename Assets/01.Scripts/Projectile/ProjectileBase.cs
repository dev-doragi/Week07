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

    protected virtual void ProcessHit(IDamageable target, Vector2 hitPoint)
    {
        if (target.Team == _attackerTeam || target.IsDead) return;

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
                Despawn();
                break;

            case AreaType.Splash:
                Explode(hitPoint);
                Despawn();
                break;

            case AreaType.Piercing:
                data.IsPiercing = true;
                target.TakeDamage(data);
                _currentDamage *= _attackData.PiercingDecay;
                if (_remainingPiercing-- <= 0) Despawn();
                break;
        }
    }

    private void Explode(Vector2 center)
    {
        Collider2D[] targets = Physics2D.OverlapCircleAll(center, _attackData.RangeRadius);
        foreach (var col in targets)
        {
            if (col.TryGetComponent(out IDamageable target) && target.Team != _attackerTeam)
            {
                target.TakeDamage(new DamageData { Damage = _currentDamage, HitPoint = center });
            }
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