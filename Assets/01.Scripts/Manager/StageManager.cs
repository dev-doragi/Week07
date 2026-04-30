using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-160)]
/// <summary>
/// 스테이지 데이터 로드, 환경(Grid/Layout) 생성 및 웨이브 진행을 총괄하는 매니저입니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - ScriptableObject(StageDataSO) 기반 스테이지 환경 및 유닛 프리팹 배치
/// - 현재 웨이브(Wave)의 시작과 끝을 통제하는 중앙 권한자
/// - WaveStartedEvent 수신 후 StageLayout에 적 스폰 위임
///
/// [이벤트 흐름]
/// - Publish: StageLoadedEvent, StageGenerateCompleteEvent, WaveStartedEvent, WaveEndedEvent
/// - Subscribe: WaveStartedEvent
/// </remarks>
public class StageManager : Singleton<StageManager>
{
    [Header("Stage Settings")]
    [SerializeField] private StageDataSO[] _stageDatas;
    [SerializeField] private Transform _stageContainer;

    [Header("Wave Start")]
    [Min(0f)]
    [Tooltip("All automatic wave waits use this value, including the first wave, map-selected waves, and next-wave waits.")]
    [SerializeField] private float _initialWaveStartDelay = 15f;

    [Header("Drop Rat Clear Reward")]
    [Min(0f)]
    [Tooltip("Clear times at or below this value grant the maximum DropRat resource reward.")]
    [SerializeField] private float _maxRewardClearTime = 20f;
    [Min(0f)]
    [Tooltip("Clear times at or above this value grant the minimum DropRat resource reward.")]
    [SerializeField] private float _minRewardClearTime = 60f;
    [Min(0)]
    [Tooltip("Total DropRat resource reward granted for the fastest clear time.")]
    [SerializeField] private int _maxDropRatReward = 300;
    [Min(0)]
    [Tooltip("Total DropRat resource reward granted for the slowest clear time.")]
    [SerializeField] private int _minDropRatReward = 50;

    private StageLayout _currentLayout;
    private bool _isWaveEnding;
    private Coroutine _mapWaveStartCoroutine;
    private int _pendingWaveStartIndex = -1;
    private float _waveStartRemainingTime;
    private float _waveStartTime;
    private float _lastWaveClearTime;

    public int CurrentStageIndex { get; private set; } = 0;
    public int CurrentWaveIndex { get; private set; } = 0;
    public StageLayout CurrentLayout => _currentLayout;
    public InGameState CurrentState { get; private set; } = InGameState.None;
    public StageDataSO CurrentStageData => _stageDatas != null && CurrentStageIndex >= 0 && CurrentStageIndex < _stageDatas.Length ? _stageDatas[CurrentStageIndex] : null;
    public bool IsFinalStage => _stageDatas != null && CurrentStageIndex >= _stageDatas.Length - 1;
    public float WaveStartDelay => Mathf.Max(0f, _initialWaveStartDelay);
    public bool IsWaitingForWaveStart => _mapWaveStartCoroutine != null;
    public int PendingWaveStartIndex => _pendingWaveStartIndex;
    public float WaveStartRemainingTime => Mathf.Max(0f, _waveStartRemainingTime);
    public float CurrentWaveClearTime => CurrentState == InGameState.WavePlaying ? GetCurrentWaveElapsedTime() : _lastWaveClearTime;

    protected override void OnBootstrap()
    {
        if (_stageContainer == null)
        {
            GameObject parentObj = new GameObject("StageContainer");
            _stageContainer = parentObj.transform;
        }

        if (StageLoadContext.HasValue && !StageLoadContext.IsTutorial)
        {
            int stageIndex = StageLoadContext.GetStageIndex();
            LoadStage(stageIndex);
            StartWaveAfterDelay(0, WaveStartDelay);
        }
    }

    private void OnEnable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Instance.Subscribe<CoreDestroyedEvent>(OnCoreDestroyed);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Instance.Unsubscribe<CoreDestroyedEvent>(OnCoreDestroyed);
        }
    }

    private void OnCoreDestroyed(CoreDestroyedEvent evt)
    {
        if (StageLoadContext.IsTutorial)
        {
            Debug.Log("[StageManager] 튜토리얼 중이므로 코어 파괴 시 스테이지 클리어를 무시합니다.");
            return;
        }

        // GameFlowManager가 없으면 EndWave를 호출하지 않음 (방어)
        if (GameFlowManager.Instance == null)
        {
            Debug.LogWarning("[StageManager] GameFlowManager.Instance가 null이므로 EndWave를 호출하지 않습니다.");
            return;
        }
        if (GameFlowManager.Instance.CurrentInGameState != InGameState.WavePlaying)
        {
            return;
        }

        EndWave(!evt.IsPlayerBase);
    }

    /// <summary>
    /// [EventBus] 웨이브 시작 시 StageLayout에 적 스폰을 위임합니다.
    /// </summary>
    private void OnWaveStarted(WaveStartedEvent evt)
    {
        Debug.Log($"[StageManager] Wave {evt.WaveIndex} 시작 - 적 스폰 중...");

        if (_currentLayout == null)
        {
            Debug.LogError("[StageManager] 현재 레이아웃이 없습니다!");
            return;
        }

        if (CurrentStageData == null)
        {
            Debug.LogError("[StageManager] CurrentStageData가 없습니다.");
            return;
        }

        int waveIndex = CurrentWaveIndex;
        if (waveIndex < 0 || waveIndex >= CurrentStageData.Waves.Count)
        {
            Debug.LogError($"[StageManager] 잘못된 웨이브 인덱스: {waveIndex}");
            return;
        }

        WaveData currentWave = CurrentStageData.Waves[waveIndex];
        _currentLayout.SpawnEnemy(currentWave);
    }

    public void LoadStage(int stageIndex)
    {
        if (_stageDatas == null || stageIndex < 0 || stageIndex >= _stageDatas.Length)
        {
            Debug.LogError($"[StageManager] 유효하지 않은 스테이지 인덱스: {stageIndex}");
            return;
        }

        ClearCurrentStage();
        CurrentStageIndex = stageIndex;
        StageDataSO nextData = _stageDatas[CurrentStageIndex];

        if (nextData.StageLayoutPrefab != null)
        {
            _currentLayout = Instantiate(nextData.StageLayoutPrefab, _stageContainer);
            // TODO: 여기서 그리드를 초기화하고 SO 데이터를 기반으로 몬스터나 환경 프리팹을 배치합니다.
        }

        EventBus.Instance.Publish(new StageLoadedEvent { StageIndex = CurrentStageIndex });

        StartCoroutine(PublishStageGenerateCompleteAfterDelay());

        Debug.Log($"[StageManager] Stage {stageIndex} 로드 준비 완료.");
    }

    public void LoadNextStage()
    {
        if (IsFinalStage)
        {
            Debug.Log("[StageManager] 이미 마지막 스테이지입니다.");
            return;
        }

        // StageClearedEvent에서 이미 현재 그리드 상태가 저장되었습니다.
        // SiegeSaveLoader가 StageGenerateCompleteEvent를 받아 복원합니다.
        LoadStage(CurrentStageIndex + 1);
        StartWaveAfterDelay(0, WaveStartDelay);
    }

    public void ClearCurrentStage()
    {
        StopMapWaveStartRoutine();

        if (_currentLayout != null)
        {
            Destroy(_currentLayout.gameObject);
            _currentLayout = null;
        }

        EventBus.Instance.Publish(new StageCleanedUpEvent { StageIndex = CurrentStageIndex });

        CurrentState = InGameState.None;
        Debug.Log($"[StageManager] Stage {CurrentStageIndex} 정리 완료");
    }

    public void StartWave(int waveIndex)
    {
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.IsWaitingForNextWave)
        {
            GrantIncomeForSkippedWait(GameFlowManager.Instance.CurrentWaveWaitRemainingTime);
            GameFlowManager.Instance.RequestImmediateNextWaveStart();
            return;
        }

        if (IsWaitingForWaveStart)
        {
            RequestImmediatePendingWaveStart();
            return;
        }

        StopMapWaveStartRoutine();

        if (CurrentStageData == null)
        {
            Debug.LogError("[StageManager] CurrentStageData가 없습니다.");
            return;
        }

        if (waveIndex < 0 || waveIndex >= CurrentStageData.Waves.Count)
        {
            Debug.LogError($"[StageManager] 잘못된 웨이브 인덱스: {waveIndex}");
            return;
        }

        BeginWave(CurrentStageIndex, waveIndex);
    }

    public int CalculateDropRatClearReward()
    {
        return CalculateDropRatClearReward(CurrentWaveClearTime);
    }

    public int CalculateDropRatClearReward(float clearTime)
    {
        float maxRewardTime = Mathf.Max(0f, _maxRewardClearTime);
        float minRewardTime = Mathf.Max(maxRewardTime, _minRewardClearTime);
        int maxReward = Mathf.Max(0, _maxDropRatReward);
        int minReward = Mathf.Clamp(_minDropRatReward, 0, maxReward);

        if (Mathf.Approximately(minRewardTime, maxRewardTime))
            return clearTime <= maxRewardTime ? maxReward : minReward;

        float rewardRatio = Mathf.InverseLerp(maxRewardTime, minRewardTime, clearTime);
        float reward = Mathf.Lerp(maxReward, minReward, rewardRatio);
        return Mathf.RoundToInt(reward);
    }

    public void StartStageFromMapNode(int stageIndex, float delay)
    {
        StopMapWaveStartRoutine();

        if (_stageDatas == null || stageIndex < 0 || stageIndex >= _stageDatas.Length)
        {
            Debug.LogError($"[StageManager] Invalid map stage index: {stageIndex}");
            return;
        }

        LoadStage(stageIndex);
        StartWaveAfterDelay(0, WaveStartDelay);
    }

    public void StartNextWave()
    {
        StartWave(CurrentWaveIndex + 1);
    }

    public void RequestImmediatePendingWaveStart()
    {
        if (!IsWaitingForWaveStart || _pendingWaveStartIndex < 0)
            return;

        int waveIndex = _pendingWaveStartIndex;
        float remainingTime = WaveStartRemainingTime;

        StopCoroutine(_mapWaveStartCoroutine);
        _mapWaveStartCoroutine = null;
        _pendingWaveStartIndex = -1;
        _waveStartRemainingTime = 0f;
        PublishWaveWaitTick(0f);

        GrantIncomeForSkippedWait(remainingTime);
        StartWave(waveIndex);
    }

    public void EndWave(bool isWin)
    {
        if (_isWaveEnding) return;

        _isWaveEnding = true;
        if (isWin)
            _lastWaveClearTime = GetCurrentWaveElapsedTime();

        CurrentState = InGameState.WaveEnded;
        Debug.Log($"[StageManager] Wave {CurrentWaveIndex} 종료 - isWin: {isWin}");
        EventBus.Instance?.Publish(new WaveEndedEvent { StageIndex = CurrentStageIndex, WaveIndex = CurrentWaveIndex, IsWin = isWin });
    }

    /// <summary>
    /// 수동으로 적을 스폰하는 메서드 (튜토리얼 등에서 버튼 연동)
    /// </summary>
    public void ManualSpawnEnemy(int waveIndex)
    {
        StartCoroutine(ManualSpawnEnemyRoutine(waveIndex));
    }

    private IEnumerator ManualSpawnEnemyRoutine(int waveIndex)
    {
        // 스테이지/레이아웃 준비 체크
        if (CurrentStageData == null)
        {
            Debug.LogError("[StageManager] CurrentStageData가 없습니다.");
            yield break;
        }
        if (_currentLayout == null)
        {
            LoadStage(CurrentStageIndex);
            yield return null;
        }
        int waitCount = 0;
        while (_currentLayout == null && waitCount < 30)
        {
            yield return null;
            waitCount++;
        }
        if (_currentLayout == null)
        {
            Debug.LogError("[StageManager] CurrentLayout이 없습니다.");
            yield break;
        }
        if (waveIndex < 0 || waveIndex >= CurrentStageData.Waves.Count)
        {
            Debug.LogError($"[StageManager] 잘못된 웨이브 인덱스: {waveIndex}");
            yield break;
        }

        EventBus.Instance.Publish(new WaveStartedEvent { WaveIndex = waveIndex });

        WaveData currentWave = CurrentStageData.Waves[waveIndex];
        _currentLayout.SpawnEnemy(currentWave);
        Debug.Log($"[StageManager] ManualSpawnEnemy: Wave {waveIndex} 적 스폰 완료");
    }

    private float GetCurrentWaveElapsedTime()
    {
        return Mathf.Max(0f, Time.time - _waveStartTime);
    }

    private IEnumerator PublishStageGenerateCompleteAfterDelay()
    {
        // 1프레임 대기: Instantiate된 오브젝트의 OnEnable/OnBootstrap 완료 시간 제공
        yield return null;

        Debug.Log("[StageManager] StageGenerateCompleteEvent 발행");
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish(new StageGenerateCompleteEvent());
        }
    }

    private IEnumerator StartMapWaveAfterDelay(int waveIndex, float delay)
    {
        _pendingWaveStartIndex = waveIndex;
        _waveStartRemainingTime = Mathf.Max(0f, delay);
        PublishWaveWaitTick(_waveStartRemainingTime);

        while (_waveStartRemainingTime > 0f)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                yield return null;
                continue;
            }

            _waveStartRemainingTime -= Time.deltaTime;
            PublishWaveWaitTick(_waveStartRemainingTime);
            yield return null;
        }

        _mapWaveStartCoroutine = null;
        _pendingWaveStartIndex = -1;
        _waveStartRemainingTime = 0f;
        PublishWaveWaitTick(0f);
        StartWave(waveIndex);
    }

    private void StartWaveAfterDelay(int waveIndex, float delay)
    {
        StopMapWaveStartRoutine();
        _mapWaveStartCoroutine = StartCoroutine(StartMapWaveAfterDelay(waveIndex, delay));
    }

    private void PublishWaveWaitTick(float remainingTime)
    {
        EventBus.Instance?.Publish(new WaveWaitTimerTickEvent { RemainingTime = Mathf.Max(0f, remainingTime) });
    }

    private void BeginWave(int stageIndex, int waveIndex)
    {
        CurrentStageIndex = stageIndex;
        CurrentWaveIndex = waveIndex;
        CurrentState = InGameState.WavePlaying;
        _isWaveEnding = false;
        _waveStartTime = Time.time;
        _lastWaveClearTime = 0f;
        Debug.Log($"[StageManager] Starting Wave {waveIndex}");
        EventBus.Instance?.Publish(new WaveStartedEvent { StageIndex = CurrentStageIndex, WaveIndex = waveIndex });
    }

    private void StopMapWaveStartRoutine()
    {
        if (_mapWaveStartCoroutine == null)
            return;

        StopCoroutine(_mapWaveStartCoroutine);
        _mapWaveStartCoroutine = null;
        _pendingWaveStartIndex = -1;
        _waveStartRemainingTime = 0f;
        PublishWaveWaitTick(0f);
    }

    private void GrantIncomeForSkippedWait(float remainingTime)
    {
        IncomeResourceProducer producer = FindFirstObjectByType<IncomeResourceProducer>(FindObjectsInactive.Include);
        if (producer != null)
            producer.GrantProductionForDuration(remainingTime);
    }
}
