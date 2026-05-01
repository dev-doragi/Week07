using UnityEngine;

public enum HighlightTrackingMode
{
    StaticMarker = 0, // 기존 가짜 마커 방식
    FollowTarget = 1  // 실제 UI 추적 방식
}

[System.Serializable]
public class HighlightModuleConfig
{
    [Header("Highlight")]
    public RectTransform TargetUI;

    [Header("Tracking")]
    public HighlightTrackingMode TrackingMode = HighlightTrackingMode.StaticMarker;

    [Min(1f)]
    public float PulseScale = 1.1f;

    [Min(0.1f)]
    public float PulseSpeed = 4f;
}