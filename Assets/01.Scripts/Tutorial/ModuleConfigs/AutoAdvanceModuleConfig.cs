using UnityEngine;

/// <summary>
/// 자동 진행/클릭 대기 모듈 설정
/// </summary>
[System.Serializable]
public class AutoAdvanceModuleConfig
{
    [Header("Auto Advance Settings")]
    [Tooltip("0 이하면 클릭 대기, 0 초과면 지정된 시간 후 자동 진행")]
    public float AutoAdvanceDelay = 3.0f;
}
