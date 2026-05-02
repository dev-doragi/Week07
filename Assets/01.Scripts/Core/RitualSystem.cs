using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 플레이어가 사용하는 의식 스킬 3종을 관리합니다.
/// 버튼 OnClick → UseSkill1/2/3 연결.
/// </summary>
public class RitualSystem : MonoBehaviour
{
    [System.Serializable]
    private class IntUnityEvent : UnityEvent<int> { }

    [System.Serializable]
    private class IntIntUnityEvent : UnityEvent<int, int> { }

    [Header("Doctrine Unlock")]
    [SerializeField] private bool _useDoctrineUnlockChecks = true;
    [SerializeField] private bool _unlockAllRitualSkillsAtStart = true;
    [SerializeField] private UnlockManager _unlockManager;

    [Header("Skill Costs")]
    [SerializeField] private int _skill1Cost = 30;
    [SerializeField] private int _skill2Cost = 50;
    [SerializeField] private int _skill3Cost = 80;

    [Header("Skill Cooldowns (seconds)")]
    [SerializeField] private float _skill1Cooldown = 5f;
    [SerializeField] private float _skill2Cooldown = 5f;
    [SerializeField] private float _skill3Cooldown = 5f;

    [Header("Skill 1 - Wall")]
    [Tooltip("물리적으로 켜고 끌 벽 오브젝트. 아군 투사체는 통과, 적 투사체는 차단.")]
    [SerializeField] private GameObject _wallObject;
    [SerializeField] private float _skill1Duration = 5f;
    [SerializeField] private float[] _skill1DurationBonusByLevel = new float[2];
    [SerializeField, Range(0f, 1f)] private float _skill1HealPercentByLevel1 = 0.3f;
    [SerializeField, Min(0.1f)] private float _skill1CounterProjectileDistance = 12f;
    [SerializeField, Min(0f)] private float _skill1CounterProjectileSpeed = 16f;
    [SerializeField, Min(0.01f)] private float _skill1CounterProjectileArcTravelTime = 0.8f;
    [SerializeField, Min(0f)] private float _skill1CounterProjectileArcHeight = 2.5f;
    [SerializeField, Min(0f)] private float _skill1CounterFanAngle = 70f;
    [SerializeField, Min(0)] private int _skill1CounterMaxShotCount = 0;
    [SerializeField] private string _skill1CounterProjectilePoolKey = "RatProjectile";
    [SerializeField] private AttackTrajectoryType _skill1CounterProjectileTrajectory = AttackTrajectoryType.Direct;
    [SerializeField] private AreaType _skill1CounterProjectileArea = AreaType.Single;
    [SerializeField, Min(0f)] private float _skill1CounterProjectileDamage = 20f;
    [SerializeField, Min(0f)] private float _skill1CounterProjectileSplashRadius = 1.5f;
    [SerializeField, Min(0)] private int _skill1CounterProjectilePiercingCount = 0;
    [SerializeField, Range(0f, 1f)] private float _skill1CounterProjectilePiercingDecay = 1f;
    [SerializeField, Range(0f, 1f)] private float _skill1CounterProjectilePenetration = 0f;

    [Header("Skill 2 - Meteor")]
    [SerializeField] private string _meteorPoolKey  = "MeteorProjectile";
    [SerializeField] private int    _meteorCount    = 5;
    [SerializeField] private float  _meteorDamage   = 80f;
    [SerializeField] private float  _splashRadius   = 2f;
    [SerializeField] private float  _spawnHeight    = 15f;
    [SerializeField] private float  _targetRadius   = 3f;
    [SerializeField] private float  _minDuration    = 0.8f;
    [SerializeField] private float  _maxDuration    = 1.4f;
    [SerializeField] private float  _meteorDelay    = 0.15f;
    [SerializeField] private int[]   _meteorCountBonusByLevel = new int[2];
    [SerializeField] private float[] _meteorDamageMultiplierByLevel = new float[2];
    [SerializeField] private float[] _meteorSplashRadiusBonusByLevel = new float[2];
    [SerializeField] private float[] _meteorTargetRadiusBonusByLevel = new float[2];

    [Header("RatHero Notify (Inspector Optional)")]
    [SerializeField] private IntUnityEvent _onSkill1CastToRatHero;
    [SerializeField] private IntUnityEvent _onSkill2CastToRatHero;
    [SerializeField] private IntUnityEvent _onSkill3CastToRatHero;
    [SerializeField] private IntIntUnityEvent _onRitualSkillLevelChanged;

    [Header("Cooldown Gauges")]
    [SerializeField] private Image _skill1CooldownGauge;
    [SerializeField] private Image _skill2CooldownGauge;
    [SerializeField] private Image _skill3CooldownGauge;

    [Header("Cutin UI")]
    [SerializeField] private RitualCutinUI _ritualCutinUI;

    private Coroutine _wallRoutine;
    private RitualWallHitCounter _wallHitCounter;
    private Unit _playerCore;
    private Unit _enemyCore;

    private float _skill1CooldownTimer = 0f;
    private float _skill2CooldownTimer = 0f;
    private float _skill3CooldownTimer = 0f;

    private const int MaxSkillLevel = 2;
    private readonly Dictionary<int, int> _skillLevels = new Dictionary<int, int>
    {
        { 1, 0 },
        { 2, 0 },
        { 3, 0 }
    };

    private const string RitualSkill2UnlockId = "Ritual_Node_0";
    private const string RitualSkill3UnlockId = "Ritual_Node_4";

    public float Skill1CooldownRemaining => _skill1CooldownTimer;
    public float Skill2CooldownRemaining => _skill2CooldownTimer;
    public float Skill3CooldownRemaining => _skill3CooldownTimer;
    public bool IsSkillUnlocked(int skillIndex)
    {
        return skillIndex switch
        {
            1 => true,
            2 => !_useDoctrineUnlockChecks || IsDoctrineUnlockedForUI(RitualSkill2UnlockId),
            3 => !_useDoctrineUnlockChecks || IsDoctrineUnlockedForUI(RitualSkill3UnlockId),
            _ => true
        };
    }

    public float GetSkillCooldownDuration(int skillIndex)
    {
        return skillIndex switch
        {
            1 => _skill1Cooldown,
            2 => _skill2Cooldown,
            3 => _skill3Cooldown,
            _ => 0f
        };
    }

    public float GetSkillCooldownRemaining(int skillIndex)
    {
        return skillIndex switch
        {
            1 => Mathf.Max(0f, _skill1CooldownTimer),
            2 => Mathf.Max(0f, _skill2CooldownTimer),
            3 => Mathf.Max(0f, _skill3CooldownTimer),
            _ => 0f
        };
    }

    private void Update()
    {
        if (_skill1CooldownTimer > 0f) _skill1CooldownTimer -= Time.deltaTime;
        if (_skill2CooldownTimer > 0f) _skill2CooldownTimer -= Time.deltaTime;
        if (_skill3CooldownTimer > 0f) _skill3CooldownTimer -= Time.deltaTime;

        UpdateCooldownGauges();
    }

    // ──────────────────────────────────────────────
    // 외부(버튼) 진입점
    // ──────────────────────────────────────────────

    public void UseSkill1()
    {
        if (!IsReady(1, _skill1CooldownTimer)) return;
        if (!TryConsumeResource(_skill1Cost)) return;
        _skill1CooldownTimer = _skill1Cooldown;
        Debug.Log($"[RitualSystem] 스킬 사용 성공 | Skill1 Wall | 단계: {GetSkillLevel(1)}");
        _ritualCutinUI?.Play(1);
        GameCsvLogger.Instance.LogEvent(GameLogEventType.SkillUsed, actor: gameObject, value: _skill1Cost, metadata: new System.Collections.Generic.Dictionary<string, object> { { "skillIndex", 1 }, { "skillName", "Wall" }, { "kind", "Ritual" } });
        GameCsvLogger.Instance.LogEvent(GameLogEventType.RitualUsed, actor: gameObject, value: _skill1Cost, metadata: new System.Collections.Generic.Dictionary<string, object> { { "skillIndex", 1 }, { "skillName", "Wall" } });
        ActivateWall();
        NotifyRatHeroSkillCast(1);
        EventBus.Instance?.Publish(new TutorialSkillUsedEvent { SkillIndex = 1 });
        GameLogger.Instance?.RecordRitualSkillUsed(1, "Wall");
    }

    public void UseSkill2()
    {
        if (!IsDoctrineUnlocked(2, RitualSkill2UnlockId)) return;
        if (!IsReady(2, _skill2CooldownTimer)) return;
        if (!CanCastSkill2()) return;
        if (!TryConsumeResource(_skill2Cost)) return;
        _skill2CooldownTimer = _skill2Cooldown;
        Debug.Log($"[RitualSystem] 스킬 사용 성공 | Skill2 Meteor | 단계: {GetSkillLevel(2)}");
        _ritualCutinUI?.Play(2);
        GameCsvLogger.Instance.LogEvent(GameLogEventType.SkillUsed, actor: gameObject, value: _skill2Cost, metadata: new System.Collections.Generic.Dictionary<string, object> { { "skillIndex", 2 }, { "skillName", "Meteor" }, { "kind", "Ritual" } });
        GameCsvLogger.Instance.LogEvent(GameLogEventType.RitualUsed, actor: gameObject, value: _skill2Cost, metadata: new System.Collections.Generic.Dictionary<string, object> { { "skillIndex", 2 }, { "skillName", "Meteor" } });
        OnSkill2();
        NotifyRatHeroSkillCast(2);
        EventBus.Instance?.Publish(new TutorialSkillUsedEvent { SkillIndex = 2 });
        GameLogger.Instance?.RecordRitualSkillUsed(2, "Meteor");
    }

    public void UseSkill3()
    {
        if (!IsDoctrineUnlocked(3, RitualSkill3UnlockId)) return;
        if (!IsReady(3, _skill3CooldownTimer)) return;
        if (!CanCastSkill3()) return;
        if (!TryConsumeResource(_skill3Cost)) return;
        _skill3CooldownTimer = _skill3Cooldown;
        Debug.Log($"[RitualSystem] 스킬 사용 성공 | Skill3 RatHero | 단계: {GetSkillLevel(3)}");
        _ritualCutinUI?.Play(3);
        GameCsvLogger.Instance.LogEvent(GameLogEventType.SkillUsed, actor: gameObject, value: _skill3Cost, metadata: new System.Collections.Generic.Dictionary<string, object> { { "skillIndex", 3 }, { "skillName", "RatHero" }, { "kind", "Ritual" } });
        GameCsvLogger.Instance.LogEvent(GameLogEventType.RitualUsed, actor: gameObject, value: _skill3Cost, metadata: new System.Collections.Generic.Dictionary<string, object> { { "skillIndex", 3 }, { "skillName", "RatHero" } });
        OnSkill3();
        EventBus.Instance?.Publish(new TutorialSkillUsedEvent { SkillIndex = 3 });
        GameLogger.Instance?.RecordRitualSkillUsed(3, "RatHero");
    }

    // ──────────────────────────────────────────────
    // 스킬 구현
    // ──────────────────────────────────────────────

    /// <summary>
    /// 스킬 1: 벽 토글.
    /// 벽은 적 투사체 레이어와만 충돌하도록
    /// Physics 2D 충돌 매트릭스에서 설정하세요.
    /// (Wall 레이어 ↔ EnemyProjectile 충돌 ON, AllyProjectile 충돌 OFF)
    /// </summary>

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance.Subscribe<WaveEndedEvent>(OnWaveEnded);
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance.Unsubscribe<WaveEndedEvent>(OnWaveEnded);
    }

    private void OnWaveStarted(WaveStartedEvent evt)
    {
        _playerCore = null;
        _enemyCore = null;

        foreach(var u in FindObjectsByType<Unit>(FindObjectsSortMode.None))
        {
            if(u.Team == TeamType.Player && u.Category == UnitCategory.Core) _playerCore = u;
            if(u.Team == TeamType.Enemy && u.Category == UnitCategory.Core) _enemyCore = u;
            if(_playerCore != null && _enemyCore != null) break;
        }
    }

    private void OnWaveEnded(WaveEndedEvent evt)
    {
        _playerCore = null;
        _enemyCore = null;
    }

    private void ActivateWall()
    {
        if (_wallObject == null)
        {
            Debug.LogWarning("[RitualSystem] 벽 오브젝트가 연결되지 않았습니다.");
            return;
        }

        EnsureWallHitCounter();

        if (_wallRoutine != null) StopCoroutine(_wallRoutine);
        _wallRoutine = StartCoroutine(WallRoutine());
    }

    private IEnumerator WallRoutine()
    {
        _wallObject.SetActive(true);
        int level = GetSkillLevel(1);
        float duration = Mathf.Max(0.1f, _skill1Duration + GetLevelBonus(_skill1DurationBonusByLevel, level));
        bool useCounterAttack = level >= 2;

        if (level >= 1)
        {
            HealAllPlayerUnitsByPercent(_skill1HealPercentByLevel1);
            Debug.Log($"[RitualSystem] 스킬 1 - 아군 회복 적용 ({_skill1HealPercentByLevel1 * 100f:0.#}%, 단계: {level})");
        }

        if (useCounterAttack && _wallHitCounter != null)
        {
            _wallHitCounter.BeginRecord();
        }

        Debug.Log($"[RitualSystem] 스킬 1 - 벽 활성화 ({duration}초, 단계: {level})");

        yield return new WaitForSeconds(duration);

        _wallObject.SetActive(false);
        _wallRoutine = null;

        if (useCounterAttack)
        {
            int blockedHitCount = _wallHitCounter != null ? _wallHitCounter.EndRecord() : 0;
            FireSkill1CounterFan(blockedHitCount);
            Debug.Log($"[RitualSystem] 스킬 1 - 반격 발동 | 피격 수: {blockedHitCount}");
        }

        Debug.Log("[RitualSystem] 스킬 1 - 벽 비활성화");
    }

    private void OnSkill2()
    {
        StartCoroutine(MeteorRoutine());
    }

    private void OnSkill3()
    {
        NotifyRatHeroSkillCast(3);
    }

    private void EnsureWallHitCounter()
    {
        if (_wallObject == null)
            return;

        if (_wallHitCounter == null)
            _wallHitCounter = _wallObject.GetComponent<RitualWallHitCounter>();

        if (_wallHitCounter == null)
            _wallHitCounter = _wallObject.AddComponent<RitualWallHitCounter>();
    }

    private void HealAllPlayerUnitsByPercent(float percent)
    {
        if (percent <= 0f)
            return;

        List<Unit> units = GridManager.Instance != null
            ? GridManager.Instance.GetAllLivingUnits()
            : new List<Unit>(FindObjectsByType<Unit>(FindObjectsSortMode.None));

        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            if (unit == null || unit.IsDead || unit.Team != TeamType.Player || unit.Data == null)
                continue;

            unit.Heal(unit.Data.MaxHp * percent);
        }
    }

    private void FireSkill1CounterFan(int blockedHitCount)
    {
        if (blockedHitCount <= 0)
            return;

        if (PoolManager.Instance == null)
        {
            Debug.LogWarning("[RitualSystem] 스킬 1 반격 실패: PoolManager 없음");
            return;
        }

        if (string.IsNullOrWhiteSpace(_skill1CounterProjectilePoolKey))
        {
            Debug.LogWarning("[RitualSystem] 스킬 1 반격 실패: 반격 투사체 키 미지정");
            return;
        }

        int shotCount = blockedHitCount;
        if (_skill1CounterMaxShotCount > 0)
            shotCount = Mathf.Min(shotCount, _skill1CounterMaxShotCount);

        if (shotCount <= 0)
            return;

        Vector3 spawnPos = _wallObject != null ? _wallObject.transform.position : transform.position;
        Vector2 baseDirection = ResolveCounterBaseDirection(spawnPos);
        AttackModule counterAttackData = BuildSkill1CounterAttackData();
        float halfFanAngle = Mathf.Max(0f, _skill1CounterFanAngle) * 0.5f;
        float distance = Mathf.Max(0.1f, _skill1CounterProjectileDistance);

        for (int i = 0; i < shotCount; i++)
        {
            float t = shotCount <= 1 ? 0.5f : (float)i / (shotCount - 1);
            float angle = Mathf.Lerp(-halfFanAngle, halfFanAngle, t);
            Vector2 direction = (Quaternion.Euler(0f, 0f, angle) * baseDirection).normalized;
            Vector3 targetPos = spawnPos + (Vector3)(direction * distance);

            GameObject projectileObject = PoolManager.Instance.Spawn(_skill1CounterProjectilePoolKey, spawnPos, Quaternion.identity);
            if (projectileObject == null)
                continue;

            if (!TryInitializeCounterProjectile(projectileObject, counterAttackData, spawnPos, targetPos))
            {
                PoolManager.Instance.Despawn(projectileObject);
            }
        }
    }

    private Vector2 ResolveCounterBaseDirection(Vector3 spawnPos)
    {
        if (_enemyCore != null && !_enemyCore.IsDead)
        {
            Vector2 toEnemyCore = _enemyCore.transform.position - spawnPos;
            if (toEnemyCore.sqrMagnitude > 0.0001f)
                return toEnemyCore.normalized;
        }

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        Unit nearestEnemy = null;
        float nearestDistSq = float.MaxValue;

        for (int i = 0; i < allUnits.Length; i++)
        {
            Unit unit = allUnits[i];
            if (unit == null || unit.IsDead || unit.Team != TeamType.Enemy)
                continue;

            float distSq = (unit.transform.position - spawnPos).sqrMagnitude;
            if (distSq < nearestDistSq)
            {
                nearestDistSq = distSq;
                nearestEnemy = unit;
            }
        }

        if (nearestEnemy != null)
        {
            Vector2 toEnemy = nearestEnemy.transform.position - spawnPos;
            if (toEnemy.sqrMagnitude > 0.0001f)
                return toEnemy.normalized;
        }

        return Vector2.right;
    }

    private AttackModule BuildSkill1CounterAttackData()
    {
        return new AttackModule
        {
            Damage = Mathf.Max(0f, _skill1CounterProjectileDamage),
            Speed = Mathf.Max(0f, _skill1CounterProjectileSpeed),
            Distance = Mathf.Max(0.1f, _skill1CounterProjectileDistance),
            AttackCost = 0,
            Trajectory = _skill1CounterProjectileTrajectory,
            Targeting = TargetingPolicy.Closest,
            Area = _skill1CounterProjectileArea,
            RangeRadius = Mathf.Max(0f, _skill1CounterProjectileSplashRadius),
            PiercingCount = Mathf.Max(0, _skill1CounterProjectilePiercingCount),
            Penetration = Mathf.Clamp01(_skill1CounterProjectilePenetration),
            PiercingDecay = Mathf.Clamp01(_skill1CounterProjectilePiercingDecay),
            ProjectilePrefab = null
        };
    }

    private bool TryInitializeCounterProjectile(GameObject projectileObject, AttackModule attackData, Vector3 spawnPos, Vector3 targetPos)
    {
        if (projectileObject == null || attackData == null)
            return false;

        if (projectileObject.TryGetComponent(out RatProjectile ratDirect))
        {
            ratDirect.Initialize(attackData, TeamType.Player, spawnPos, targetPos, _skill1CounterProjectileSpeed, gameObject);
            return true;
        }

        if (projectileObject.TryGetComponent(out DirectProjectile direct))
        {
            direct.Initialize(attackData, TeamType.Player, spawnPos, targetPos, _skill1CounterProjectileSpeed, gameObject);
            return true;
        }

        float arcTravelTime = Mathf.Max(0.01f, _skill1CounterProjectileArcTravelTime);
        float arcHeight = Mathf.Max(0f, _skill1CounterProjectileArcHeight);

        if (projectileObject.TryGetComponent(out RatArcProjectile ratArc))
        {
            ratArc.Initialize(attackData, TeamType.Player, spawnPos, targetPos, arcTravelTime, arcHeight, gameObject);
            return true;
        }

        if (projectileObject.TryGetComponent(out ArcProjectile arc))
        {
            arc.Initialize(attackData, TeamType.Player, spawnPos, targetPos, arcTravelTime, arcHeight, gameObject);
            return true;
        }

        Debug.LogWarning($"[RitualSystem] 스킬 1 반격 실패: 지원되지 않는 투사체 타입 | {projectileObject.name}");
        return false;
    }

    // ──────────────────────────────────────────────
    // 공통 유틸
    // ──────────────────────────────────────────────

    public int GetSkillCost(int skillIndex)
    {
        return skillIndex switch
        {
            1 => _skill1Cost,
            2 => _skill2Cost,
            3 => _skill3Cost,
            _ => 0
        };
    }

    private bool IsReady(int skillIndex, float timer)
    {
        if (timer > 0f)
        {
            Debug.Log($"[RitualSystem] 스킬 사용 실패 | Skill{skillIndex} 쿨타임 | 남은 시간: {timer:F1}s");
            return false;
        }
        return true;
    }

    private bool TryConsumeResource(int cost)
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError("[RitualSystem] ResourceManager를 찾을 수 없습니다.");
            return false;
        }

        if (!ResourceManager.Instance.SubtractMouseCount(cost, "ritual_cost"))
        {
            Debug.Log($"[RitualSystem] 스킬 사용 실패 | 남은 쥐 부족 | 필요: {cost} / 보유: {ResourceManager.Instance.CurrentMouse}");
            ShowResourceFailureFeedback();
            return false;
        }

        return true;
    }

    private void ShowResourceFailureFeedback()
    {
        if (GridManager.Instance == null) return;

        GridManager.Instance.ShowResourceFailureFeedback(GetFeedbackScreenPosition());
    }

    private Vector2 GetFeedbackScreenPosition()
    {
        return InputReader.Instance != null
            ? InputReader.Instance.GetMousePosition()
            : Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    private bool IsDoctrineUnlocked(int skillIndex, string unlockId)
    {
        if (IsDoctrineUnlockedForUI(unlockId))
            return true;

        Debug.Log($"[RitualSystem] 스킬 사용 실패 | Skill{skillIndex} 잠금 상태 | 필요 해금: {unlockId}");
        return false;
    }

    private bool IsDoctrineUnlockedForUI(string unlockId)
    {
        if (!_useDoctrineUnlockChecks)
            return true;

        if (_unlockManager == null)
            _unlockManager = FindAnyObjectByType<UnlockManager>();

        if (_unlockManager == null)
            return true;

        return _unlockManager.IsUnlocked(unlockId);
    }

    private void Awake()
    {
        EnsureGaugeType(_skill1CooldownGauge);
        EnsureGaugeType(_skill2CooldownGauge);
        EnsureGaugeType(_skill3CooldownGauge);
        UpdateCooldownGauges();
    }

    private void Start()
    {
        EnsureInitialRitualUnlocks();
        LogAllSkillLevels("초기화");
    }

    private void OnValidate()
    {
        EnsureGaugeType(_skill1CooldownGauge);
        EnsureGaugeType(_skill2CooldownGauge);
        EnsureGaugeType(_skill3CooldownGauge);
    }

    private void UpdateCooldownGauges()
    {
        UpdateGauge(_skill1CooldownGauge, _skill1CooldownTimer, _skill1Cooldown);
        UpdateGauge(_skill2CooldownGauge, _skill2CooldownTimer, _skill2Cooldown);
        UpdateGauge(_skill3CooldownGauge, _skill3CooldownTimer, _skill3Cooldown);
    }

    private static void UpdateGauge(Image gauge, float remaining, float duration)
    {
        if (gauge == null)
            return;

        // Cooldown progress fills from left(0) to right(1).
        gauge.fillAmount = duration > 0f ? Mathf.Clamp01(1f - (remaining / duration)) : 1f;
    }

    private static void EnsureGaugeType(Image gauge)
    {
        if (gauge == null)
            return;

        if (gauge.type != Image.Type.Filled)
            gauge.type = Image.Type.Filled;

        gauge.fillMethod = Image.FillMethod.Horizontal;
        gauge.fillOrigin = 0; // Left
        gauge.fillClockwise = true;
    }

    private IEnumerator MeteorRoutine()
    {
        if(_enemyCore == null || _enemyCore.IsDead)
        {
            Debug.LogWarning("[RitualSystem] 스킬 2(Meteor) 사용 실패: 적 코어 없음");
            yield break;
        }

        int level = GetSkillLevel(2);
        int meteorCount = Mathf.Max(1, _meteorCount + GetLevelBonus(_meteorCountBonusByLevel, level));
        float meteorDamage = _meteorDamage * GetLevelMultiplier(_meteorDamageMultiplierByLevel, level, 1f);
        float splashRadius = Mathf.Max(0.1f, _splashRadius + GetLevelBonus(_meteorSplashRadiusBonusByLevel, level));
        float targetRadius = Mathf.Max(0.1f, _targetRadius + GetLevelBonus(_meteorTargetRadiusBonusByLevel, level));

        Debug.Log($"[RitualSystem] 스킬 2(Meteor) 시전 | 단계: {level} | 개수: {meteorCount} | 피해: {meteorDamage:0.##} | 범위: {splashRadius:0.##}");

        Vector3 corePos = _enemyCore.transform.position;
        
        Vector3 playerPos = (_playerCore != null && !_playerCore.IsDead) 
            ? _playerCore.transform.position
            : transform.position;       

        for(int i = 0; i < meteorCount; i++)
        {
            // 착탄 위치 : 코어 원 안 랜덤
            Vector2 targetOffset = Random.insideUnitCircle * targetRadius;
            Vector3 targetPos = corePos + new Vector3(targetOffset.x, targetOffset.y, 0f);
        
            // 스폰 위치 : 목표 플레이어 위 + 약간의 X 랜덤 (화면 벽)
            float spawnOffsetX = Random.Range(-targetRadius, targetRadius);
            Vector3 spawnPos = new Vector3(
                playerPos.x + spawnOffsetX,
                playerPos.y + _spawnHeight, 0f
                );

            // 낙하 시간 랜덤 -> 빠른 것 느린 것 섞임
            float duration = Random.Range(_minDuration, _maxDuration);

            var go = PoolManager.Instance?.Spawn(_meteorPoolKey, spawnPos, Quaternion.identity);
            if(go != null && go.TryGetComponent(out MeteorProjectile meteor))
                meteor.Launch(spawnPos, targetPos, duration, meteorDamage, splashRadius); 
        
            yield return new WaitForSeconds(_meteorDelay);
        }
    }

    public int GetSkillLevel(int skillIndex)
    {
        if (_skillLevels.TryGetValue(skillIndex, out int level))
            return level;

        return 0;
    }

    public int UpgradeSkillLevelFromDoctrine(int skillIndex, string effectId, int amount = 1)
    {
        if (!_skillLevels.ContainsKey(skillIndex))
            return 0;

        int before = _skillLevels[skillIndex];
        int after = Mathf.Clamp(before + Mathf.Max(0, amount), 0, MaxSkillLevel);
        _skillLevels[skillIndex] = after;

        Debug.Log($"[RitualSystem] 교리 강화 적용 | effectId: {effectId} | Skill{skillIndex} 단계: {before} -> {after}");
        Debug.Log($"[RitualSystem] 스킬별 현재 강화 단계 | Wall:{GetSkillLevel(1)} Meteor:{GetSkillLevel(2)} RatHero:{GetSkillLevel(3)}");

        EventBus.Instance?.Publish(new RitualSkillLevelChangedEvent
        {
            SkillIndex = skillIndex,
            SkillLevel = after,
            EffectId = effectId
        });
        _onRitualSkillLevelChanged?.Invoke(skillIndex, after);

        return after;
    }

    public void LogAllSkillLevels(string reason)
    {
        Debug.Log($"[RitualSystem] 스킬별 현재 강화 단계 ({reason}) | Wall:{GetSkillLevel(1)} Meteor:{GetSkillLevel(2)} RatHero:{GetSkillLevel(3)}");
    }

    private void EnsureInitialRitualUnlocks()
    {
        if (!_unlockAllRitualSkillsAtStart)
            return;

        if (_unlockManager == null)
            _unlockManager = FindAnyObjectByType<UnlockManager>();

        if (_unlockManager == null)
        {
            Debug.LogWarning("[RitualSystem] UnlockManager를 찾지 못해 초기 의식 해금 등록을 건너뜁니다.");
            return;
        }

        _unlockManager.Unlock(RitualSkill2UnlockId);
        _unlockManager.Unlock(RitualSkill3UnlockId);
        Debug.Log("[RitualSystem] 초기 의식 스킬 해금 등록 완료 (Skill1/2/3 사용 가능)");
    }

    private bool CanCastSkill2()
    {
        bool hasEnemyCore = _enemyCore != null && !_enemyCore.IsDead;
        if (hasEnemyCore)
            return true;

        Debug.Log("[RitualSystem] 스킬 사용 실패 | Skill2 Meteor | 적 코어를 찾지 못했습니다.");
        return false;
    }

    private bool CanCastSkill3()
    {
        return true;
    }

    private void NotifyRatHeroSkillCast(int skillIndex)
    {
        int level = GetSkillLevel(skillIndex);

        EventBus.Instance?.Publish(new RitualSkillCastEvent
        {
            SkillIndex = skillIndex,
            SkillLevel = level
        });

        switch (skillIndex)
        {
            case 1:
                _onSkill1CastToRatHero?.Invoke(level);
                break;

            case 2:
                _onSkill2CastToRatHero?.Invoke(level);
                break;

            case 3:
                _onSkill3CastToRatHero?.Invoke(level);
                break;
        }
    }

    private static int GetLevelBonus(int[] levelBonuses, int level)
    {
        if (levelBonuses == null || level <= 0)
            return 0;

        int index = Mathf.Min(level - 1, levelBonuses.Length - 1);
        return index >= 0 ? levelBonuses[index] : 0;
    }

    private static float GetLevelBonus(float[] levelBonuses, int level)
    {
        if (levelBonuses == null || level <= 0)
            return 0f;

        int index = Mathf.Min(level - 1, levelBonuses.Length - 1);
        return index >= 0 ? levelBonuses[index] : 0f;
    }

    private static float GetLevelMultiplier(float[] levelMultipliers, int level, float defaultMultiplier)
    {
        if (levelMultipliers == null || level <= 0)
            return defaultMultiplier;

        int index = Mathf.Min(level - 1, levelMultipliers.Length - 1);
        if (index < 0)
            return defaultMultiplier;

        float value = levelMultipliers[index];
        return value > 0f ? value : defaultMultiplier;
    }

    // 임시 디버깅용: 버튼 OnClick에 연결해서 교리 효과를 강제 적용
    public void DebugApplyRitualNode0() => DebugApplyDoctrineEffect("Ritual_Node_0");
    public void DebugApplyRitualNode1() => DebugApplyDoctrineEffect("Ritual_Node_1");
    public void DebugApplyRitualNode2() => DebugApplyDoctrineEffect("Ritual_Node_2");
    public void DebugApplyRitualNode3() => DebugApplyDoctrineEffect("Ritual_Node_3");
    public void DebugApplyRitualNode4() => DebugApplyDoctrineEffect("Ritual_Node_4");

    private void DebugApplyDoctrineEffect(string effectId)
    {
        DoctrineEffectApplier applier = FindFirstObjectByType<DoctrineEffectApplier>(FindObjectsInactive.Include);
        if (applier != null)
        {
            Debug.Log($"[RitualSystem] 디버그 교리 적용 요청(DoctrineEffectApplier 경유) | effectId: {effectId}");
            applier.ApplyEffect(effectId);
            return;
        }

        Debug.LogWarning($"[RitualSystem] DoctrineEffectApplier를 찾지 못해 로컬 디버그 적용으로 전환합니다. | effectId: {effectId}");
        DebugApplyDoctrineEffectLocally(effectId);
    }

    private void DebugApplyDoctrineEffectLocally(string effectId)
    {
        switch (effectId)
        {
            case "Ritual_Node_0":
                UpgradeSkillLevelFromDoctrine(2, effectId, 1);
                break;

            case "Ritual_Node_1":
                DebugReduceSkillCosts(0.5f);
                EventBus.Instance?.Publish(new RitualCostChangedEvnet());
                break;

            case "Ritual_Node_2":
                UpgradeSkillLevelFromDoctrine(1, effectId, 1);
                UpgradeSkillLevelFromDoctrine(2, effectId, 1);
                UpgradeSkillLevelFromDoctrine(3, effectId, 1);
                break;

            case "Ritual_Node_3":
                DebugReduceSkillCooldowns(0.5f);
                break;

            case "Ritual_Node_4":
                // 현재 디버깅 룰: Ritual_Node_4 적용 시 의식 3종 모두 2단계
                UpgradeSkillLevelFromDoctrine(1, effectId, 99);
                UpgradeSkillLevelFromDoctrine(2, effectId, 99);
                UpgradeSkillLevelFromDoctrine(3, effectId, 99);
                break;

            default:
                Debug.LogWarning($"[RitualSystem] 로컬 디버그 적용 실패: 미지원 effectId | {effectId}");
                break;
        }
    }

    private void DebugReduceSkillCosts(float multiplier)
    {
        int before1 = _skill1Cost;
        int before2 = _skill2Cost;
        int before3 = _skill3Cost;

        _skill1Cost = Mathf.Max(0, Mathf.CeilToInt(_skill1Cost * multiplier));
        _skill2Cost = Mathf.Max(0, Mathf.CeilToInt(_skill2Cost * multiplier));
        _skill3Cost = Mathf.Max(0, Mathf.CeilToInt(_skill3Cost * multiplier));

        Debug.Log($"[RitualSystem] 로컬 디버그 적용 | 비용 감소 x{multiplier:0.##} | S1:{before1}->{_skill1Cost} S2:{before2}->{_skill2Cost} S3:{before3}->{_skill3Cost}");
    }

    private void DebugReduceSkillCooldowns(float multiplier)
    {
        float before1 = _skill1Cooldown;
        float before2 = _skill2Cooldown;
        float before3 = _skill3Cooldown;

        _skill1Cooldown = Mathf.Max(0.01f, _skill1Cooldown * multiplier);
        _skill2Cooldown = Mathf.Max(0.01f, _skill2Cooldown * multiplier);
        _skill3Cooldown = Mathf.Max(0.01f, _skill3Cooldown * multiplier);

        Debug.Log($"[RitualSystem] 로컬 디버그 적용 | 쿨다운 감소 x{multiplier:0.##} | S1:{before1:0.##}->{_skill1Cooldown:0.##} S2:{before2:0.##}->{_skill2Cooldown:0.##} S3:{before3:0.##}->{_skill3Cooldown:0.##}");
    }
}
