using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RatHeroUnit : MonoBehaviour, IDamageable
{
    private enum RatHeroAttackType
    {
        Projectile = 0,
        LaserRaycast = 1,
        ChainLightning = 2
    }

    private enum HeroAttackMode
    {
        Single = 0,
        Splash = 1
    }

    [System.Serializable]
    private class LevelBonus
    {
        public float durationBonus;
        public float maxHpBonus;
        public float attackDamageBonus;
        public float attackRangeBonus;
        public float splashRadiusBonus;
        public float healAmountBonus;
        public float attackIntervalMultiplier = 1f;
        public float healIntervalMultiplier = 1f;
        public bool overrideAttackType;
        public RatHeroAttackType attackType = RatHeroAttackType.Projectile;
        public bool overrideAttackMode;
        public HeroAttackMode attackMode;
    }

    [Header("Base Stats")]
    [SerializeField, Min(0.1f)] private float _duration = 10f;
    [SerializeField, Min(1f)] private float _maxHp = 300f;
    [SerializeField, Range(0f, 1f)] private float _defenseRate = 0f;

    [Header("Attack")]
    [SerializeField] private UnitDataSO _heroUnitData;
    [SerializeField] private Transform _projectileSpawnPoint;
    [SerializeField, Min(0.1f)] private float _directProjectileSpeed = 15f;
    [SerializeField, Min(0.1f)] private float _arcTravelTime = 0.7f;
    [SerializeField, Min(0f)] private float _arcHeight = 3f;
    [SerializeField] private RatHeroAttackType _attackType = RatHeroAttackType.Projectile;
    [SerializeField] private HeroAttackMode _attackMode = HeroAttackMode.Single;
    [SerializeField, Min(0.1f)] private float _attackInterval = 1.25f;
    [SerializeField, Min(0.1f)] private float _attackRange = 5f;
    [SerializeField, Min(0f)] private float _attackDamage = 20f;
    [SerializeField, Min(0f)] private float _splashRadius = 1.5f;
    [SerializeField] private LayerMask _enemyLayerMask;

    [Header("Laser Attack")]
    [SerializeField, Min(0.1f)] private float _laserRange = 7f;
    [SerializeField] private bool _laserHitAllTargetsOnLine = true;
    [SerializeField, Min(0.05f)] private float _laserDuration = 1.2f;
    [SerializeField, Min(0.05f)] private float _laserTickInterval = 0.2f;
    [SerializeField, Min(0.01f)] private float _laserLineWidth = 0.2f;
    [SerializeField, Min(0.1f)] private float _laserTilePerUnit = 3f;
    [SerializeField] private Color _laserColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private Material _laserLineMaterial;
    [SerializeField] private LineRenderer _laserLineRenderer;

    [Header("Chain Lightning Attack")]
    [SerializeField, Min(1)] private int _chainMaxJumps = 3;
    [SerializeField, Min(0.1f)] private float _chainJumpRange = 3f;
    [SerializeField, Range(0.1f, 1f)] private float _chainDamageDecay = 0.75f;

    [Header("Heal")]
    [SerializeField, Min(0.1f)] private float _healInterval = 2.5f;
    [SerializeField, Min(0f)] private float _healAmount = 12f;
    [SerializeField] private bool _healSelf = true;

    [Header("Upgrade Level")]
    [SerializeField] private LevelBonus[] _levelBonuses = new LevelBonus[2];

    [Header("Layer Setup")]
    [SerializeField] private bool _setLayerToAllyOnInitialize = true;
    [SerializeField] private string _allyLayerName = "Ally";

    private float _currentHp;
    private float _remainingDuration;
    private float _currentAttackInterval;
    private float _currentAttackRange;
    private float _currentAttackDamage;
    private float _currentSplashRadius;
    private float _currentHealInterval;
    private float _currentHealAmount;
    private RatHeroAttackType _currentAttackType;
    private HeroAttackMode _currentAttackMode;
    private AttackModule _baseAttackData;
    private float _attackTimer;
    private float _healTimer;
    private bool _isDead;
    private Coroutine _laserRoutine;
    private bool _isLaserFiring;
    private Dictionary<RatHeroAttackType, IHeroAttackExecutor> _attackExecutors;

    public TeamType Team => TeamType.Player;
    public UnitCategory Category => UnitCategory.Defense;
    public bool IsDead => _isDead;

    public void Initialize(int upgradeLevel)
    {
        EnsureAttackExecutors();
        ResolveAttackDataFromUnitData();
        ConfigureStatsByLevel(upgradeLevel);

        _currentHp = _maxHp;
        _isDead = false;
        _attackTimer = _currentAttackInterval;
        _healTimer = _currentHealInterval;

        if (_setLayerToAllyOnInitialize)
        {
            int allyLayer = LayerMask.NameToLayer(_allyLayerName);
            if (allyLayer >= 0)
            {
                SetLayerRecursively(transform, allyLayer);
            }
        }

        Debug.Log($"[RatHero] 소환 | 단계: {upgradeLevel} | 지속: {_remainingDuration:0.##}초 | HP: {_currentHp:0.##}");
    }

    private void Update()
    {
        if (_isDead)
            return;

        _remainingDuration -= Time.deltaTime;
        if (_remainingDuration <= 0f)
        {
            Expire("지속시간 종료");
            return;
        }

        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f)
        {
            PerformAttack();
            _attackTimer = _currentAttackInterval;
        }

        _healTimer -= Time.deltaTime;
        if (_healTimer <= 0f)
        {
            PerformHeal();
            _healTimer = _currentHealInterval;
        }
    }

    public void TakeDamage(DamageData damageData)
    {
        if (_isDead)
            return;

        float finalDamage = damageData.Damage * (1f - Mathf.Clamp01(_defenseRate));
        _currentHp -= Mathf.Max(0f, finalDamage);
        Debug.Log($"[RatHero] 피격 | 피해: {finalDamage:0.##} | 남은 HP: {Mathf.Max(0f, _currentHp):0.##}");

        if (_currentHp <= 0f)
        {
            Expire("전투 사망");
        }
    }

    private void PerformAttack()
    {
        IDamageable primaryTarget = FindPrimaryEnemyTarget();
        if (primaryTarget == null)
            return;

        if (!_attackExecutors.TryGetValue(_currentAttackType, out IHeroAttackExecutor executor) || executor == null)
        {
            Debug.LogWarning($"[RatHero] 공격 실패 | 실행기 없음 | 타입: {_currentAttackType}");
            return;
        }

        if (!executor.Execute(this, primaryTarget))
        {
            Debug.LogWarning($"[RatHero] 공격 실패 | 실행기 수행 실패 | 타입: {_currentAttackType}");
            return;
        }

        Debug.Log($"[RatHero] 공격 | 타입: {_currentAttackType} | 방식: {_currentAttackMode} | 피해: {_currentAttackDamage:0.##}");
    }

    private void PerformHeal()
    {
        List<Unit> allies = GridManager.Instance != null
            ? GridManager.Instance.GetAllLivingUnits()
            : FindAllLivingPlayerUnits();

        int healedCount = 0;
        for (int i = 0; i < allies.Count; i++)
        {
            Unit ally = allies[i];
            if (ally == null || ally.IsDead || ally.Team != TeamType.Player)
                continue;

            ally.Heal(_currentHealAmount);
            healedCount++;
        }

        if (_healSelf && !_isDead)
        {
            _currentHp = Mathf.Min(_currentHp + _currentHealAmount, _maxHp);
        }

        Debug.Log($"[RatHero] 아군 회복 | 회복량: {_currentHealAmount:0.##} | 회복 대상 수: {healedCount}");
    }

    private void Expire(string reason)
    {
        if (_isDead)
            return;

        _isDead = true;
        StopLaserRoutine();
        Debug.Log($"[RatHero] 소멸 | 사유: {reason}");
        Destroy(gameObject);
    }

    private void ConfigureStatsByLevel(int level)
    {
        int clampedLevel = Mathf.Clamp(level, 0, 2);
        _remainingDuration = _duration;
        _maxHp = Mathf.Max(1f, _maxHp);
        _currentAttackInterval = Mathf.Max(0.05f, _attackInterval);
        _currentAttackRange = Mathf.Max(0.1f, _attackRange);
        _currentAttackDamage = Mathf.Max(0f, _attackDamage);
        _currentSplashRadius = Mathf.Max(0f, _splashRadius);
        _currentHealInterval = Mathf.Max(0.05f, _healInterval);
        _currentHealAmount = Mathf.Max(0f, _healAmount);
        _currentAttackType = _attackType;
        _currentAttackMode = _attackMode;

        for (int i = 0; i < clampedLevel; i++)
        {
            if (_levelBonuses == null || i >= _levelBonuses.Length || _levelBonuses[i] == null)
                continue;

            LevelBonus bonus = _levelBonuses[i];
            _remainingDuration = Mathf.Max(0.1f, _remainingDuration + bonus.durationBonus);
            _maxHp = Mathf.Max(1f, _maxHp + bonus.maxHpBonus);
            _currentAttackDamage = Mathf.Max(0f, _currentAttackDamage + bonus.attackDamageBonus);
            _currentAttackRange = Mathf.Max(0.1f, _currentAttackRange + bonus.attackRangeBonus);
            _currentSplashRadius = Mathf.Max(0f, _currentSplashRadius + bonus.splashRadiusBonus);
            _currentHealAmount = Mathf.Max(0f, _currentHealAmount + bonus.healAmountBonus);

            if (bonus.attackIntervalMultiplier > 0f)
                _currentAttackInterval = Mathf.Max(0.05f, _currentAttackInterval * bonus.attackIntervalMultiplier);

            if (bonus.healIntervalMultiplier > 0f)
                _currentHealInterval = Mathf.Max(0.05f, _currentHealInterval * bonus.healIntervalMultiplier);

            if (bonus.overrideAttackType)
                _currentAttackType = bonus.attackType;

            if (bonus.overrideAttackMode)
                _currentAttackMode = bonus.attackMode;
        }
    }

    private IDamageable FindPrimaryEnemyTarget()
    {
        Collider2D[] hits = OverlapEnemy(transform.position, _currentAttackRange);
        IDamageable closest = null;
        float closestDistSq = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            IDamageable target = hits[i].GetComponentInParent<IDamageable>();
            if (!CanAttackTarget(target))
                continue;

            Component targetComponent = target as Component;
            if (targetComponent == null)
                continue;

            float distSq = (targetComponent.transform.position - transform.position).sqrMagnitude;
            if (distSq < closestDistSq)
            {
                closestDistSq = distSq;
                closest = target;
            }
        }

        return closest;
    }

    private Collider2D[] OverlapEnemy(Vector2 center, float radius)
    {
        if (_enemyLayerMask.value == 0)
            return Physics2D.OverlapCircleAll(center, radius);

        return Physics2D.OverlapCircleAll(center, radius, _enemyLayerMask);
    }

    private bool CanAttackTarget(IDamageable target)
    {
        if (target == null || target.IsDead)
            return false;

        if (target.Team != TeamType.Enemy)
            return false;

        return target.Category != UnitCategory.Wheel;
    }

    private void ResolveAttackDataFromUnitData()
    {
        AttackModule data = _heroUnitData != null ? _heroUnitData.Attack : null;
        if (data == null)
        {
            _baseAttackData = null;
            Debug.LogWarning("[RatHero] UnitDataSO AttackModule이 없어 투사체 공격을 수행할 수 없습니다.");
            return;
        }

        _baseAttackData = new AttackModule
        {
            Damage = data.Damage,
            Speed = data.Speed,
            Distance = data.Distance,
            AttackCost = 0,
            Trajectory = data.Trajectory,
            Targeting = data.Targeting,
            Area = data.Area,
            RangeRadius = data.RangeRadius,
            PiercingCount = data.PiercingCount,
            Penetration = data.Penetration,
            PiercingDecay = data.PiercingDecay,
            ProjectilePrefab = data.ProjectilePrefab
        };
    }

    private bool TryFireProjectile(IDamageable target)
    {
        if (_baseAttackData == null)
            return false;

        if (_baseAttackData.ProjectilePrefab == null)
        {
            Debug.LogWarning("[RatHero] 전용 투사체 프리팹이 지정되지 않았습니다.");
            return false;
        }

        Component targetComponent = target as Component;
        if (targetComponent == null)
            return false;

        Vector3 startPos = _projectileSpawnPoint != null ? _projectileSpawnPoint.position : transform.position;
        Vector3 targetPos = targetComponent.transform.position;
        GameObject projectile = PoolManager.Instance != null
            ? PoolManager.Instance.Spawn(_baseAttackData.ProjectilePrefab.name, startPos, Quaternion.identity)
            : null;

        if (projectile == null)
        {
            Debug.LogWarning($"[RatHero] 투사체 스폰 실패: {_baseAttackData.ProjectilePrefab.name}");
            return false;
        }

        AttackModule attackForShot = BuildAttackDataForShot();
        if (attackForShot.Trajectory == AttackTrajectoryType.Arc)
        {
            if (projectile.TryGetComponent(out RatArcProjectile ratArc))
            {
                ratArc.Initialize(attackForShot, TeamType.Player, startPos, targetPos, _arcTravelTime, _arcHeight, gameObject);
                return true;
            }

            if (projectile.TryGetComponent(out ArcProjectile arc))
            {
                arc.Initialize(attackForShot, TeamType.Player, startPos, targetPos, _arcTravelTime, _arcHeight, gameObject);
                return true;
            }
        }
        else
        {
            if (projectile.TryGetComponent(out RatProjectile ratDirect))
            {
                ratDirect.Initialize(attackForShot, TeamType.Player, startPos, targetPos, _directProjectileSpeed, gameObject);
                return true;
            }

            if (projectile.TryGetComponent(out DirectProjectile direct))
            {
                direct.Initialize(attackForShot, TeamType.Player, startPos, targetPos, _directProjectileSpeed, gameObject);
                return true;
            }
        }

        Debug.LogWarning($"[RatHero] 투사체 타입 초기화 실패: {projectile.name}");
        if (PoolManager.Instance != null)
            PoolManager.Instance.Despawn(projectile);
        else
            Destroy(projectile);
        return false;
    }

    private bool TryLaserAttack(IDamageable target)
    {
        if (_isLaserFiring)
            return true;

        Component targetComponent = target as Component;
        if (targetComponent == null)
            return false;

        EnsureLaserLineRenderer();
        if (_laserLineRenderer == null)
            return false;

        _laserRoutine = StartCoroutine(LaserBeamRoutine(targetComponent));
        return true;
    }

    private bool TryChainLightningAttack(IDamageable primaryTarget)
    {
        Component primaryComponent = primaryTarget as Component;
        if (primaryComponent == null)
            return false;

        var hitSet = new HashSet<IDamageable>();
        IDamageable current = primaryTarget;
        Vector3 currentPos = primaryComponent.transform.position;
        float damage = _currentAttackDamage;
        int totalHits = 0;

        for (int jump = 0; jump < Mathf.Max(1, _chainMaxJumps); jump++)
        {
            if (current == null || !CanAttackTarget(current))
                break;

            current.TakeDamage(new DamageData
            {
                Damage = damage,
                AttackerTeam = TeamType.Player,
                HitPoint = currentPos
            });
            hitSet.Add(current);
            totalHits++;

            IDamageable next = FindNextChainTarget(currentPos, hitSet);
            if (next == null)
                break;

            Component nextComp = next as Component;
            if (nextComp == null)
                break;

            current = next;
            currentPos = nextComp.transform.position;
            damage *= Mathf.Clamp(_chainDamageDecay, 0.1f, 1f);
        }

        return totalHits > 0;
    }

    private IDamageable FindNextChainTarget(Vector3 fromPosition, HashSet<IDamageable> excluded)
    {
        Collider2D[] hits = OverlapEnemy(fromPosition, Mathf.Max(0.1f, _chainJumpRange));
        IDamageable closest = null;
        float closestDistSq = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            IDamageable target = hits[i].GetComponentInParent<IDamageable>();
            if (!CanAttackTarget(target))
                continue;

            if (excluded.Contains(target))
                continue;

            Component targetComp = target as Component;
            if (targetComp == null)
                continue;

            float distSq = (targetComp.transform.position - fromPosition).sqrMagnitude;
            if (distSq < closestDistSq)
            {
                closestDistSq = distSq;
                closest = target;
            }
        }

        return closest;
    }

    private AttackModule BuildAttackDataForShot()
    {
        AttackModule source = _baseAttackData;
        AreaType area = _currentAttackMode == HeroAttackMode.Splash ? AreaType.Splash : AreaType.Single;
        float rangeRadius = _currentAttackMode == HeroAttackMode.Splash ? Mathf.Max(0.1f, _currentSplashRadius) : source.RangeRadius;

        return new AttackModule
        {
            Damage = _currentAttackDamage,
            Speed = source.Speed,
            Distance = _currentAttackRange,
            AttackCost = 0,
            Trajectory = source.Trajectory,
            Targeting = source.Targeting,
            Area = area,
            RangeRadius = rangeRadius,
            PiercingCount = source.PiercingCount,
            Penetration = source.Penetration,
            PiercingDecay = source.PiercingDecay,
            ProjectilePrefab = source.ProjectilePrefab
        };
    }

    private static List<Unit> FindAllLivingPlayerUnits()
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        var result = new List<Unit>();
        for (int i = 0; i < allUnits.Length; i++)
        {
            Unit unit = allUnits[i];
            if (unit != null && !unit.IsDead && unit.Team == TeamType.Player)
                result.Add(unit);
        }

        return result;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
            return;

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    private void EnsureLaserLineRenderer()
    {
        if (_laserLineRenderer == null)
            _laserLineRenderer = GetComponentInChildren<LineRenderer>(true);

        if (_laserLineRenderer == null)
        {
            GameObject laserObject = new GameObject("LaserLineRenderer");
            laserObject.transform.SetParent(transform, false);
            _laserLineRenderer = laserObject.AddComponent<LineRenderer>();
        }

        _laserLineRenderer.enabled = false;
        _laserLineRenderer.useWorldSpace = true;
        _laserLineRenderer.positionCount = 2;
        _laserLineRenderer.startWidth = _laserLineWidth;
        _laserLineRenderer.endWidth = _laserLineWidth;
        _laserLineRenderer.numCapVertices = 0;
        _laserLineRenderer.numCornerVertices = 0;
        _laserLineRenderer.textureMode = LineTextureMode.Tile;
        _laserLineRenderer.alignment = LineAlignment.View;
        _laserLineRenderer.startColor = _laserColor;
        _laserLineRenderer.endColor = _laserColor;
        _laserLineRenderer.sortingOrder = 20;

        if (_laserLineMaterial != null)
            _laserLineRenderer.material = _laserLineMaterial;
        else if (_laserLineRenderer.material == null)
            _laserLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    private IEnumerator LaserBeamRoutine(Component primaryTarget)
    {
        _isLaserFiring = true;
        float elapsed = 0f;
        float tickTimer = 0f;
        float duration = Mathf.Max(0.05f, _laserDuration);

        _laserLineRenderer.enabled = true;

        while (!_isDead && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            tickTimer -= Time.deltaTime;

            Vector3 startPos = _projectileSpawnPoint != null ? _projectileSpawnPoint.position : transform.position;
            Vector3 fallbackTargetPos = primaryTarget != null ? primaryTarget.transform.position : (startPos + transform.right * _laserRange);
            Vector3 targetPos = ResolveLaserHitPoint(startPos, fallbackTargetPos, out List<IDamageable> hitTargets);

            UpdateLaserLineVisual(startPos, targetPos);

            if (tickTimer <= 0f)
            {
                tickTimer = Mathf.Max(0.05f, _laserTickInterval);
                ApplyLaserTickDamage(hitTargets, targetPos);
            }

            yield return null;
        }

        _laserLineRenderer.enabled = false;
        _isLaserFiring = false;
        _laserRoutine = null;
    }

    private Vector3 ResolveLaserHitPoint(Vector3 startPos, Vector3 targetPosHint, out List<IDamageable> hitTargets)
    {
        hitTargets = new List<IDamageable>();

        Vector2 direction = (targetPosHint - startPos).normalized;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.right;

        float range = Mathf.Max(0.1f, _laserRange > 0f ? _laserRange : _currentAttackRange);
        RaycastHit2D[] hits = _enemyLayerMask.value == 0
            ? Physics2D.RaycastAll(startPos, direction, range)
            : Physics2D.RaycastAll(startPos, direction, range, _enemyLayerMask);

        float closestDistance = range;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            IDamageable hitTarget = hit.collider != null ? hit.collider.GetComponentInParent<IDamageable>() : null;
            if (!CanAttackTarget(hitTarget))
                continue;

            hitTargets.Add(hitTarget);
            if (hit.distance < closestDistance)
                closestDistance = hit.distance;

            if (!_laserHitAllTargetsOnLine)
                break;
        }

        return startPos + (Vector3)(direction * closestDistance);
    }

    private void UpdateLaserLineVisual(Vector3 startPos, Vector3 endPos)
    {
        if (_laserLineRenderer == null)
            return;

        _laserLineRenderer.SetPosition(0, startPos);
        _laserLineRenderer.SetPosition(1, endPos);

        Material mat = _laserLineRenderer.material;
        if (mat != null)
        {
            float length = Vector3.Distance(startPos, endPos);
            float tileX = Mathf.Max(1f, length * Mathf.Max(0.1f, _laserTilePerUnit));
            mat.SetTextureScale("_MainTex", new Vector2(tileX, 1f));
        }
    }

    private void ApplyLaserTickDamage(List<IDamageable> targets, Vector2 hitPoint)
    {
        if (targets == null || targets.Count == 0)
            return;

        for (int i = 0; i < targets.Count; i++)
        {
            IDamageable target = targets[i];
            if (!CanAttackTarget(target))
                continue;

            target.TakeDamage(new DamageData
            {
                Damage = _currentAttackDamage,
                AttackerTeam = TeamType.Player,
                HitPoint = hitPoint
            });
        }
    }

    private void StopLaserRoutine()
    {
        if (_laserRoutine != null)
        {
            StopCoroutine(_laserRoutine);
            _laserRoutine = null;
        }

        _isLaserFiring = false;
        if (_laserLineRenderer != null)
            _laserLineRenderer.enabled = false;
    }

    private void OnDisable()
    {
        StopLaserRoutine();
    }

    private void EnsureAttackExecutors()
    {
        if (_attackExecutors != null)
            return;

        _attackExecutors = new Dictionary<RatHeroAttackType, IHeroAttackExecutor>
        {
            { RatHeroAttackType.Projectile, new ProjectileAttackExecutor() },
            { RatHeroAttackType.LaserRaycast, new LaserRaycastAttackExecutor() },
            { RatHeroAttackType.ChainLightning, new ChainLightningAttackExecutor() }
        };
    }

    private interface IHeroAttackExecutor
    {
        bool Execute(RatHeroUnit owner, IDamageable primaryTarget);
    }

    private class ProjectileAttackExecutor : IHeroAttackExecutor
    {
        public bool Execute(RatHeroUnit owner, IDamageable primaryTarget)
        {
            return owner.TryFireProjectile(primaryTarget);
        }
    }

    private class LaserRaycastAttackExecutor : IHeroAttackExecutor
    {
        public bool Execute(RatHeroUnit owner, IDamageable primaryTarget)
        {
            return owner.TryLaserAttack(primaryTarget);
        }
    }

    private class ChainLightningAttackExecutor : IHeroAttackExecutor
    {
        public bool Execute(RatHeroUnit owner, IDamageable primaryTarget)
        {
            return owner.TryChainLightningAttack(primaryTarget);
        }
    }
}
