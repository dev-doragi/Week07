// 전역 및 흐름 상태 이벤트
using UnityEngine;

public struct GameStateChangedEvent { public GameState NewState; }
public struct InGameStateChangedEvent { public InGameState NewState; }

// 스테이지 및 전투 흐름 이벤트 (그리드 방식에 맞춤)
public struct StageLoadedEvent { public int StageIndex; }
public struct StageGenerateCompleteEvent { } // 그리드 및 유닛 배치가 끝났을 때 발행
public struct WaveStartedEvent { public int WaveIndex; } // 배치 완료 후 유저가 '전투 시작'을 눌렀을 때 발행
public struct WaveEndedEvent { public bool IsWin; } // 전투 승패가 결정되었을 때 발행
public struct StageClearedEvent { public int StageIndex; public bool IsFinalStage; }
public struct StageFailedEvent { public int StageIndex; }
// 스테이지 진행도 관련 이벤트
// - 발행 시점: 저장된 클리어/해금 정보가 변경되었거나 로드되었을 때 발행
// - 처리 방법: UI(스테이지 버튼 등)는 이 이벤트를 수신하여 ProgressManager에서 현재 HighestCleared를 조회하고
//   버튼 상태를 갱신합니다. 예: StageButtonUnlocker는 이 이벤트를 받아 ApplyLockState()를 호출하여
//   버튼의 interactable 과 LockedOverlay를 갱신합니다.
public struct StageProgressUpdatedEvent { public int HighestCleared; }

// 튜토리얼 완료 이벤트
// - 발행 시점: 플레이어가 튜토리얼을 완료하고 보상을 수령했을 때 발행
// - 처리 방법: ProgressManager는 이 이벤트를 받아 튜토리얼 플래그를 설정하거나 보상으로 주어진 스테이지를 해금합니다.
public struct TutorialCompletedEvent { public int RewardStageIndex; }

public struct PlaySFXEvent
{
    public AudioClip Clip;
    public float Volume;
}

public struct PausePressedEvent { }