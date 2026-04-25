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