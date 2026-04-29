using UnityEngine;

/// <summary>
/// 일시정지/입력 제어 모듈 설정
/// </summary>
[System.Serializable]
public class PauseModuleConfig
{
    [Header("Pause Settings")]
    public bool ShouldPause = true;
    public bool BlockInput = true;
}
