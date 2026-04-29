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
// [튜토리얼 전용]
// ============================================================================
public struct PartPlacedEvent { public int PartKey; public UnityEngine.Vector2Int GridPos; public bool costUp; }
public struct AttackPlacementTutorialRequestedEvent { public int PartKey; }
public struct AttackPlacementTutorialEndedEvent { }
public struct TutorialCompletedEvent { public int RewardStageIndex; }
public struct TutorialEnemyDefeatedEvent { }
