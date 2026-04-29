using UnityEngine;

/// <summary>
/// 카메라 모듈 설정
/// </summary>
[System.Serializable]
public class CameraModuleConfig
{
    [Header("Camera Reset")]
    public bool ResetCameraAfterMove = true;
    public float ResetDuration = 1f;
}
