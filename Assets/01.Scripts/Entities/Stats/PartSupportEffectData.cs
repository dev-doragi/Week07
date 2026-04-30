using System;
using UnityEngine;

[Serializable]
public class PartSupportEffectData
{
    [SerializeField] private SupportTargetRoleType _targetRoleType;
    [SerializeField] private SupportStatType _targetStatType;
    [SerializeField] private ModifierType _modifierType;
    [SerializeField] private float _value;
    [SerializeField] private string _description;
    public SupportTargetRoleType TargetRoleType => _targetRoleType;
    public SupportStatType TargetStatType => _targetStatType;
    public ModifierType ModifierType => _modifierType;
    public float Value => _value;
    public string Description => _description;
    public PartSupportEffectData(
        SupportTargetRoleType targetRoleType,
        SupportStatType targetStatType,
        ModifierType modifierType,
        float value)
    {
        // 지원 대상 역할 저장
        _targetRoleType = targetRoleType;
        // 지원할 대상 스탯 저장
        _targetStatType = targetStatType;
        // 증가 방식 저장
        _modifierType = modifierType;
        // 증가 수치 저장
        _value = value;
        // 효과 텍스트
        _description = EffectDescription();
    }

    public string EffectDescription()
    {
        string description = "";
        if (_targetRoleType == SupportTargetRoleType.Attack)
            description += "모든 공격 쥐의 ";
        else if (_targetRoleType == SupportTargetRoleType.Defense)
            description += "모든 방어 쥐의 ";
        else if (_targetRoleType == SupportTargetRoleType.All)
            description += "모든 쥐의 ";
        if (_targetStatType == SupportStatType.AttackSpeed)
            description += "공격 속도를 ";
        else if (_targetStatType == SupportStatType.AttackDamage)
            description += "공격력을 ";
        else if (_targetStatType == SupportStatType.DefenseRate)
            description += "방어력을 ";
        else if (_targetStatType == SupportStatType.PenetrationRate)
            description += "관통력을 ";
        if (_value >= 1)
            description += $"{_value} ";
        else if (_value < 1)
            description += $"{_value * 100}% ";
        description += "증가시킵니다. ";

        return description;
    }
}