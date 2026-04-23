using Mono.Cecil;
using System.Collections.Generic;
using UnityEngine;

public class EntityStatReceiver : MonoBehaviour
{
    private Dictionary<SupportStatType, float> _flatBonuses = new();
    private Dictionary<SupportStatType, float> _percentBonuses = new();

    public void ResetModifiers()
    {
        _flatBonuses.Clear();
        _percentBonuses.Clear();
    }

    public void ApplyModifier(PartSupportEffectData effect)
    {
        if (effect == null) return;
        ApplyModifier(effect.TargetStatType, effect.ModifierType, effect.Value);
    }

    public void ApplyModifier(SupportStatType type, ModifierType mod, float value)
    {
        if (mod == ModifierType.Flat)
            _flatBonuses[type] = _flatBonuses.GetValueOrDefault(type) + value;
        else
            _percentBonuses[type] = _percentBonuses.GetValueOrDefault(type) + value;
    }

    public float GetModifiedValue(SupportStatType type, float baseValue)
    {
        float flat = _flatBonuses.GetValueOrDefault(type, 0f);
        float percent = _percentBonuses.GetValueOrDefault(type, 0f);
        return (baseValue + flat) * (1f + percent);
    }
}