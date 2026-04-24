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

    private StageLayout _currentLayout;

    public int CurrentStageIndex { get; private set; } = 0;
    public int CurrentWaveIndex { get; private set; } = 0;
    public InGameState CurrentState { get; private set; } = InGameState.None;
    public StageDataSO CurrentStageData => _stageDatas != null && CurrentStageIndex >= 0 && CurrentStageIndex < _stageDatas.Length ? _stageDatas[CurrentStageIndex] : null;
    public bool IsFinalStage => _stageDatas != null && CurrentStageIndex >= _stageDatas.Length - 1;

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
        }
    }

    private void OnEnable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<WaveStartedEvent>(OnWaveStarted);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
        }
    }

    /// <summary>
    /// [EventBus] 웨이브 시작 시 StageLayout에 적 스폰을 위임합니다.
    /// </summary>
    private void OnWaveStarted(WaveStartedEvent evt)
    {
        Debug.Log($"[StageManager] Wave {evt.WaveIndex} 시작 - 적 스폰 위임 중...");

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
        EventBus.Instance.Publish(new StageGenerateCompleteEvent());

        Debug.Log($"[StageManager] Stage {stageIndex} 로드 및 그리드 배치 완료.");
    }

    public void LoadNextStage()
    {
        if (IsFinalStage)
        {
            Debug.Log("[StageManager] 이미 마지막 스테이지입니다.");
            return;
        }
        LoadStage(CurrentStageIndex + 1);
    }

    public void ClearCurrentStage()
    {
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

        CurrentWaveIndex = waveIndex;
        Debug.Log($"[StageManager] Starting Wave {waveIndex}");
        EventBus.Instance?.Publish(new WaveStartedEvent { WaveIndex = waveIndex });
    }

    public void StartNextWave()
    {
        StartWave(CurrentWaveIndex + 1);
    }

    public void EndWave(bool isWin)
    {
        Debug.Log($"[StageManager] Wave {CurrentWaveIndex} 종료 - isWin: {isWin}");
        EventBus.Instance?.Publish(new WaveEndedEvent { IsWin = isWin });
    }
}