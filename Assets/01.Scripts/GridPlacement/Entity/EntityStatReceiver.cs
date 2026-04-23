using Mono.Cecil;
using System.Collections.Generic;
using UnityEngine;

public class EntityStatReceiver : MonoBehaviour
{
    private Dictionary<E_SupportStatType, float> _flatBonuses = new();
    private Dictionary<E_SupportStatType, float> _percentBonuses = new();

    public void ResetModifiers()
    {
        _flatBonuses.Clear();
        _percentBonuses.Clear();
    }

    public void ApplyModifier(E_SupportStatType type, E_ModifierType mod, float value)
    {
        if (mod == E_ModifierType.Flat)
            _flatBonuses[type] = _flatBonuses.GetValueOrDefault(type) + value;
        else
            _percentBonuses[type] = _percentBonuses.GetValueOrDefault(type) + value;
    }

    public float GetModifiedValue(E_SupportStatType type, float baseValue)
    {
        float flat = _flatBonuses.GetValueOrDefault(type, 0f);
        float percent = _percentBonuses.GetValueOrDefault(type, 0f);
        return (baseValue + flat) * (1f + percent);
    }
}