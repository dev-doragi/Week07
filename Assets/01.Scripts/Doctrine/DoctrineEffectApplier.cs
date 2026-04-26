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

    private struct ResolvedUnlockBinding
    {
        public UnlockEventKind EventKind;
        public string UnlockId;
    }

    [Header("Optional References")]
    [SerializeField] private RitualSystem ritualSystem;
    [SerializeField] private GaugeController gaugeController;
    [SerializeField] private SiegeChargeHandler siegeChargeHandler;
    [SerializeField] private GridManager playerGrid;

    [Header("Doctrine Tunables")]
    [SerializeField, Range(0f, 1f)] private float ritualWallHealPercent = 0.1f;
    [SerializeField, Min(0.05f)] private float wallMonitorInterval = 0.1f;
    [SerializeField, Min(1f)] private float ramGaugeGainMultiplier = 2f;

    [Header("Unlock Event Bindings")]
    [SerializeField] private List<UnlockEventBinding> unlockEventBindings = new List<UnlockEventBinding>();

    private readonly HashSet<string> _appliedEffectIds = new HashSet<string>();

    private bool _ritualWallHealEnabled;
    private Coroutine _ritualWallMonitorRoutine;
    private bool _lastWallActiveState;

    private const BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    // EffectSummary ?곕룞 怨듯넻: ?낇듃由??④낵 ?곸슜???꾪븳 李몄“ 罹먯떆 珥덇린??
    private void Awake()
    {
        ResolveReferences();
    }

    // EffectSummary ?곕룞 怨듯넻: ?섏떇 媛뺥솕 紐⑤땲???ш컻
    private void OnEnable()
    {
        ResolveReferences();

        if (_ritualWallHealEnabled && _ritualWallMonitorRoutine == null)
        {
            _ritualWallMonitorRoutine = StartCoroutine(RitualWallMonitorRoutine());
        }
    }

    // EffectSummary ?곕룞 怨듯넻: ?섏떇 媛뺥솕 紐⑤땲???뺤?
    private void OnDisable()
    {
        if (_ritualWallMonitorRoutine != null)
        {
            StopCoroutine(_ritualWallMonitorRoutine);
            _ritualWallMonitorRoutine = null;
        }
    }

    // EffectSummary 怨듯넻 吏꾩엯?? effectId蹂꾨줈 媛??낇듃由??④낵瑜?遺꾧린 ?곸슜
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
                ApplyRamGaugeGainBoost();
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

    public void RepublishUnlockEventsForAppliedEffects()
    {
        foreach (string effectId in _appliedEffectIds)
            TryPublishUnlockEvent(effectId);
    }

    public void CollectUnlockIdsForAppliedEffects(ICollection<string> result)
    {
        if (result == null)
            return;

        var resolved = new List<ResolvedUnlockBinding>();
        foreach (string effectId in _appliedEffectIds)
        {
            if (!TryResolveUnlockBindings(effectId, resolved))
                continue;

            for (int i = 0; i < resolved.Count; i++)
                result.Add(resolved[i].UnlockId);
        }
    }

    private bool TryPublishUnlockEvent(string effectId)
    {
        var resolved = new List<ResolvedUnlockBinding>();
        if (!TryResolveUnlockBindings(effectId, resolved))
            return false;

        for (int i = 0; i < resolved.Count; i++)
            PublishUnlockEvent(resolved[i].EventKind, resolved[i].UnlockId);

        return true;
    }

    private bool TryResolveUnlockBindings(string effectId, List<ResolvedUnlockBinding> result)
    {
        result.Clear();
        bool hasExplicit = false;

        for (int i = 0; i < unlockEventBindings.Count; i++)
        {
            UnlockEventBinding binding = unlockEventBindings[i];
            if (binding == null || !string.Equals(binding.effectId, effectId, System.StringComparison.Ordinal))
                continue;

            hasExplicit = true;
            string unlockId = string.IsNullOrWhiteSpace(binding.unlockId) ? effectId : binding.unlockId;
            result.Add(new ResolvedUnlockBinding
            {
                EventKind = binding.eventKind,
                UnlockId = unlockId
            });
        }

        if (hasExplicit)
            return result.Count > 0;

        if (!TryGetDefaultUnlockBinding(effectId, out UnlockEventKind fallbackEventKind, out string fallbackUnlockId))
            return false;

        result.Add(new ResolvedUnlockBinding
        {
            EventKind = fallbackEventKind,
            UnlockId = fallbackUnlockId
        });
        return true;
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

    // EffectSummary ?곕룞 怨듯넻: 異⑷컖/?섏떇/????④낵???꾩슂???고???李몄“ ?먮룞 ?먯깋
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

    // EffectSummary: 異⑷컖 ??뵾??30% 媛먯냼
    private void ApplyRamSelfDamageReduction(float percent)
    {
        if (siegeChargeHandler == null)
        {
            Debug.LogWarning("[DoctrineEffectApplier] SiegeChargeHandler not found. Ram self-damage reduction skipped.");
            return;
        }

        siegeChargeHandler.SetDoctrineSelfDamageReductionPercent(percent);
    }

    // EffectSummary: 異⑷컖 荑⑦???50% 媛먯냼
    private void ApplyRamGaugeGainBoost()
    {
        if (gaugeController == null)
        {
            Debug.LogWarning("[DoctrineEffectApplier] GaugeController not found. Ram gauge gain boost skipped.");
            return;
        }

        gaugeController.SetDoctrineGaugeGainMultiplier(ramGaugeGainMultiplier);
        Debug.Log($"[DoctrineEffectApplier] Applied: Ram gauge gain x{ramGaugeGainMultiplier:0.##}");
    }

    // EffectSummary: 異⑷컖 ?????꾩껜 2珥?湲곗젅
    private void ApplyRamStunDuration(float durationSeconds)
    {
        if (siegeChargeHandler == null)
        {
            Debug.LogWarning("[DoctrineEffectApplier] SiegeChargeHandler not found. Ram stun skipped.");
            return;
        }

        siegeChargeHandler.SetDoctrineStunDurationSeconds(durationSeconds);
    }

    // EffectSummary: 異⑷컖 ?쇳빐 50% 利앷?
    private void ApplyRamBonusDamage(float percent)
    {
        if (siegeChargeHandler == null)
        {
            Debug.LogWarning("[DoctrineEffectApplier] SiegeChargeHandler not found. Ram bonus damage skipped.");
            return;
        }

        siegeChargeHandler.SetDoctrineBonusDamagePercent(percent);
    }

    // EffectSummary: ?섏떇???ъ슜?섎뒗???쒕뒗 伊???50% 媛먯냼
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

    // EffectSummary: ?섏떇??荑⑦???50% 媛먯냼
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

    // EffectSummary: 伊먮꼍 ?섏떇??媛뺥솕 - ?ъ슜 ??紐⑤뱺 伊먯쓽 泥대젰 ?뚮웾 ?뚮났
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

    // EffectSummary: 伊먮꼍 ?섏떇??媛뺥솕 - ?ъ슜 ??紐⑤뱺 伊먯쓽 泥대젰 ?뚮웾 ?뚮났
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

    // EffectSummary: 紐⑤뱺 怨듦꺽 ??뚯쓽 ?쇳빐 +50%
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

    // EffectSummary: 紐⑤뱺 諛⑹뼱 ??뚯쓽 理쒕? 泥대젰 +50
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

    // EffectSummary: 伊먮꼍 ?섏떇??媛뺥솕 - ?ъ슜 ??紐⑤뱺 伊먯쓽 泥대젰 ?뚮웾 ?뚮났
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

    // EffectSummary ?곕룞 怨듯넻: ?섏떇/????④낵 ????꾧뎔) ?섏쭛
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

    // EffectSummary: ?섏떇???ъ슜?섎뒗???쒕뒗 伊???50% 媛먯냼
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

    // EffectSummary: ?섏떇??荑⑦???50% 媛먯냼
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

    // EffectSummary: 伊먮꼍 ?섏떇??媛뺥솕 - ?ъ슜 ??紐⑤뱺 伊먯쓽 泥대젰 ?뚮웾 ?뚮났
    private static T GetPrivateObjectField<T>(object target, string fieldName) where T : class
    {
        if (target == null || !TryGetPrivateField(target, fieldName, out FieldInfo field))
        {
            return null;
        }

        return field.GetValue(target) as T;
    }

    // EffectSummary ?곕룞 怨듯넻: 由ы뵆?됱뀡 湲곕컲 ?꾨뱶 ?묎렐 ?ы띁
    private static bool TryGetPrivateField(object target, string fieldName, out FieldInfo fieldInfo)
    {
        fieldInfo = target.GetType().GetField(fieldName, PrivateInstanceFlags);
        return fieldInfo != null;
    }
}
