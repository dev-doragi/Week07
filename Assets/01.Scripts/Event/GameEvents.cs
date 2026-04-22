//using UnityEngine;

//// ============================================================================
//// [스테이지 및 웨이브 흐름 이벤트]
//// 게임의 스테이지/웨이브 진행 상태를 알리는 이벤트들
//// ============================================================================

///// <summary>
///// 스테이지 데이터 로드 완료 시 발행
///// [발행 위치] StageManager.LoadStage()
///// [구독자] GameFlowManager (InGameState.Prepare 전환)
///// </summary>
//public struct StageLoadedEvent { public int StageIndex; }

///// <summary>
///// 웨이브 전투 시작 시 발행
///// [발행 위치] StageManager.PlayWave()
///// [구독자] GameFlowManager (InGameState.WavePlaying 전환)
///// </summary>
//public struct WaveStartedEvent { public int StageIndex; public int WaveIndex; }

///// <summary>
///// 단일 웨이브 클리어 시 발행 (다음 웨이브가 있을 수 있음)
///// [발행 위치] GameFlowManager.ProcessStateLogic()
///// [구독자] UI 시스템 (웨이브 완료 알림, 보상 표시 등)
///// </summary>
//public struct WaveClearedEvent { public int StageIndex; public int WaveIndex; }

///// <summary>
///// 해당 스테이지의 모든 웨이브 종료 시 발행 (승리)
///// [발행 위치] StageManager.NotifyStageCleared()
///// [구독자] VehicleSaveLoader (차량 자동 저장), UI (클리어 화면)
///// </summary>
//public struct StageClearedEvent { public int StageIndex; }

///// <summary>
///// 스테이지 파괴 시 발행 (패배)
///// [발행 위치] GameFlowManager.ProcessStateLogic()
///// [구독자] UI (패배 화면), SoundManager (게임오버 사운드)
///// </summary>
//public struct StageFailedEvent { public int StageIndex; }

///// <summary>
///// 스테이지 오브젝트 정리(삭제) 완료 시 발행
///// [발행 위치] StageManager.ClearCurrentStage()
///// [구독자] 정리 작업이 필요한 시스템 (파티클, 오브젝트 풀 등)
///// </summary>
//public struct StageCleanedUpEvent { public int StageIndex; }

//// ============================================================================
//// [게임 및 인게임 상태 전환 이벤트]
//// 게임의 상태 변화를 알리는 이벤트들
//// ============================================================================

///// <summary>
///// 전체 게임 상태 변경 시 발행 (Ready, Playing, Paused, GameOver, GameClear)
///// [발행 위치] GameManager.ChangeState()
///// [구독자] SoundManager (로비 BGM), UI (화면 전환)
///// </summary>
//public struct GameStateChangedEvent { public GameState NewState; }

///// <summary>
///// 인게임 세부 상태 변경 시 발행 (Prepare, WavePlaying, WaveCleared, StageCleared, StageFailed)
///// [발행 위치] GameFlowManager.ChangeFlowState()
///// [구독자] SoundManager (전투/준비 BGM 전환), UI (상태별 UI 표시)
///// </summary>
//public struct InGameStateChangedEvent { public InGameState NewState; }

//// ============================================================================
//// [전투 및 승패 판정 트리거 이벤트]
//// 전투 중 발생하는 주요 이벤트들 (타 파트에서 발행)
//// ============================================================================

///// <summary>
///// 적 유닛 사망 시 발행
///// [발행 위치] Rat.Die() 등 적 스크립트 (타 파트 작업 필요)
///// [구독자] GameFlowManager (웨이브 클리어 판정)
///// [사용법] EventBus.Instance.Publish(new EnemyDefeatedEvent());
///// </summary>
//public struct EnemyDefeatedEvent { }

///// <summary>
///// 아군 기지 파괴 시 발행
///// [발행 위치] GridBoard 또는 Base 스크립트 (타 파트 작업 필요)
///// [구독자] GameFlowManager (패배 판정)
///// [사용법] EventBus.Instance.Publish(new BaseDestroyedEvent());
///// </summary>
//public struct BaseDestroyedEvent { }

//// ============================================================================
//// [오디오 및 이펙트 출력 이벤트]
//// 사운드 재생 요청 이벤트
//// ============================================================================

///// <summary>
///// 효과음(SFX) 재생 요청 시 발행
///// [발행 위치] 타 파트 (유닛, 무기, UI 등)
///// [구독자] SoundManager (효과음 풀링 재생)
///// [사용법] EventBus.Instance.Publish(new PlaySFXEvent { Clip = audioClip, Volume = 1f });
///// </summary>
//public struct PlaySFXEvent
//{
//    public AudioClip Clip;
//    public float Volume;
//}

//// ============================================================================
//// [입력 및 조작 관련 이벤트]
//// InputReader에서 발행하는 마우스 및 키보드 입력 이벤트
//// ============================================================================

///// <summary>
///// 마우스 좌클릭 상태 변경 시 발행
///// [발행 위치] InputReader (Click Action)
///// [구독자] BuildManager (배치 시도), UI 시스템 (클릭 상호작용)
///// </summary>
//public struct ClickEvent
//{
//    public bool IsStarted; // true: 누름(Started), false: 뗌(Canceled)
//}

///// <summary>
///// 마우스 우클릭 상태 변경 시 발행
///// [발행 위치] InputReader (RightClick Action)
///// [구독자] CameraManager (드래그 시작/종료 판정)
///// </summary>
//public struct RightClickEvent
//{
//    public bool IsStarted; // true: 누름(Started), false: 뗌(Canceled)
//}

///// <summary>
///// 구조물 회전 키(R) 입력 시 발행
///// [발행 위치] InputReader (Rotate Action)
///// [구독자] BuildManager (설치 중인 오브젝트 회전)
///// </summary>
//public struct RotateEvent { }

///// <summary>
///// 마우스 휠 스크롤 발생 시 발행 (단발성 수치 전달)
///// [발행 위치] InputReader (Scroll Action)
///// [구독자] CameraManager (줌 인/아웃 처리)
///// [사용법] EventBus.Instance.Publish(new ScrollEvent { Delta = value });
///// </summary>
//public struct ScrollEvent
//{
//    public float Delta; // 휠 스크롤 변화량 (y값)
//}

//// 일시정지 이벤트
//public struct PausePressedEvent { }

//public struct WaveCountdownEvent
//{
//    public float RemainingTime;
//    public bool IsActive; // UI를 켜고 끄기 위한 플래그
//}

//public struct PartPlacedEvent
//{
//    public int PartKey;
//    public UnityEngine.Vector2Int GridPos;
//    public bool costUp;
//}

//// 튜토리얼: 공격 유닛 배치 설명 요청
//public struct AttackPlacementTutorialRequestedEvent
//{
//    public int PartKey;
//}

//// 튜토리얼 종료 알림
//public struct AttackPlacementTutorialEndedEvent { }

//public struct TutorialCompletedEvent
//{
//    public int RewardStageIndex; // 완료 후 진행할 스테이지
//}

//// GameEvents.cs 맨 아랫부분에 추가
//public struct TutorialEnemyDefeatedEvent { }