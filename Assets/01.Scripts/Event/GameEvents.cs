using UnityEngine;

// ============================================================================
// [스테이지/웨이브/진행]
// ============================================================================
public struct StageLoadedEvent { public int StageIndex; }
public struct StageGenerateCompleteEvent { }
public struct WaveStartedEvent { public int StageIndex; public int WaveIndex; }
public struct WaveEndedEvent { public int StageIndex; public int WaveIndex; public bool IsWin; }
public struct StageClearedEvent { public int StageIndex; public bool IsFinalStage; }
public struct StageFailedEvent { public int StageIndex; }
public struct StageCleanedUpEvent { public int StageIndex; }
public struct StageProgressUpdatedEvent { public int HighestCleared; }
public struct WaveWaitTimerTickEvent { public float RemainingTime; }
public struct WaveWaitInterruptedEvent { }

// ============================================================================
// [게임/인게임 상태]
// ============================================================================
public struct GameStateChangedEvent { public GameState NewState; }
public struct InGameStateChangedEvent { public InGameState NewState; }

// ============================================================================
// [전투/판정/코어/그리드]
// ============================================================================
public struct CoreDestroyedEvent { public bool IsPlayerBase; }
public struct SiegeCollisionResolvedEvent {
    public float PlayerCP;
    public float EnemyCP;
    public float Delta;
    public bool IsPlayerLosing;
    public float FinalDamage;
}
public struct EnemyHitEvent { public TeamType AttackerTeam; }
public struct CollisionPowerUpdatedEvent { public float PlayerCP; public float EnemyCP; }
public struct PlayerGridChangedEvent { }
public struct EnemyGridChangedEvent { }
public struct EnemyDefeatedEvent { }
public struct PartPlacedEvent { public int PartKey; public UnityEngine.Vector2Int GridPos; public bool costUp; }
public struct TutorialProductionFacilityPlacedEvent { public int PartKey; public UnityEngine.Vector2Int GridPos; }
public struct TutorialDefenseUnitPlacedEvent { public int PartKey; public UnityEngine.Vector2Int GridPos; }
public struct TutorialAttackUnitPlacedEvent { public int PartKey; public UnityEngine.Vector2Int GridPos; }

// ============================================================================
// [오디오/이펙트]
// ============================================================================
public struct PlaySFXEvent { public AudioClip Clip; public float Volume; }

// ============================================================================
// [스킬/의식]
// ============================================================================
public struct RitualCostChangedEvnet { }

// ============================================================================
// [입력/조작]
// ============================================================================
public struct ClickEvent { public bool IsStarted; }
public struct RightClickEvent { public bool IsStarted; }
public struct RotateEvent { }
public struct ScrollEvent { public float Delta; }
public struct PausePressedEvent { }

// ============================================================================
// [해금/언락]
// ============================================================================
public struct RatUnlockedEvent { public string RatId; }
public struct RitualUnlockedEvent { public string RitualId; }
public struct FeatureUnlockedEvent { public string UnlockId; }

// ============================================================================
// [튜토리얼 전용 이벤트]
// ============================================================================

/// <summary>
/// 튜토리얼 스텝이 시작될 때 발행 (TutorialManager -> Presenter)
/// </summary>
public struct TutorialStepStartedEvent
{
    public int StepIndex;
    public int TotalStepCount;
    public TutorialStep StepData; // UI 생성을 위한 스텝 원본 데이터 전달
}

/// <summary>
/// 튜토리얼 스텝 진행도가 변경될 때 발행 (TutorialManager -> Presenter)
/// </summary>
public struct TutorialProgressUpdatedEvent
{
    public float CurrentProgress;
    public float RequiredProgress;
    public string Label; // "공격 유닛" 등의 UI 표시 텍스트
}

/// <summary>
/// 튜토리얼 스텝이 완료되었을 때 발행 (TutorialManager -> Presenter)
/// </summary>
public struct TutorialStepCompletedEvent
{
    public int StepIndex;
}

/// <summary>
/// 플레이어가 튜토리얼 화면을 클릭/스페이스바를 눌렀을 때 Presenter → Manager 방향으로 발행
/// (순환 참조 제거 및 단방향 이벤트 구조 확립)
/// </summary>
public struct TutorialNextRequestedEvent { }

// (기존 이벤트들 유지)
public struct TutorialEnemyDefeatedEvent { }
public struct UnitDeployRequestedEvent { public int PartKey; }
public struct UnitDeployEndedEvent { }
public struct TutorialCompletedEvent { public int RewardStageIndex; }
public struct TutorialAccelerationButtonUsedEvent { }
public struct CameraManipulationEvent { }
public struct TutorialSkillUsedEvent { public int SkillIndex; }
