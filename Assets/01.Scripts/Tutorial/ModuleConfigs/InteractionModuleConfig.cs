using UnityEngine;

/// <summary>
/// 튜토리얼 상호작용 조건 설정
/// </summary>
[System.Serializable]
public class InteractionModuleConfig
{
    [Header("Interaction")]
    public string InteractionId;

    [Min(1)]
    public int RequiredCount = 1;

    [Header("UI Label")]
    public string ProgressLabel = "상호작용";
}
