using UnityEngine;

[System.Serializable]
public class HighlightModuleConfig
{
    [Header("Highlight")]
    public RectTransform TargetUI;

    [Min(0.1f)]
    public float PulseScale = 1.1f;

    [Min(0.1f)]
    public float PulseSpeed = 4f;
}