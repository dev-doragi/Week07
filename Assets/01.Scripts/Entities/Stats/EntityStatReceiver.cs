using System.Collections.Generic;
using UnityEngine;

public class EntityStatReceiver : MonoBehaviour
{
    [Header("Diminishing Returns - Attack Damage")]
    [SerializeField, Min(0f)] private float _attackDamageMaxBonus = 1f;
    [SerializeField, Min(0f)] private float _attackDamageHalfPoint = 1f;

    [Header("Diminishing Returns - Attack Speed")]
    [SerializeField, Min(0f)] private float _attackSpeedMaxBonus = 0.75f;
    [SerializeField, Min(0f)] private float _attackSpeedHalfPoint = 1f;

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
        return type switch
        {
            SupportStatType.AttackDamage => ApplyConvergingBonusAfterFirst(
                rawPercent,
                firstBonus,
                _attackDamageMaxBonus,
                _attackDamageHalfPoint),

            SupportStatType.AttackSpeed => ApplyConvergingBonusAfterFirst(
                rawPercent,
                firstBonus,
                _attackSpeedMaxBonus,
                _attackSpeedHalfPoint),

            _ => rawPercent
        };
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

    private void OnValidate()
    {
        _attackDamageMaxBonus = Mathf.Max(0f, _attackDamageMaxBonus);
        _attackSpeedMaxBonus = Mathf.Max(0f, _attackSpeedMaxBonus);
        _attackDamageHalfPoint = Mathf.Max(_attackDamageMaxBonus, _attackDamageHalfPoint);
        _attackSpeedHalfPoint = Mathf.Max(_attackSpeedMaxBonus, _attackSpeedHalfPoint);
    }
}
