using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================================
// [1] 전역 및 시스템 이벤트 (Global & System)
// 게임의 전체 상태 전환 및 공용 유틸리티 제어
// ============================================================================

/// <summary> 전역 게임 상태(GameState) 변경 이벤트 </summary>
/// <remarks> [발행지] GameManager.ChangeState </remarks>
public struct GameStateChangedEvent { public GameState NewState; }

/// <summary> 일시정지 토글 입력 발생 이벤트 </summary>
/// <remarks> [발행지] InputManager 또는 Pause 버튼 UI </remarks>
public struct PausePressedEvent { }

/// <summary> 효과음(SFX) 재생 요청 이벤트 </summary>
/// <remarks> [발행지] 모든 시스템 (UI 클릭, 스킬 발사, 피격 등) </remarks>
public struct PlaySFXEvent
{
    public AudioClip Clip;
    public float Volume;
}

// ============================================================================
// [2] 스테이지 준비 이벤트 (Stage Setup)
// 씬 로드 후 실제 플레이 환경이 구축되는 단계
// ============================================================================

/// <summary> 스테이지 데이터 로드 시작 이벤트 </summary>
/// <remarks> [발행지] StageManager.LoadStage </remarks>
public struct StageLoadedEvent { public int StageIndex; }

/// <summary> 맵 타일 및 오브젝트 배치 완료 이벤트 </summary>
/// <remarks> [발행지] StageManager.LoadStage (배치 로직 종료 시점) </remarks>
public struct StageGenerateCompleteEvent { }


// ============================================================================
// [3] 인게임 전투 흐름 이벤트 (In-Game Flow)
// 실제 플레이 중 발생하는 웨이브 및 전투 상황 제어
// ============================================================================

/// <summary> 인게임 세부 흐름(InGameState) 변경 이벤트 </summary>
/// <remarks> [발행지] GameFlowManager.ChangeFlowState </remarks>
public struct InGameStateChangedEvent { public InGameState NewState; }

/// <summary> 특정 웨이브 시작 이벤트 </summary>
/// <remarks> [발행지] StageManager.StartWave </remarks>
public struct WaveStartedEvent { public int WaveIndex; }

/// <summary> 특정 웨이브 종료 이벤트 (승패 여부 포함) </summary>
/// <remarks> [발행지] 전투 시스템 또는 StageManager.EndWave </remarks>
public struct WaveEndedEvent { public bool IsWin; }

/// <summary> 아군/적군 코어 파괴 이벤트 </summary>
/// <remarks> [발행지] Core 또는 Base 클래스 (전투 판정 트리거) </remarks>
public struct CoreDestroyedEvent
{
    public bool IsPlayerBase;
}


// ============================================================================
// [4] 결과 및 데이터 갱신 이벤트 (Result & Progress)
// 전투 종료 후의 승패 판정, 저장 및 진행도 관리
// ============================================================================

/// <summary> 스테이지 최종 승리(클리어) 이벤트 </summary>
/// <remarks> [발행지] GameFlowManager </remarks>
public struct StageClearedEvent { public int StageIndex; public bool IsFinalStage; }

/// <summary> 스테이지 최종 패배(게임오버) 이벤트 </summary>
/// <remarks> [발행지] GameFlowManager </remarks>
public struct StageFailedEvent { public int StageIndex; }

/// <summary> 클리어 정보 저장 및 갱신 완료 이벤트 </summary>
/// <remarks> [발행지] ProgressManager </remarks>
public struct StageProgressUpdatedEvent { public int HighestCleared; }

/// <summary> 튜토리얼 전체 시퀀스 완료 이벤트 </summary>
/// <remarks> [발행지] TutorialManager </remarks>
public struct TutorialCompletedEvent { public int RewardStageIndex; }

/// <summary>
/// 스테이지 오브젝트 정리(삭제) 완료 시 발행
/// [발행 위치] StageManager.ClearCurrentStage()
/// [구독자] 정리 작업이 필요한 시스템 (파티클, 오브젝트 풀 등)
/// </summary>
public struct StageCleanedUpEvent { public int StageIndex; }