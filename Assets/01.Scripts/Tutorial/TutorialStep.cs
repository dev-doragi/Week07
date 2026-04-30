using UnityEngine;

/// <summary>
/// 실습(튜토리얼) 완료 조건 종류
/// </summary>
public enum TutorialCondition
{
    None,
    CameraMove,
    PartPlacement,
    EnemyDefeated,
    InteractionTriggered
}

/// <summary>
/// 하나의 튜토리얼 단계를 정의하는 데이터 클래스 (인스펙터 노출용)
/// </summary>
[System.Serializable]
public class TutorialStep
{
    [Header("Step Info")]
    public string StepName = "Step";

    [Header("Module Configurations")]
    public UIStateModuleConfig UIStateConfig;
    public PauseModuleConfig PauseConfig;
    public DialogueModuleConfig DialogueConfig;
    public QuestModuleConfig QuestConfig;
    public PlacementModuleConfig PlacementConfig;
    public CameraModuleConfig CameraConfig;
    public EnemySpawnModuleConfig EnemySpawnConfig;
    public EnemyDefeatedModuleConfig EnemyDefeatedConfig;
    public InteractionModuleConfig InteractionConfig;
    public HighlightModuleConfig HighlightConfig;
    public AutoAdvanceModuleConfig AutoAdvanceConfig;

    /// <summary>
    /// 이 스텝에서 어떤 조건을 사용할지 결정
    /// </summary>
    [Header("Condition Type")]
    public TutorialCondition Condition = TutorialCondition.None;

    public bool HasCompletionCondition => Condition != TutorialCondition.None;

    public TutorialStep()
    {
        UIStateConfig = new UIStateModuleConfig();
        PauseConfig = new PauseModuleConfig();
        DialogueConfig = new DialogueModuleConfig();
        QuestConfig = new QuestModuleConfig();
        PlacementConfig = new PlacementModuleConfig();
        CameraConfig = new CameraModuleConfig();
        EnemySpawnConfig = new EnemySpawnModuleConfig();
        EnemyDefeatedConfig = new EnemyDefeatedModuleConfig();
        InteractionConfig = new InteractionModuleConfig();
        HighlightConfig = new HighlightModuleConfig();
        AutoAdvanceConfig = new AutoAdvanceModuleConfig();
    }
}