using UnityEngine;

// ============================================================================
// [스테이지 및 웨이브 흐름 이벤트]
// ============================================================================
public struct StageLoadedEvent { public int StageIndex; }
public struct StageGenerateCompleteEvent { }
public struct WaveStartedEvent { public int StageIndex; public int WaveIndex; }
public struct WaveClearedEvent { public int StageIndex; public int WaveIndex; }
public struct WaveEndedEvent { public int StageIndex; public int WaveIndex; public bool IsWin; }
public struct StageClearedEvent { public int StageIndex; public bool IsFinalStage; }
public struct StageFailedEvent { public int StageIndex; }
public struct StageCleanedUpEvent { public int StageIndex; }
public struct StageProgressUpdatedEvent { public int HighestCleared; }
public struct WaveWaitTimerTickEvent { public float RemainingTime; }
public struct WaveWaitInterruptedEvent { }

// ============================================================================
// [게임 및 인게임 상태 전환 이벤트]
// ============================================================================
public struct GameStateChangedEvent { public GameState NewState; }
public struct InGameStateChangedEvent { public InGameState NewState; }

// ============================================================================
// [전투 및 승패 판정 트리거 이벤트]
// ============================================================================
public struct EnemyDefeatedEvent { }
public struct BaseDestroyedEvent { }
public struct CoreDestroyedEvent { public bool IsPlayerBase; }
public struct TutorialEnemyDefeatedEvent { }

// ============================================================================
// [오디오 및 이펙트 출력 이벤트]
// ============================================================================
public struct PlaySFXEvent { public AudioClip Clip; public float Volume; }

// ============================================================================
// [입력 및 조작 관련 이벤트]
// ============================================================================
public struct ClickEvent { public bool IsStarted; }
public struct RightClickEvent { public bool IsStarted; }
public struct RotateEvent { }
public struct ScrollEvent { public float Delta; }
public struct PausePressedEvent { }

// ============================================================================
// [웨이브/카운트다운/배치/튜토리얼 등 기타]
// ============================================================================
public struct WaveCountdownEvent { public float RemainingTime; public bool IsActive; }
public struct PartPlacedEvent { public int PartKey; public UnityEngine.Vector2Int GridPos; public bool costUp; }
public struct AttackPlacementTutorialRequestedEvent { public int PartKey; }
public struct AttackPlacementTutorialEndedEvent { }
public struct TutorialCompletedEvent { public int RewardStageIndex; }
public struct TutorialCameraManipulatedEvent { }
public struct TutorialProductionFacilityPlacedEvent { public int PartKey; public UnityEngine.Vector2Int GridPos; }
public struct TutorialAccelerationButtonUsedEvent { }
public struct TutorialDefenseUnitPlacedEvent { public int PartKey; public UnityEngine.Vector2Int GridPos; }
public struct TutorialSkillUsedEvent { public int SkillIndex; }
public struct TutorialAttackUnitPlacedEvent { public int PartKey; public UnityEngine.Vector2Int GridPos; }
public struct TutorialStepStartedEvent
{
    public int StepIndex;
    public int TotalStepCount;
}
public struct TutorialStepCompletedEvent
{
    public int StepIndex;
    public int TotalStepCount;
}

// ============================================================================
// [충돌 관련 이벤트]
// ============================================================================
/// <summary>양쪽 진영의 CollisionPower 수치가 갱신되었을 때 UI 등에 알립니다.</summary>
public struct CollisionPowerUpdatedEvent
{
    public float PlayerCP;
    public float EnemyCP;
}

/// <summary>플레이어 그리드에 유닛이 추가/제거되어 CP 재계산이 필요할 때 발행합니다.</summary>
public struct PlayerGridChangedEvent { }

/// <summary>적 그리드에 유닛이 추가/제거되어 CP 재계산이 필요할 때 발행합니다.</summary>
public struct EnemyGridChangedEvent { }

/// <summary>차지(돌진) 충돌 결과 데미지가 분배되었을 때 발행합니다.</summary>
public struct SiegeCollisionResolvedEvent
{
    public float PlayerCP;
    public float EnemyCP;
    public float Delta;
    public bool IsPlayerLosing;
}

/// <summary>투사체 또는 공격이 적에게 명중했을 때 발행합니다.</summary>
public struct EnemyHitEvent
{
    public TeamType AttackerTeam;
}

// ============================================================================
// [해금 이벤트]
// ============================================================================
public struct RatUnlockedEvent
{
    public string RatId;
}

public struct RitualUnlockedEvent
{
    public string RitualId;
}

public struct FeatureUnlockedEvent
{
    public string UnlockId;
}
