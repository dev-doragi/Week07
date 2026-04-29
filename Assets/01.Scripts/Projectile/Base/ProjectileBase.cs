using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ProjectileBase : MonoBehaviour
{
    [Header("Lifecycle Settings")]
    [SerializeField] protected float _lifeTime = 5f;

    [Header("Audio")]
    [SerializeField] protected AudioClip _impactSFX;

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

        GameCsvLogger.Instance.LogEvent(
            eventType: GameLogEventType.ProjectileSpawned,
            actor: gameObject,
            value: _currentDamage,
            metadata: new Dictionary<string, object>
            {
                { "attackerTeam", _attackerTeam.ToString() },
                { "area", _attackData != null ? _attackData.Area.ToString() : "Unknown" }
            });
    }

    protected virtual void OnImpact(Vector2 hitPoint)
    {
        // ✅ 베이스에서 기본 SFX 재생 로직
        if (_impactSFX != null)
            SoundManager.Instance.PlaySFX(_impactSFX, (Vector3)hitPoint, 1f);
    }

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
                GameObject targetGoSingle = (target as Component) != null ? (target as Component).gameObject : null;
                GameCsvLogger.Instance.LogEvent(
                    eventType: GameLogEventType.DamageDealt,
                    actor: gameObject,
                    target: targetGoSingle,
                    value: _currentDamage,
                    metadata: new Dictionary<string, object>
                    {
                        { "areaType", "Single" }
                    });
                target.TakeDamage(data);
                if (_attackerTeam == TeamType.Player)
                    EventBus.Instance?.Publish(new EnemyHitEvent { AttackerTeam = _attackerTeam });
                OnImpact(hitPoint);
                Despawn();
                break;

            case AreaType.Splash:
                GameCsvLogger.Instance.LogEvent(
                    eventType: GameLogEventType.ProjectileHit,
                    actor: gameObject,
                    value: _currentDamage,
                    metadata: new Dictionary<string, object>
                    {
                        { "areaType", "Splash" }
                    });
                Explode(hitPoint);
                OnImpact(hitPoint);
                Despawn();
                break;

            case AreaType.Piercing:
                data.IsPiercing = true;
                GameObject targetGoPiercing = (target as Component) != null ? (target as Component).gameObject : null;
                GameCsvLogger.Instance.LogEvent(
                    eventType: GameLogEventType.DamageDealt,
                    actor: gameObject,
                    target: targetGoPiercing,
                    value: _currentDamage,
                    metadata: new Dictionary<string, object>
                    {
                        { "areaType", "Piercing" },
                        { "remainingPierce", _remainingPiercing }
                    });
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
                GameCsvLogger.Instance.LogEvent(
                    eventType: GameLogEventType.DamageDealt,
                    actor: gameObject,
                    target: col.gameObject,
                    value: _currentDamage,
                    metadata: new Dictionary<string, object>
                    {
                        { "areaType", "Splash" },
                        { "radius", _attackData.RangeRadius }
                    });
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
