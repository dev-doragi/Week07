using System.Collections.Generic;
using UnityEngine;

public class EntityStatReceiver : MonoBehaviour
{
    private const string DiminishingReturnConfigResourcePath = "StatDiminishingReturnConfig";
    private const float DefaultAttackDamageMaxBonus = 1f;
    private const float DefaultAttackDamageHalfPoint = 1f;
    private const float DefaultAttackSpeedMaxBonus = 0.75f;
    private const float DefaultAttackSpeedHalfPoint = 1f;

    private static StatDiminishingReturnConfig _cachedDiminishingReturnConfig;

    // 출처별 버프 저장: supporter → (statType → (modifierType, value))
    private Dictionary<object, List<(SupportStatType type, ModifierType mod, float value)>> _sourceModifiers = new();

    public void SetModifier(object source, PartSupportEffectData effect)
    {
        if (effect == null) return;

        if (!_sourceModifiers.ContainsKey(source))
            _sourceModifiers[source] = new List<(SupportStatType, ModifierType, float)>();
        else
            _sourceModifiers[source].RemoveAll(e => e.type == effect.TargetStatType);

        _sourceModifiers[source].Add((effect.TargetStatType, effect.ModifierType, effect.Value));
    }

    public void RemoveModifier(object source)
    {
        _sourceModifiers.Remove(source);
    }

    public void ResetModifiers()
    {
        _sourceModifiers.Clear();
    }

    public float GetModifiedValue(SupportStatType type, float baseValue)
    {
        float flat = 0f;
        float percent = 0f;
        float firstBonus = 0f;

        foreach (var modifiers in _sourceModifiers.Values)
        {
            foreach (var (statType, mod, value) in modifiers)
            {
                if (statType != type) continue;
                if (mod == ModifierType.Flat)
                    flat += value;
                else
                {
                    percent += value;
                    firstBonus = Mathf.Max(firstBonus, value);
                }
            }
        }

        percent = ApplyDiminishingReturns(type, percent, firstBonus);
        return (baseValue + flat) * (1f + percent);
    }

    private float ApplyDiminishingReturns(SupportStatType type, float rawPercent, float firstBonus)
    {
        StatDiminishingReturnConfig config = ResolveDiminishingReturnConfig();

        return type switch
        {
            SupportStatType.AttackDamage => ApplyConvergingBonusAfterFirst(
                rawPercent,
                firstBonus,
                config != null ? config.AttackDamageMaxBonus : DefaultAttackDamageMaxBonus,
                config != null ? config.AttackDamageHalfPoint : DefaultAttackDamageHalfPoint),

            SupportStatType.AttackSpeed => ApplyConvergingBonusAfterFirst(
                rawPercent,
                firstBonus,
                config != null ? config.AttackSpeedMaxBonus : DefaultAttackSpeedMaxBonus,
                config != null ? config.AttackSpeedHalfPoint : DefaultAttackSpeedHalfPoint),

            _ => rawPercent
        };
    }

    private StatDiminishingReturnConfig ResolveDiminishingReturnConfig()
    {
        if (_cachedDiminishingReturnConfig == null)
        {
            _cachedDiminishingReturnConfig =
                Resources.Load<StatDiminishingReturnConfig>(DiminishingReturnConfigResourcePath);
        }

        return _cachedDiminishingReturnConfig;
    }

    private static float ApplyConvergingBonusAfterFirst(
        float rawPercent,
        float firstBonus,
        float maxBonus,
        float halfPoint)
    {
        if (rawPercent <= 0f || firstBonus <= 0f)
            return rawPercent;

        float safeMaxBonus = Mathf.Max(maxBonus, firstBonus);
        if (rawPercent <= firstBonus)
            return rawPercent;

        float safeHalfPoint = Mathf.Max(halfPoint, safeMaxBonus);
        float excess = rawPercent - firstBonus;
        float remainingCap = safeMaxBonus - firstBonus;

        if (remainingCap <= 0f)
            return firstBonus;

        return firstBonus + remainingCap * excess / (excess + safeHalfPoint);
    }

}
