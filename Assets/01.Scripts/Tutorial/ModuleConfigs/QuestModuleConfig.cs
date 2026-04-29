using UnityEngine;

/// <summary>
/// 목표/UI 강조 모듈 설정
/// </summary>
[System.Serializable]
public class QuestModuleConfig
{
    [Header("Quest UI")]
    public RectTransform TargetUI;
    public bool HighlightUI = true;
}
