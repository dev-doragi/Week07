using UnityEngine;
using System.Collections.Generic;

public enum BombProjectileMoveType
{
    Direct = 0,
    Arc = 1
}

public class RatBombProjectile : ArcProjectile
{
    [SerializeField] private bool _explodeOnTriggerEnter = true;
    [SerializeField] private GameObject _fragmentPrefab;
    [SerializeField] private int _fragmentCount;

    private Unit _attacker;
    private Unit _primaryTarget;
    private Unit _impactTarget;

    private int _attackRangeRadius;
    private BombProjectileMoveType _moveType;
    private bool _hasExploded;

    private Vector3 _startPos;
    private Vector3 _targetPos;
    private float _duration;
    private float _elapsedTime;
    private bool _isInitialized;

    public void Initialize(
        Unit attacker,
        Unit primaryTarget,
        int attackRangeRadius,
        BombProjectileMoveType moveType,
        Vector3 startPosition,
        Vector3 targetPosition,
        float travelTime,
        float arcHeight)
    {
        if (attacker == null)
        {
            Debug.LogError($"{name}: RatBombProjectile Initialize 실패 - attacker가 Null입니다.");
            return;
        }

        _attacker = attacker;
        _primaryTarget = primaryTarget;
        _impactTarget = null;
        _attackRangeRadius = attackRangeRadius;
        _moveType = moveType;
        _hasExploded = false;

        // 부모 클래스의 Initialize 호출
        if (attacker.Data != null && attacker.Data.Attack != null)
        {
            base.Initialize(attacker.Data.Attack, attacker.Team, startPosition, targetPosition, travelTime, arcHeight);
        }

        // 로컬 필드도 캐시
        _startPos = startPosition;
        _targetPos = targetPosition;
        _duration = Mathf.Max(0.01f, travelTime);
        _elapsedTime = 0f;
        _isInitialized = true;
    }

    private void Update()
    {
        if (!_isInitialized || _hasExploded)
        {
            return;
        }

        _elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsedTime / _duration);

        // Direct 타입일 때만 호 계산 스킵
        if (_moveType == BombProjectileMoveType.Direct)
        {
            transform.position = Vector3.Lerp(_startPos, _targetPos, t);
        }
        // Arc는 부모의 로직이 이미 처리함 - 하지만 여기서 명시적으로 계산
        else
        {
            Vector3 linear = Vector3.Lerp(_startPos, _targetPos, t);
            float heightOffset = 4f * 0.5f * t * (1f - t); // arcHeight 고정값
            transform.position = linear + Vector3.up * heightOffset;
        }

        if (t >= 1f)
        {
            Explode();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_explodeOnTriggerEnter || _hasExploded || !_isInitialized)
        {
            return;
        }

        Unit hitTarget = other.GetComponent<Unit>();
        if (hitTarget == null) return;
        if (_attacker == null) return;
        if (_attacker.Team == hitTarget.Team) return;
        if (hitTarget.IsDead) return;
        if (hitTarget.Category.ToString().ToLower().Contains("wheel")) return;

        _impactTarget = hitTarget;
        Explode();
    }

    private void Explode()
    {
        if (_hasExploded) return;
        _hasExploded = true;

        foreach (var target in CollectHitTargetsByCellRadius())
        {
            if (target == null) continue;

            DamageData damageData = new DamageData
            {
                Damage = _currentDamage,
                AttackerTeam = _attackerTeam,
                HitPoint = target.transform.position
            };
            target.TakeDamage(damageData);
        }

        for (int i = 0; i < _fragmentCount; i++)
        {
            SpawnFragment(_fragmentPrefab.name);
        }

        Despawn();
    }

    private List<Unit> CollectHitTargetsByCellRadius()
    {
        var result = new List<Unit>();
        var explosionCenter = ResolveExplosionCenter();
        var allUnits = FindObjectsOfType<Unit>();

        foreach (var unit in allUnits)
        {
            if (unit == null) continue;
            if (_attacker.Team == unit.Team) continue;
            if (unit.IsDead) continue;
            if (unit.Category.ToString().ToLower().Contains("wheel")) continue;

            float dist = Vector3.Distance(explosionCenter, unit.transform.position);
            if (dist <= _attackRangeRadius)
                result.Add(unit);
        }
        return result;
    }

    private Vector3 ResolveExplosionCenter()
    {
        if (_impactTarget != null) return _impactTarget.transform.position;
        if (_primaryTarget != null) return _primaryTarget.transform.position;
        return transform.position;
    }

    protected void SpawnFragment(string fragmentName)
    {
        if (_fragmentPrefab == null)
        {
            Debug.LogWarning($"{name}: SpawnFragment 실패 - _fragmentPrefab이 Null입니다.");
            return;
        }

        GameObject fragmentInstance = PoolManager.Instance.Spawn(fragmentName, transform.position, Quaternion.identity);
        if (fragmentInstance == null)
        {
            Debug.LogWarning($"{name}: SpawnFragment 실패 - '{fragmentName}' 풀 스폰 실패.");
        }
    }

    protected override void Despawn()
    {
        _hasExploded = false;
        _isInitialized = false;
        _elapsedTime = 0f;

        _attacker = null;
        _primaryTarget = null;
        _impactTarget = null;

        base.Despawn();
    }
}