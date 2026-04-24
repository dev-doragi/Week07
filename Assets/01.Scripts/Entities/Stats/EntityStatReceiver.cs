using System.Collections.Generic;
using UnityEngine;

public class EntityStatReceiver : MonoBehaviour
{
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

        foreach (var modifiers in _sourceModifiers.Values)
        {
            foreach (var (statType, mod, value) in modifiers)
            {
                if (statType != type) continue;
                if (mod == ModifierType.Flat)
                    flat += value;
                else
                    percent += value;
            }
        }

        return (baseValue + flat) * (1f + percent);
    }
}