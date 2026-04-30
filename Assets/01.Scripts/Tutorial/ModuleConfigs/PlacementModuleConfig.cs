using UnityEngine;

/// <summary>
/// 파트 배치 모듈 설정
/// </summary>
[System.Serializable]
public class PlacementModuleConfig
{
    [Header("Placement Condition")]
    public int RequiredAmount = 1;
    public RequiredPartGroup RequiredGroup = RequiredPartGroup.Any;
    public int[] RequiredPartKeys;

    [Header("UI Label")]
    public string PlacementLabel;
}
