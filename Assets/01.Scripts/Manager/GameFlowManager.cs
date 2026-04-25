using UnityEngine;
using System.Collections;

[DefaultExecutionOrder(-150)]
public class GameFlowManager : Singleton<GameFlowManager>
{
    [Header("Wave Wait Settings")]
    [SerializeField] private float _defaultWaveWaitDuration = 15f;

    private Coroutine _waveWaitCoroutine;
    private Coroutine _transitionCoroutine;
    private int _pendingWaveIndex = -1;

    public InGameState CurrentInGameState { get; private set; } = InGameState.None;
    public float CurrentWaveWaitRemainingTime { get; private set; } = 0f;
    public bool IsWaitingForNextWave => _waveWaitCoroutine != null;
    public float DefaultWaveWaitDuration => _defaultWaveWaitDuration;

    protected override void OnBootstrap()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Ready)
        {
            ChangeFlowState(InGameState.None);
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGlobalStateChanged);
            EventBus.Instance.Subscribe<StageGenerateCompleteEvent>(OnStageGenerateComplete);
            EventBus.Instance.Subscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Instance.Subscribe<WaveEndedEvent>(OnWaveEnded);
        }

#if UNITY_EDITOR
        // 에디터에서 인게임씬 단독 실행 시 자동 세팅
        if (!Application.isPlaying || !UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name.Contains("InGame")) return;
        if (!StageLoadContext.HasValue)
        {
            Debug.LogWarning("[GameFlowManager] 에디터 단독 인게임씬 실행 감지: 0번 스테이지 자동 로드 및 상태 강제 세팅");
            StageLoadContext.SetStageIndex(0);
            if (StageManager.Instance != null)
                StageManager.Instance.LoadStage(0);
            if (GameManager.Instance != null)
                GameManager.Instance.ChangeState(GameState.Playing);
            ChangeFlowState(InGameState.Prepare);
        }
#endif
    }

    private void OnDisable()
    {
        StopAllFlowCoroutines(resetTimeScale: true);

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
        if (evt.NewState == GameState.Ready || evt.NewState == GameState.GameOver || evt.NewState == GameState.GameClear)
        {
            StopAllFlowCoroutines(resetTimeScale: false);
            ChangeFlowState(InGameState.None);
        }
    }

    private void OnStageGenerateComplete(StageGenerateCompleteEvent evt)
    {
        StopWaveWaitRoutine(publishInterruptedEvent: false);
        StopTransitionRoutine(resetTimeScale: false);

        ChangeFlowState(InGameState.Prepare);

        if (StageManager.Instance == null)
        {
            Debug.LogError("[GameFlowManager] StageManager가 없어 인게임 Prepare 상태를 유지할 수 없습니다.");
            return;
        }

        CurrentWaveWaitRemainingTime = 0f;
        _pendingWaveIndex = -1;
    }

    private void OnWaveStarted(WaveStartedEvent evt)
    {
        StopWaveWaitRoutine(publishInterruptedEvent: false);
        ChangeFlowState(InGameState.WavePlaying);
    }

    private void OnWaveEnded(WaveEndedEvent evt)
    {
        if (CurrentInGameState != InGameState.WavePlaying)
        {
            return;
        }

        ChangeFlowState(InGameState.WaveEnded);

        if (!evt.IsWin)
        {
            StartTransitionRoutine(evt.IsWin);
            return;
        }

        if (IsLastWave())
        {
            StartTransitionRoutine(evt.IsWin);
            return;
        }

        if (StageManager.Instance == null)
        {
            Debug.LogError("[GameFlowManager] StageManager가 없어 다음 웨이브 대기를 시작할 수 없습니다.");
            return;
        }

        float waitDuration = GetNextWaveWaitDuration();
        ChangeFlowState(InGameState.Prepare);
        StartWaveWait(waitDuration, StageManager.Instance.CurrentWaveIndex + 1);
    }

    private float GetNextWaveWaitDuration()
    {
        if (StageManager.Instance == null || StageManager.Instance.CurrentStageData == null)
            return 1f;

        int currentWaveIndex = StageManager.Instance.CurrentWaveIndex;
        var waves = StageManager.Instance.CurrentStageData.Waves;
        if (currentWaveIndex < 0 || currentWaveIndex >= waves.Count)
            return 1f;

        // 최소 1초 보장
        return Mathf.Max(1f, waves[currentWaveIndex].NextWaveInterval);
    }

    public void RequestImmediateNextWaveStart()
    {
        if (!IsWaitingForNextWave)
        {
            Debug.LogWarning("[GameFlowManager] 현재 즉시 시작할 웨이브 대기 상태가 아닙니다.");
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("[GameFlowManager] GameManager가 없어 즉시 시작 요청을 처리할 수 없습니다.");
            return;
        }

        if (GameManager.Instance.CurrentState != GameState.Playing)
        {
            Debug.LogWarning("[GameFlowManager] 게임이 Playing 상태가 아니어서 즉시 시작 요청을 무시합니다.");
            return;
        }

        if (StageManager.Instance == null)
        {
            Debug.LogError("[GameFlowManager] StageManager가 없어 즉시 시작 요청을 처리할 수 없습니다.");
            return;
        }

        int nextWaveIndex = _pendingWaveIndex;

        StopWaveWaitRoutine(publishInterruptedEvent: true);
        StartPendingWave(nextWaveIndex);
    }

    private void StartWaveWait(float duration, int nextWaveIndex)
    {
        StopWaveWaitRoutine(publishInterruptedEvent: false);

        if (StageManager.Instance == null)
        {
            Debug.LogError("[GameFlowManager] StageManager가 없어 웨이브 대기를 시작할 수 없습니다.");
            return;
        }

        _pendingWaveIndex = nextWaveIndex;
        CurrentWaveWaitRemainingTime = Mathf.Max(0f, duration);

        if (CurrentWaveWaitRemainingTime <= 0f)
        {
            PublishWaveWaitTick(0f);
            StartPendingWave(_pendingWaveIndex);
            return;
        }

        _waveWaitCoroutine = StartCoroutine(WaveWaitRoutine(CurrentWaveWaitRemainingTime));
    }

    private IEnumerator WaveWaitRoutine(float duration)
    {
        float remainingTime = Mathf.Max(0f, duration);
        int lastReportedSecond = Mathf.CeilToInt(remainingTime);

        PublishWaveWaitTick(remainingTime);

        while (remainingTime > 0f)
        {
            if (!isActiveAndEnabled)
            {
                StopWaveWaitRoutine(publishInterruptedEvent: false);
                yield break;
            }

            if (GameManager.Instance == null)
            {
                Debug.LogError("[GameFlowManager] GameManager가 없어 웨이브 대기를 중단합니다.");
                StopWaveWaitRoutine(publishInterruptedEvent: false);
                yield break;
            }

            if (StageManager.Instance == null)
            {
                Debug.LogError("[GameFlowManager] StageManager가 없어 웨이브 대기를 중단합니다.");
                StopWaveWaitRoutine(publishInterruptedEvent: false);
                yield break;
            }

            if (CurrentInGameState != InGameState.Prepare)
            {
                StopWaveWaitRoutine(publishInterruptedEvent: false);
                yield break;
            }

            if (GameManager.Instance.CurrentState != GameState.Playing)
            {
                yield return null;
                continue;
            }

            remainingTime -= Time.unscaledDeltaTime;
            CurrentWaveWaitRemainingTime = Mathf.Max(0f, remainingTime);

            int currentSecond = Mathf.CeilToInt(CurrentWaveWaitRemainingTime);
            if (currentSecond != lastReportedSecond)
            {
                lastReportedSecond = currentSecond;
                PublishWaveWaitTick(CurrentWaveWaitRemainingTime);
            }

            yield return null;
        }

        int nextWaveIndex = _pendingWaveIndex;
        StopWaveWaitRoutine(publishInterruptedEvent: false);
        PublishWaveWaitTick(0f);
        StartPendingWave(nextWaveIndex);
    }

    private void StartPendingWave(int waveIndex)
    {
        if (StageManager.Instance == null)
        {
            Debug.LogError("[GameFlowManager] StageManager가 없어 다음 웨이브를 시작할 수 없습니다.");
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("[GameFlowManager] GameManager가 없어 다음 웨이브를 시작할 수 없습니다.");
            return;
        }

        if (GameManager.Instance.CurrentState != GameState.Playing)
        {
            Debug.LogWarning("[GameFlowManager] 게임이 Playing 상태가 아니어서 다음 웨이브를 시작하지 않습니다.");
            return;
        }

        if (CurrentInGameState != InGameState.Prepare)
        {
            Debug.LogWarning("[GameFlowManager] Prepare 상태가 아니어서 다음 웨이브를 시작하지 않습니다.");
            return;
        }

        StageManager.Instance.StartWave(waveIndex);
    }

    private void StartTransitionRoutine(bool isWin)
    {
        StopTransitionRoutine(resetTimeScale: false);
        _transitionCoroutine = StartCoroutine(SlowMotionTransitionRoutine(isWin));
    }

    private void StopWaveWaitRoutine(bool publishInterruptedEvent)
    {
        if (_waveWaitCoroutine != null)
        {
            StopCoroutine(_waveWaitCoroutine);
            _waveWaitCoroutine = null;
        }

        bool hadPendingWave = _pendingWaveIndex >= 0;

        CurrentWaveWaitRemainingTime = 0f;
        _pendingWaveIndex = -1;

        if (publishInterruptedEvent && hadPendingWave && EventBus.Instance != null)
        {
            EventBus.Instance.Publish(new WaveWaitInterruptedEvent());
            EventBus.Instance.Publish(new WaveWaitTimerTickEvent { RemainingTime = 0f });
        }
    }

    private void StopTransitionRoutine(bool resetTimeScale)
    {
        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
        }

        if (resetTimeScale)
        {
            Time.timeScale = 1f;
        }
    }

    private void StopAllFlowCoroutines(bool resetTimeScale)
    {
        StopWaveWaitRoutine(publishInterruptedEvent: false);
        StopTransitionRoutine(resetTimeScale);
    }

    private bool IsLastWave()
    {
        if (StageManager.Instance == null || StageManager.Instance.CurrentStageData == null)
        {
            return true;
        }

        return StageManager.Instance.CurrentWaveIndex >= StageManager.Instance.CurrentStageData.Waves.Count - 1;
    }

    private void ChangeFlowState(InGameState newState)
    {
        if (CurrentInGameState == newState)
        {
            return;
        }

        if (newState != InGameState.None)
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[GameFlowManager] GameManager가 없어 인게임 상태를 변경할 수 없습니다.");
                return;
            }

            if (GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }
        }

        InGameState previousState = CurrentInGameState;
        CurrentInGameState = newState;

        Debug.Log($"[GameFlowManager] Flow State: {previousState} -> {CurrentInGameState}");

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish(new InGameStateChangedEvent { NewState = CurrentInGameState });
        }
    }

    private void PublishWaveWaitTick(float remainingTime)
    {
        CurrentWaveWaitRemainingTime = Mathf.Max(0f, remainingTime);

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish(new WaveWaitTimerTickEvent
            {
                RemainingTime = CurrentWaveWaitRemainingTime
            });
        }
    }

    private IEnumerator SlowMotionTransitionRoutine(bool isWin)
    {
        Time.timeScale = 0.3f;
        yield return new WaitForSecondsRealtime(1.5f);
        Time.timeScale = 1f;

        _transitionCoroutine = null;

        if (isWin)
        {
            ChangeFlowState(InGameState.StageCleared);

            if (StageManager.Instance == null)
            {
                Debug.LogError("[GameFlowManager] StageManager 인스턴스가 없어 StageClearedEvent를 발행할 수 없습니다.");
                yield break;
            }

            if (EventBus.Instance != null)
            {
                EventBus.Instance.Publish(new StageClearedEvent
                {
                    StageIndex = StageManager.Instance.CurrentStageIndex,
                    IsFinalStage = StageManager.Instance.IsFinalStage
                });
            }
        }
        else
        {
            ChangeFlowState(InGameState.StageFailed);

            if (StageManager.Instance == null)
            {
                Debug.LogError("[GameFlowManager] StageManager 인스턴스가 없어 StageFailedEvent를 발행할 수 없습니다.");
                yield break;
            }

            if (EventBus.Instance != null)
            {
                EventBus.Instance.Publish(new StageFailedEvent
                {
                    StageIndex = StageManager.Instance.CurrentStageIndex
                });
            }
        }
    }
}
