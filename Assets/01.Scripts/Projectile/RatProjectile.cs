using UnityEngine;

public class RatProjectile : DirectProjectile
{
    [SerializeField] private GameObject _fragmentPrefab;

    private Unit _attacker;
    private Unit _primaryTarget;
    private Unit _impactTarget;
    private bool _hasHit;

    private float _elapsedTime;
    private float _duration;
    private bool _isInitialized;

    public void Initialize(
        Unit attacker,
        Unit primaryTarget,
        Vector3 startPosition,
        Vector3 targetPosition,
        float travelTime)
    {
        if (attacker == null)
        {
            Debug.LogError($"{name}: RatProjectile Initialize 실패 - attacker가 Null입니다.");
            return;
        }

        // 풀 재사용 기준으로 상태를 매번 완전히 초기화한다.
        _attacker = attacker;
        _primaryTarget = primaryTarget;
        _impactTarget = null;

        _hasHit = false;

        transform.position = startPosition;
        transform.rotation = Quaternion.identity;

        // 부모의 Launch 호출 (데미지 계산을 위해)
        if (attacker.Data != null && attacker.Data.Attack != null)
        {
            base.Launch(attacker.Data.Attack, attacker.Team);
        }

        // DirectProjectile의 Initialize 시그니처에 맞춘 호출
        float distance = Vector3.Distance(startPosition, targetPosition);
        float speed = distance / Mathf.Max(0.01f, travelTime);
        base.Initialize(attacker.Data.Attack, attacker.Team, startPosition, targetPosition, speed);
    }

    private void Update()
    {
        // DirectProjectile의 기본 이동 로직 사용
        if (!_isInitialized || _hasHit)
        {
            return;
        }

        _elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsedTime / _duration);

        // 이동 처리는 부모에서 하도록 위임, 여기서는 충돌 처리만 담당
        if (t >= 1f)
        {
            ResolveFinalHitOrDespawn();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isInitialized || _hasHit)
        {
            return;
        }

        Unit hitTarget = other.GetComponent<Unit>();
        if (hitTarget == null)
        {
            return;
        }

        if (_attacker == null)
        {
            Debug.LogError($"{name}: OnTriggerEnter2D 실패 - attacker가 Null입니다.");
            return;
        }

        if (_attacker.Team == hitTarget.Team)
        {
            return;
        }

        if (hitTarget.IsDead)
        {
            return;
        }

        // wheel은 직접 맞아도 전투 대상이 아니므로 무시한다.
        if (hitTarget.Category.ToString().ToLower().Contains("wheel"))
        {
            return;
        }

        // 목표가 아니더라도 먼저 맞은 적이 실제 피격 대상이 된다.
        _impactTarget = hitTarget;
        ApplyHitAndDespawn(_impactTarget);
    }

    private void ResolveFinalHitOrDespawn()
    {
        if (_hasHit)
        {
            return;
        }

        Unit finalTarget = _impactTarget != null ? _impactTarget : _primaryTarget;

        if (finalTarget == null)
        {
            Despawn();
            return;
        }

        if (_attacker == null)
        {
            Debug.LogError($"{name}: ResolveFinalHitOrDespawn 실패 - attacker가 Null입니다.");
            Despawn();
            return;
        }

        if (_attacker.Team == finalTarget.Team)
        {
            Despawn();
            return;
        }

        if (finalTarget.IsDead)
        {
            Despawn();
            return;
        }

        // wheel은 최종 대상이 되어도 무시한다.
        if (finalTarget.Category.ToString().ToLower().Contains("wheel"))
        {
            Despawn();
            return;
        }

        ApplyHitAndDespawn(finalTarget);
    }

    private void ApplyHitAndDespawn(Unit hitTarget)
    {
        if (_hasHit)
        {
            return;
        }

        if (_attacker == null)
        {
            Debug.LogError($"{name}: ApplyHitAndDespawn 실패 - attacker가 Null입니다.");
            Despawn();
            return;
        }

        if (hitTarget == null)
        {
            Despawn();
            return;
        }

        _hasHit = true;

        // 발사체의 현재 스탯 기준으로 공격 데미지를 계산한다.
        DamageData damageData = new DamageData
        {
            Damage = _currentDamage,
            AttackerTeam = _attackerTeam,
            HitPoint = hitTarget.transform.position
        };
        hitTarget.TakeDamage(damageData);

        // 파편 생성
        if (_fragmentPrefab != null)
        {
            SpawnFragment(_fragmentPrefab.name);
        }

        Despawn();
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
        // 풀 반환 전에 내부 상태를 확실히 정리한다.
        _isInitialized = false;
        _hasHit = false;
        _elapsedTime = 0f;

        _attacker = null;
        _primaryTarget = null;
        _impactTarget = null;

        base.Despawn();
    }
}
