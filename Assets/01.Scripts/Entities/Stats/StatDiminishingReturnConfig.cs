using UnityEngine;

[CreateAssetMenu(fileName = "StatDiminishingReturnConfig", menuName = "Data/Buff/Stat Diminishing Return Config")]
public class StatDiminishingReturnConfig : ScriptableObject
{
    [Header("Attack Damage")]
    [SerializeField, Min(0f)] private float _attackDamageMaxBonus = 1f;
    [SerializeField, Min(0f)] private float _attackDamageHalfPoint = 1f;

    [Header("Attack Speed")]
    [SerializeField, Min(0f)] private float _attackSpeedMaxBonus = 0.75f;
    [SerializeField, Min(0f)] private float _attackSpeedHalfPoint = 1f;

    public float AttackDamageMaxBonus => _attackDamageMaxBonus;
    public float AttackDamageHalfPoint => _attackDamageHalfPoint;
    public float AttackSpeedMaxBonus => _attackSpeedMaxBonus;
    public float AttackSpeedHalfPoint => _attackSpeedHalfPoint;

    private void OnValidate()
    {
        _attackDamageMaxBonus = Mathf.Max(0f, _attackDamageMaxBonus);
        _attackSpeedMaxBonus = Mathf.Max(0f, _attackSpeedMaxBonus);
        _attackDamageHalfPoint = Mathf.Max(_attackDamageMaxBonus, _attackDamageHalfPoint);
        _attackSpeedHalfPoint = Mathf.Max(_attackSpeedMaxBonus, _attackSpeedHalfPoint);
    }
}
