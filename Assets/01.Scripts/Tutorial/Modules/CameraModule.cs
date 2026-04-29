using System.Collections;
using UnityEngine;

/// <summary>
/// 튜토리얼 카메라 모듈
/// - 카메라 이동/줌 조건 대기
/// - 사용자의 카메라 조작 감지
/// </summary>
public class CameraModule : ITutorialModule
{
    private CameraModuleConfig _config;
    private bool _cameraMoved = false;
    private bool _cameraZoomed = false;

    public void Initialize(TutorialStep step)
    {
        _config = step.CameraConfig;
        _cameraMoved = false;
        _cameraZoomed = false;

        // 카메라 이벤트 구독
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<RightClickEvent>(OnCameraDragged);
            EventBus.Instance.Subscribe<ScrollEvent>(OnCameraZoomed);
        }
    }

    public IEnumerator Execute()
    {
        // 사용자가 카메라를 조작할 때까지 대기
        yield return new WaitUntil(() => _cameraMoved || _cameraZoomed);
    }

    public void Cleanup()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<RightClickEvent>(OnCameraDragged);
            EventBus.Instance.Unsubscribe<ScrollEvent>(OnCameraZoomed);
        }
    }

    private void OnCameraDragged(RightClickEvent evt)
    {
        _cameraMoved = true;
    }

    private void OnCameraZoomed(ScrollEvent evt)
    {
        _cameraZoomed = true;
    }
}
