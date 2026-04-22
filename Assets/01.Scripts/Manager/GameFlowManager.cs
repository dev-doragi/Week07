//using UnityEngine;
//using System.Collections;
//public enum InGameState
//{
//    None,
//    Prepare,
//    WavePlaying,
//    WaveCleared,
//    StageCleared,
//    StageFailed
//}

//[DefaultExecutionOrder(-100)]
//public class GameFlowManager : Singleton<GameFlowManager>
//{
//    //private StageManager _stageManager => ManagerRegistry.TryGet(out StageManager sm) ? sm : StageManager.Instance;

//    public InGameState CurrentInGameState { get; private set; } = InGameState.None;

//    private void OnEnable()
//    {
//        if (EventBus.Instance == null) return;
//        EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGlobalStateChanged);
//        EventBus.Instance.Subscribe<StageLoadedEvent>(OnStageLoaded);
//        EventBus.Instance.Subscribe<WaveStartedEvent>(OnWaveStarted);
//        EventBus.Instance.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
//        EventBus.Instance.Subscribe<BaseDestroyedEvent>(OnBaseDestroyed);
//    }

//    private void OnDisable()
//    {
//        if (EventBus.Instance == null) return;
//        EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGlobalStateChanged);
//        EventBus.Instance.Unsubscribe<StageLoadedEvent>(OnStageLoaded);
//        EventBus.Instance.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
//        EventBus.Instance.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
//        EventBus.Instance.Unsubscribe<BaseDestroyedEvent>(OnBaseDestroyed);
//    }

//    private void OnGlobalStateChanged(GameStateChangedEvent evt)
//    {
//        if (evt.NewState == GameState.GameOver || evt.NewState == GameState.Ready || evt.NewState == GameState.GameClear)
//            ChangeFlowState(InGameState.None);
//    }

//    private void OnStageLoaded(StageLoadedEvent evt) => ChangeFlowState(InGameState.Prepare);

//    private void OnWaveStarted(WaveStartedEvent evt)
//    {
//        // 튜토리얼 모드에서는 웨이브 시작 이벤트가 전역 흐름 상태를 변경하지 않도록 함
//        //if (StageLoadContext.IsTutorial) return;
//        ChangeFlowState(InGameState.WavePlaying);
//    }

//    private void OnEnemyDefeated(EnemyDefeatedEvent evt) => CheckWinLossCondition(false);
//    private void OnBaseDestroyed(BaseDestroyedEvent evt) => CheckWinLossCondition(true);

//    public void ChangeFlowState(InGameState newState)
//    {
//        if (CurrentInGameState == newState) return;
//        if (newState != InGameState.None && GameManager.Instance.CurrentState != GameState.Playing) return;

//        InGameState previousState = CurrentInGameState;
//        CurrentInGameState = newState;

//        Debug.Log($"[GameFlowManager] {previousState} -> {CurrentInGameState}");

//        EventBus.Instance.Publish(new InGameStateChangedEvent { NewState = CurrentInGameState });
//        _stageManager?.UpdateState(CurrentInGameState);

//        ProcessStateLogic(newState);
//    }

//    private void ProcessStateLogic(InGameState state)
//    {
//        switch (state)
//        {
//            case InGameState.Prepare:
//                //_stageManager?.PlayWave(); // 웨이브 시작 처리는 버튼으로?
//                break;
//            case InGameState.WaveCleared:
//                if (_stageManager.CurrentWaveIndex >= _stageManager.CurrentStageData.Waves.Count - 1)
//                    StartCoroutine(SlowMotionTransitionRoutine(GameState.GameClear));
//                else
//                    StartCoroutine(NextWaveRoutine());
//                break;

//            case InGameState.StageFailed:
//                StartCoroutine(SlowMotionTransitionRoutine(GameState.GameOver));
//                break;

//            //case InGameState.StageCleared:
//            //    // 발행 단계에서도 튜토리얼 모드인 경우 실제 클리어 이벤트를 전파하지 않음
//            //    if (!StageLoadContext.IsTutorial)
//            //    {
//            //        EventBus.Instance.Publish(new StageClearedEvent { StageIndex = _stageManager.CurrentStageIndex });
//            //    }
//            //    break;
//        }
//    }

//    private IEnumerator NextWaveRoutine()
//    {
//        int nextIndex = _stageManager.CurrentWaveIndex + 1;
//        float timer = _stageManager.CurrentStageData.Waves[nextIndex].WaveInterval;

//        while (timer > 0)
//        {
//            timer -= Time.unscaledDeltaTime;
//            EventBus.Instance.Publish(new WaveCountdownEvent { RemainingTime = timer, IsActive = true });
//            yield return null;
//        }

//        EventBus.Instance.Publish(new WaveCountdownEvent { IsActive = false });
//        _stageManager.GoToNextWave();
//        _stageManager.PlayWave();
//    }

//    private IEnumerator SlowMotionTransitionRoutine(GameState targetGlobalState)
//    {
//        Time.timeScale = 0.3f;
//        yield return new WaitForSecondsRealtime(1.5f);
//        Time.timeScale = 1f;

//        if (targetGlobalState == GameState.GameClear)
//        {
//            ChangeFlowState(InGameState.StageCleared);
//        }
//        else
//        {
//            EventBus.Instance.Publish(new StageFailedEvent { StageIndex = _stageManager.CurrentStageIndex });
//        }
//    }

//    public void CheckWinLossCondition(bool isBaseDestroyed)
//    {
//        // 튜토리얼 모드에서는 전역 흐름(웨이브 클리어/스테이지 실패)을 변경하지 않음
//        if (StageLoadContext.IsTutorial) return;

//        if (CurrentInGameState != InGameState.WavePlaying) return;

//        // 적이 하나뿐이므로, 어떤 이벤트가 먼저 들어오느냐에 따라 바로 승패 결정
//        ChangeFlowState(isBaseDestroyed ? InGameState.StageFailed : InGameState.WaveCleared);
//    }
//}