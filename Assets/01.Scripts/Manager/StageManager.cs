using UnityEngine;

[DefaultExecutionOrder(-100)]
/// <summary>
/// 스테이지 데이터 로드, 환경(Grid/Layout) 생성 및 웨이브 진행을 총괄하는 매니저입니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - ScriptableObject(StageDataSO) 기반 스테이지 환경 및 유닛 프리팹 배치
/// - 현재 웨이브(Wave)의 시작과 끝을 통제하는 중앙 권한자
///
/// [이벤트 흐름]
/// - Publish: StageLoadedEvent, StageGenerateCompleteEvent, WaveStartedEvent, WaveEndedEvent
/// </remarks>
public class StageManager : Singleton<StageManager>
{
    [Header("Stage Settings")]
    [SerializeField] private StageDataSO[] _stageDatas;
    [SerializeField] private Transform _stageParent;

    private StageLayout _currentLayout;

    public int CurrentStageIndex { get; private set; } = 0;
    public StageDataSO CurrentStageData => _stageDatas != null && CurrentStageIndex >= 0 && CurrentStageIndex < _stageDatas.Length ? _stageDatas[CurrentStageIndex] : null;

    // 현재 웨이브 인덱스(Stage 내 진행 중인 웨이브). 기본 0.
    public int CurrentWaveIndex { get; private set; } = 0;

    public bool IsFinalStage => _stageDatas != null && CurrentStageIndex >= _stageDatas.Length - 1;

    protected override void Init()
    {
        if (_stageParent == null)
        {
            GameObject parentObj = new GameObject("StageContainer");
            _stageParent = parentObj.transform;
            DontDestroyOnLoad(parentObj);
        }
    }

    private void Start()
    {
        // TODO: 세이브 데이터 연동 시 타겟 인덱스 수정
        LoadStage(0);
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
            _currentLayout = Instantiate(nextData.StageLayoutPrefab, _stageParent);
            // TODO: 여기서 그리드를 초기화하고 SO 데이터를 기반으로 몬스터나 환경 프리팹을 배치합니다.
        }

        EventBus.Instance.Publish(new StageLoadedEvent { StageIndex = CurrentStageIndex });

        // 배치가 완전히 끝났음을 알림 (GameFlowManager가 Prepare 상태로 진입)
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