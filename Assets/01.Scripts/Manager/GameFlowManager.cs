using UnityEngine;
using System.Collections;

[DefaultExecutionOrder(-150)]
public class GameFlowManager : Singleton<GameFlowManager>
{
    public InGameState CurrentInGameState { get; private set; } = InGameState.None;

    protected override void OnBootstrap()
    {
        // OnBootstrap 시점에 현재 전역 게임 상태를 확인하고 필요하면 플로우 상태를 동기화합니다.
        if (GameManager.Instance != null)
        {
            // GameManager의 현재 상태가 Ready 또는 GameOver/ Clear인 경우 플로우를 None으로 유지.
            if (GameManager.Instance.CurrentState == GameState.Ready)
            {
                ChangeFlowState(InGameState.None);
            }
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGlobalStateChanged);
            EventBus.Instance.Subscribe<StageGenerateCompleteEvent>(OnStageGenerateComplete);
            EventBus.Instance.Subscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Instance.Subscribe<WaveEndedEvent>(OnWaveEnded);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGlobalStateChanged);
            EventBus.Instance.Unsubscribe<StageGenerateCompleteEvent>(OnStageGenerateComplete);
            EventBus.Instance.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Instance.Unsubscribe<WaveEndedEvent>(OnWaveEnded);
        }
    }

    private void OnGlobalStateChanged(GameStateChangedEvent evt)
    {
        if (evt.NewState == GameState.GameOver || evt.NewState == GameState.Ready || evt.NewState == GameState.GameClear)
        {
            ChangeFlowState(InGameState.None);
        }
    }

    private void OnStageGenerateComplete(StageGenerateCompleteEvent evt)
    {
        ChangeFlowState(InGameState.Prepare);

        if (StageManager.Instance == null)
        {
            Debug.LogError("[GameFlowManager] StageManager가 없어 첫 웨이브를 시작할 수 없습니다.");
            return;
        }
    }

    private void OnWaveStarted(WaveStartedEvent evt)
    {
        ChangeFlowState(InGameState.WavePlaying);
    }

    private void OnWaveEnded(WaveEndedEvent evt)
    {
        if (CurrentInGameState != InGameState.WavePlaying) return;

        ChangeFlowState(InGameState.WaveEnded);
        StartCoroutine(SlowMotionTransitionRoutine(evt.IsWin));
    }

    private bool IsLastWave()
    {
        if (StageManager.Instance == null || StageManager.Instance.CurrentStageData == null) return true;
        return StageManager.Instance.CurrentWaveIndex >= StageManager.Instance.CurrentStageData.Waves.Count - 1;
    }

    private void ChangeFlowState(InGameState newState)
    {
        if (CurrentInGameState == newState) return;
        if (newState != InGameState.None && GameManager.Instance.CurrentState != GameState.Playing) return;

        InGameState previousState = CurrentInGameState;
        CurrentInGameState = newState;

        Debug.Log($"[GameFlowManager] Flow State: {previousState} -> {CurrentInGameState}");

        EventBus.Instance.Publish(new InGameStateChangedEvent { NewState = CurrentInGameState });
    }

    private IEnumerator SlowMotionTransitionRoutine(bool isWin)
    {
        Time.timeScale = 0.3f;
        yield return new WaitForSecondsRealtime(1.5f);
        Time.timeScale = 1f;

        if (isWin)
        {
            // 마지막 웨이브가 아니면 다음 웨이브로 진행
            if (!IsLastWave())
            {
                ChangeFlowState(InGameState.Prepare);
                StageManager.Instance.StartNextWave();
                yield break;
            }

            // 마지막 웨이브 클리어 → 스테이지 클리어
            ChangeFlowState(InGameState.StageCleared);

            if (StageManager.Instance == null)
            {
                Debug.LogError("StageManager 인스턴스가 없어 StageClearedEvent를 발행할 수 없습니다.");
                yield break;
            }

            EventBus.Instance.Publish(new StageClearedEvent
            {
                StageIndex = StageManager.Instance.CurrentStageIndex,
                IsFinalStage = StageManager.Instance.IsFinalStage
            });
        }
        else
        {
            ChangeFlowState(InGameState.StageFailed);

            if (StageManager.Instance == null)
            {
                Debug.LogError("StageManager 인스턴스가 없어 StageFailedEvent를 발행할 수 없습니다.");
                yield break;
            }

            EventBus.Instance.Publish(new StageFailedEvent
            {
                StageIndex = StageManager.Instance.CurrentStageIndex
            });
        }
    }
}