using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class DoctrineEffectApplier : MonoBehaviour
{
    private enum UnlockEventKind
    {
        Rat = 0,
        Ritual = 1,
        Feature = 2
    }

    [System.Serializable]
    private class UnlockEventBinding
    {
        public string effectId;
        public string unlockId;
        public UnlockEventKind eventKind = UnlockEventKind.Feature;
    }

    [Header("Optional References")]
    [SerializeField] private RitualSystem ritualSystem;
    [SerializeField] private GaugeController gaugeController;
    [SerializeField] private SiegeChargeHandler siegeChargeHandler;
    [SerializeField] private GridManager playerGrid;

    [Header("Doctrine Tunables")]
    [SerializeField, Range(0f, 1f)] private float ritualWallHealPercent = 0.1f;
    [SerializeField, Min(0.05f)] private float wallMonitorInterval = 0.1f;

    [Header("Unlock Event Bindings")]
    [SerializeField] private List<UnlockEventBinding> unlockEventBindings = new List<UnlockEventBinding>();

    private readonly HashSet<string> _appliedEffectIds = new HashSet<string>();

    private bool _ritualWallHealEnabled;
    private Coroutine _ritualWallMonitorRoutine;
    private bool _lastWallActiveState;

    private const BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    // EffectSummary 연동 공통: 독트린 효과 적용을 위한 참조 캐시 초기화
    private void Awake()
    {
        ResolveReferences();
    }

    // EffectSummary 연동 공통: 의식 강화 모니터 재개
    private void OnEnable()
    {
        ResolveReferences();

        if (_ritualWallHealEnabled && _ritualWallMonitorRoutine == null)
        {
            _ritualWallMonitorRoutine = StartCoroutine(RitualWallMonitorRoutine());
        }
    }

    // EffectSummary 연동 공통: 의식 강화 모니터 정지
    private void OnDisable()
    {
        if (_ritualWallMonitorRoutine != null)
        {
            StopCoroutine(_ritualWallMonitorRoutine);
            _ritualWallMonitorRoutine = null;
        }
    }

    // EffectSummary 공통 진입점: effectId별로 각 독트린 효과를 분기 적용
    public void ApplyEffect(string effectId)
    {
        if (string.IsNullOrWhiteSpace(effectId))
        {
            Debug.Log("[DoctrineEffectApplier] Empty effectId. No effect applied.");
            return;
        }

        if (!_appliedEffectIds.Add(effectId))
        {
            Debug.Log($"[DoctrineEffectApplier] Effect already applied: {effectId}");
            return;
        }

        ResolveReferences();
        bool unlockEventPublished = TryPublishUnlockEvent(effectId);

        switch (effectId)
        {
            case "Ram_Node_0":
            case "Ritual_Node_0":
            case "Ritual_Node_4":
            case "Tower_Node_0":
            case "Tower_Node_2":
            case "Tower_Node_4":
                if (!unlockEventPublished)
                {
                    Debug.Log($"[DoctrineEffectApplier] Unlock-type effect has no binding: {effectId}");
                }
                break;

            case "Ram_Node_1":
                ApplyRamSelfDamageReduction(0.3f);
                break;

            case "Ram_Node_2":
                ApplyRamCooldownReduction();
                break;

            case "Ram_Node_3":
                ApplyRamStunDuration(2f);
                break;

            case "Ram_Node_4":
                ApplyRamBonusDamage(0.5f);
                break;

            case "Ritual_Node_1":
                ApplyRitualCostReduction(0.5f);
                break;

            case "Ritual_Node_2":
                EnableRitualWallHeal();
                break;

            case "Ritual_Node_3":
                ApplyRitualCooldownReduction(0.5f);
                break;

            case "Tower_Node_1":
                ApplyTowerAttackDamageBuff(0.5f);
                break;

            case "Tower_Node_3":
                ApplyTowerMaxHpBuff(50f);
                break;

            default:
                Debug.LogWarning($"[DoctrineEffectApplier] Unknown effectId: {effectId}");
                break;
        }

        Debug.Log($"[DoctrineEffectApplier] Doctrine Effect Applied: {effectId}");
    }

    private bool TryPublishUnlockEvent(string effectId)
    {
        bool published = false;

        for (int i = 0; i < unlockEventBindings.Count; i++)
        {
            UnlockEventBinding binding = unlockEventBindings[i];
            if (binding == null || !string.Equals(binding.effectId, effectId, System.StringComparison.Ordinal))
            {
                continue;
            }

            string unlockId = string.IsNullOrWhiteSpace(binding.unlockId) ? effectId : binding.unlockId;

            PublishUnlockEvent(binding.eventKind, unlockId);
            published = true;
        }

        if (!published && TryGetDefaultUnlockBinding(effectId, out UnlockEventKind fallbackEventKind, out string fallbackUnlockId))
        {
            PublishUnlockEvent(fallbackEventKind, fallbackUnlockId);
            published = true;
        }

        return published;
    }

    private static bool TryGetDefaultUnlockBinding(string effectId, out UnlockEventKind eventKind, out string unlockId)
    {
        eventKind = UnlockEventKind.Feature;
        unlockId = effectId;

        switch (effectId)
        {
            case "Ram_Node_0":
            case "Tower_Node_0":
            case "Tower_Node_2":
            case "Tower_Node_4":
                eventKind = UnlockEventKind.Rat;
                return true;

            case "Ritual_Node_0":
            case "Ritual_Node_4":
                eventKind = UnlockEventKind.Ritual;
                return true;

            default:
                return false;
        }
    }

    private static void PublishUnlockEvent(UnlockEventKind eventKind, string unlockId)
    {
        switch (eventKind)
        {
            case UnlockEventKind.Rat:
                EventBus.Instance?.Publish(new RatUnlockedEvent { RatId = unlockId });
                break;
            case UnlockEventKind.Ritual:
                EventBus.Instance?.Publish(new RitualUnlockedEvent { RitualId = unlockId });
                break;
            case UnlockEventKind.Feature:
                break;
        }

        EventBus.Instance?.Publish(new FeatureUnlockedEvent { UnlockId = unlockId });
    }

    // EffectSummary 연동 공통: 충각/의식/타워 효과에 필요한 런타임 참조 자동 탐색
    private void ResolveReferences()
    {
        if (ritualSystem == null)
        {
            ritualSystem = FindAnyObjectByType<RitualSystem>();
        }

        if (gaugeController == null)
        {
            gaugeController = FindAnyObjectByType<GaugeController>();
        }

        if (siegeChargeHandler == null)
        {
            siegeChargeHandler = FindAnyObjectByType<SiegeChargeHandler>();
        }

        if (playerGrid == null)
        {
            playerGrid = GridManager.Instance != null ? GridManager.Instance : FindAnyObjectByType<GridManager>();
        }
    }

    // EffectSummary: 충각 역피해 30% 감소
    private void ApplyRamSelfDamageReduction(float percent)
    {
        if (siegeChargeHandler == null)
        {
            Debug.LogWarning("[DoctrineEffectApplier] SiegeChargeHandler not found. Ram self-damage reduction skipped.");
            return;
        }

        siegeChargeHandler.SetDoctrineSelfDamageReductionPercent(percent);
    }

    // EffectSummary: 충각 쿨타임 50% 감소
    private void ApplyRamCooldownReduction()
    {
        if (gaugeController == null)
        {
            Debug.LogWarning("[DoctrineEffectApplier] GaugeController not found. Ram cooldown reduction skipped.");
            return;
        }

        gaugeController.SetDoctrineGaugeGainMultiplier(2f);
    }

    // EffectSummary: 충각 시 적 전체 2초 기절
    private void ApplyRamStunDuration(float durationSeconds)
    {
        if (siegeChargeHandler == null)
        {
            Debug.LogWarning("[DoctrineEffectApplier] SiegeChargeHandler not found. Ram stun skipped.");
            return;
        }

        siegeChargeHandler.SetDoctrineStunDurationSeconds(durationSeconds);
    }

    // EffectSummary: 충각 피해 50% 증가
    private void ApplyRamBonusDamage(float percent)
    {
        if (siegeChargeHandler == null)
        {
            Debug.LogWarning("[DoctrineEffectApplier] SiegeChargeHandler not found. Ram bonus damage skipped.");
            return;
        }

        siegeChargeHandler.SetDoctrineBonusDamagePercent(percent);
    }

    // EffectSummary: 의식을 사용하는데 드는 쥐 수 50% 감소
    private void ApplyRitualCostReduction(float multiplier)
    {
        if (ritualSystem == null)
        {
            Debug.LogWarning("[DoctrineEffectApplier] RitualSystem not found. Ritual cost reduction skipped.");
            return;
        }

        ReducePrivateIntField(ritualSystem, "_skill1Cost", multiplier);
        ReducePrivateIntField(ritualSystem, "_skill2Cost", multiplier);
        ReducePrivateIntField(ritualSystem, "_skill3Cost", multiplier);
    }

    // EffectSummary: 의식의 쿨타임 50% 감소
    private void ApplyRitualCooldownReduction(float multiplier)
    {
        if (ritualSystem == null)
        {
            Debug.LogWarning("[DoctrineEffectApplier] RitualSystem not found. Ritual cooldown reduction skipped.");
            return;
        }

        ReducePrivateFloatField(ritualSystem, "_skill1Cooldown", multiplier);
        ReducePrivateFloatField(ritualSystem, "_skill2Cooldown", multiplier);
        ReducePrivateFloatField(ritualSystem, "_skill3Cooldown", multiplier);
    }

    // EffectSummary: 쥐벽 의식의 강화 - 사용 시 모든 쥐의 체력 소량 회복
    private void EnableRitualWallHeal()
    {
        if (ritualSystem == null)
        {
            Debug.LogWarning("[DoctrineEffectApplier] RitualSystem not found. Ritual wall heal skipped.");
            return;
        }

        _ritualWallHealEnabled = true;
        _lastWallActiveState = false;

        if (_ritualWallMonitorRoutine == null)
        {
            _ritualWallMonitorRoutine = StartCoroutine(RitualWallMonitorRoutine());
        }

        Debug.Log("[DoctrineEffectApplier] Applied: Ritual wall enhancement (small heal to all allied units on cast)");
    }

    // EffectSummary: 쥐벽 의식의 강화 - 사용 시 모든 쥐의 체력 소량 회복
    private IEnumerator RitualWallMonitorRoutine()
    {
        while (_ritualWallHealEnabled)
        {
            ResolveReferences();
            GameObject wallObject = GetPrivateObjectField<GameObject>(ritualSystem, "_wallObject");
            bool isWallActive = wallObject != null && wallObject.activeInHierarchy;

            if (isWallActive && !_lastWallActiveState)
            {
                HealAllPlayerUnitsByPercent(ritualWallHealPercent);
            }

            _lastWallActiveState = isWallActive;
            yield return new WaitForSeconds(wallMonitorInterval);
        }

        _ritualWallMonitorRoutine = null;
    }

    // EffectSummary: 모든 공격 타워의 피해 +50%
    private void ApplyTowerAttackDamageBuff(float bonusPercent)
    {
        float multiplier = 1f + bonusPercent;
        int changedCount = 0;

        UnitDataSO[] allData = Resources.FindObjectsOfTypeAll<UnitDataSO>();
        for (int i = 0; i < allData.Length; i++)
        {
            UnitDataSO data = allData[i];
            if (data == null || data.Team != TeamType.Player || data.Category != UnitCategory.Attack || data.Attack == null)
            {
                continue;
            }

            data.Attack.Damage *= multiplier;
            changedCount++;
        }

        Debug.Log($"[DoctrineEffectApplier] Applied: Player attack tower damage +{bonusPercent * 100f:0}% | Data entries changed: {changedCount}");
    }

    // EffectSummary: 모든 방어 타워의 최대 체력 +50
    private void ApplyTowerMaxHpBuff(float bonusFlat)
    {
        int changedCount = 0;

        UnitDataSO[] allData = Resources.FindObjectsOfTypeAll<UnitDataSO>();
        for (int i = 0; i < allData.Length; i++)
        {
            UnitDataSO data = allData[i];
            if (data == null || data.Team != TeamType.Player || data.Category != UnitCategory.Defense)
            {
                continue;
            }

            data.MaxHp += bonusFlat;
            changedCount++;
        }

        Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            Unit unit = units[i];
            if (unit == null || unit.IsDead || unit.Team != TeamType.Player || unit.Category != UnitCategory.Defense)
            {
                continue;
            }

            unit.Heal(bonusFlat);
        }

        Debug.Log($"[DoctrineEffectApplier] Applied: Player defense tower max HP +{bonusFlat:0} | Data entries changed: {changedCount}");
    }

    // EffectSummary: 쥐벽 의식의 강화 - 사용 시 모든 쥐의 체력 소량 회복
    private void HealAllPlayerUnitsByPercent(float percent)
    {
        List<Unit> targets = GetLivingPlayerUnits();
        for (int i = 0; i < targets.Count; i++)
        {
            Unit unit = targets[i];
            float amount = unit.Data != null ? unit.Data.MaxHp * percent : 0f;
            unit.Heal(amount);
        }
    }

    // EffectSummary 연동 공통: 의식/타워 효과 대상(아군) 수집
    private List<Unit> GetLivingPlayerUnits()
    {
        ResolveReferences();

        if (playerGrid != null)
        {
            return playerGrid.GetAllLivingUnits();
        }

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        var result = new List<Unit>();
        for (int i = 0; i < allUnits.Length; i++)
        {
            Unit unit = allUnits[i];
            if (unit != null && !unit.IsDead && unit.Team == TeamType.Player)
            {
                result.Add(unit);
            }
        }

        return result;
    }

    // EffectSummary: 의식을 사용하는데 드는 쥐 수 50% 감소
    private static void ReducePrivateIntField(object target, string fieldName, float multiplier)
    {
        if (target == null || !TryGetPrivateField(target, fieldName, out FieldInfo field) || field.FieldType != typeof(int))
        {
            Debug.LogWarning($"[DoctrineEffectApplier] Missing int field: {fieldName}");
            return;
        }

        int before = (int)field.GetValue(target);
        int after = Mathf.Max(0, Mathf.CeilToInt(before * multiplier));
        field.SetValue(target, after);
        Debug.Log($"[DoctrineEffectApplier] Applied field change: {fieldName} {before} -> {after}");
    }

    // EffectSummary: 의식의 쿨타임 50% 감소
    private static void ReducePrivateFloatField(object target, string fieldName, float multiplier)
    {
        if (target == null || !TryGetPrivateField(target, fieldName, out FieldInfo field) || field.FieldType != typeof(float))
        {
            Debug.LogWarning($"[DoctrineEffectApplier] Missing float field: {fieldName}");
            return;
        }

        float before = (float)field.GetValue(target);
        float after = Mathf.Max(0.01f, before * multiplier);
        field.SetValue(target, after);
        Debug.Log($"[DoctrineEffectApplier] Applied field change: {fieldName} {before:F2} -> {after:F2}");
    }

    // EffectSummary: 쥐벽 의식의 강화 - 사용 시 모든 쥐의 체력 소량 회복
    private static T GetPrivateObjectField<T>(object target, string fieldName) where T : class
    {
        if (target == null || !TryGetPrivateField(target, fieldName, out FieldInfo field))
        {
            return null;
        }

        return field.GetValue(target) as T;
    }

    // EffectSummary 연동 공통: 리플렉션 기반 필드 접근 헬퍼
    private static bool TryGetPrivateField(object target, string fieldName, out FieldInfo fieldInfo)
    {
        fieldInfo = target.GetType().GetField(fieldName, PrivateInstanceFlags);
        return fieldInfo != null;
    }
}
