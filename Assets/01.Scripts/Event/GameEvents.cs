// 전역 및 인게임 흐름 이벤트 정의
using UnityEngine;

/// <summary>
/// 전역 게임 상태(GameState)가 변경되었음을 알리는 이벤트.
/// 발행: GameManager.ChangeState
/// 구독자 예: UIManager, SoundManager, PauseManager
/// </summary>
public struct GameStateChangedEvent { public GameState NewState; }

/// <summary>
/// 인게임 흐름 상태(InGameState)가 변경되었음을 알리는 이벤트.
/// 발행: GameFlowManager.ChangeFlowState
/// 구독자 예: UIManager, SoundManager
/// </summary>
public struct InGameStateChangedEvent { public InGameState NewState; }

// --------------------------------------------------
// 스테이지 및 전투 흐름 이벤트
// --------------------------------------------------

/// <summary>
/// 스테이지가 로드되었음을 알립니다. (StageManager.LoadStage에서 발행)
/// 구독자 예: VehicleSaveLoader(차량 복구), GameManager(게임 상태 변경)
/// </summary>
public struct StageLoadedEvent { public int StageIndex; }

/// <summary>
/// 스테이지 레이아웃 및 오브젝트 배치가 모두 완료되었음을 알립니다.
/// (StageManager.LoadStage가 배치 완료 후 발행)
/// 구독자 예: GameFlowManager (Prepare 상태 진입)
/// </summary>
public struct StageGenerateCompleteEvent { }

/// <summary>
/// 웨이브가 시작되었음을 알립니다. (StageManager.StartWave에서 발행)
/// 구독자 예: GameFlowManager, StageLayout(스폰 트리거)
/// </summary>
public struct WaveStartedEvent { public int WaveIndex; }

/// <summary>
/// 웨이브가 종료되었음을 알립니다. (StageManager.EndWave 또는 전투 시스템에서 발행)
/// IsWin 플래그로 승패를 전달합니다.
/// 구독자 예: GameFlowManager (연출/결과 처리)
/// </summary>
public struct WaveEndedEvent { public bool IsWin; }

/// <summary>
/// 스테이지가 클리어되었음을 알립니다. (GameFlowManager 발행)
/// IsFinalStage가 true면 전체 게임 클리어 처리로 이어집니다.
/// 구독자 예: ProgressManager(저장), GameManager(최종 클리어 상태 전환)
/// </summary>
public struct StageClearedEvent { public int StageIndex; public bool IsFinalStage; }

/// <summary>
/// 스테이지 실패(게임오버)를 알립니다. (GameFlowManager 발행)
/// 구독자 예: GameManager
/// </summary>
public struct StageFailedEvent { public int StageIndex; }

// --------------------------------------------------
// 진행도 / 튜토리얼 / 유틸 이벤트
// --------------------------------------------------

/// <summary>
/// ProgressManager가 현재 최고 클리어 정보를 저장/갱신한 후 발행합니다.
/// UI(스테이지 버튼)는 이 이벤트를 수신하여 버튼 상태를 갱신합니다.
/// </summary>
public struct StageProgressUpdatedEvent { public int HighestCleared; }

/// <summary>
/// 튜토리얼 완료 시 발행되는 이벤트. RewardStageIndex로 보상 해금 대상을 전달합니다.
/// 구독자 예: ProgressManager
/// </summary>
public struct TutorialCompletedEvent { public int RewardStageIndex; }

/// <summary>
/// SFX 재생 요청 이벤트. PlaySFXEvent를 발행하면 SoundManager가 효과음을 재생합니다.
/// </summary>
public struct PlaySFXEvent
{
    public AudioClip Clip;
    public float Volume;
}

/// <summary>
/// 일시정지 토글 입력(예: 키 입력 또는 UI)이 발생했음을 알리는 이벤트입니다.
/// 구독자 예: PauseManager
/// </summary>
public struct PausePressedEvent { }

// ============================================================================
// [전투 및 승패 판정 트리거 이벤트]
// 전투 중 발생하는 주요 이벤트들 (타 파트에서 발행)
// ============================================================================

/// <summary>
/// 코어 파괴 시 발행
/// [발행 위치] GridBoard 또는 Base 스크립트 (타 파트 작업 필요)
/// [구독자] GameFlowManager (패배 판정)
/// [사용법] EventBus.Instance.Publish(new CoreDestroyedEvent());
/// </summary>
public struct CoreDestroyedEvent 
{
    public bool IsPlayerBase;
}