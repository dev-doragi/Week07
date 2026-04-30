using UnityEngine;

/// <summary>
/// 카메라 모듈 설정
/// </summary>
[System.Serializable]
public class CameraModuleConfig
{
    [Header("Camera 유지(리셋 방지)")]
    public bool KeepCameraSize = false;
    public float ResetDuration = 1f;
}
