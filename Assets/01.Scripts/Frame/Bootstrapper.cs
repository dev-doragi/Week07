using UnityEngine;

/// <summary>
/// 프로젝트의 모든 매니저 초기화 순서를 중앙 통제하는 클래스입니다.
/// </summary>
[DefaultExecutionOrder(-500)] // 가장 먼저 실행되어 매니저들을 세팅함
public class Bootstrapper : MonoBehaviour
{
    [Header("Global Managers (DDOL)")]
    [SerializeField] private GameLogger _gameLoggerPrefab;
    [SerializeField] private ProgressManager _progressManagerPrefab;
    [SerializeField] private SceneLoader _sceneLoaderPrefab;
    [SerializeField] private GameManager _gameManagerPrefab;
    [SerializeField] private StageManager _stageManagerPrefab;
    [SerializeField] private GameFlowManager _gameFlowManagerPrefab;
    [SerializeField] private PauseManager _pauseManagerPrefab;
    [SerializeField] private SoundManager _soundManagerPrefab;

    [Header("Scene Specific Managers (Non-DDOL)")]
    [SerializeField] private UIManager _uiManagerPrefab;
    [SerializeField] private PoolManager _poolManagerPrefab;

    private void Awake()
    {
        // 1. 매니저 인스턴스 존재 확인 및 생성 (물리적 배치)
        // Singleton.cs의 Awake가 실행되면서 Instance가 등록됨
        EnsureInstance(_gameLoggerPrefab);
        EnsureInstance(_progressManagerPrefab);
        EnsureInstance(_sceneLoaderPrefab);
        EnsureInstance(_gameManagerPrefab);
        EnsureInstance(_stageManagerPrefab);
        EnsureInstance(_gameFlowManagerPrefab);
        EnsureInstance(_pauseManagerPrefab);
        EnsureInstance(_soundManagerPrefab);

        // 씬 종속 매니저 (인게임 씬에서만 할당되어 있다면 여기서 생성됨)
        EnsureInstance(_uiManagerPrefab);
        EnsureInstance(_poolManagerPrefab);
    }

    private void Start()
    {
        // 2. 확정적 순서에 따른 논리적 초기화 (OnBootstrap 호출)
        // 모든 매니저의 Awake가 끝난 시점이므로 Null 참조 위험 없음
        InitializeLogic();
    }

    private void InitializeLogic()
    {
        // [순서 1] 데이터 및 시스템 기반
        if (GameLogger.Instance != null) GameLogger.Instance.BootstrapIfNeeded();
        if (ProgressManager.Instance != null) ProgressManager.Instance.BootstrapIfNeeded();
        if (SceneLoader.Instance != null) SceneLoader.Instance.BootstrapIfNeeded();

        // [순서 2] 게임 코어 및 스테이지 상태
        if (GameManager.Instance != null) GameManager.Instance.BootstrapIfNeeded();
        if (StageManager.Instance != null) StageManager.Instance.BootstrapIfNeeded();

        // [순서 3] 전투 흐름 및 로직
        if (GameFlowManager.Instance != null) GameFlowManager.Instance.BootstrapIfNeeded();
        if (PauseManager.Instance != null) PauseManager.Instance.BootstrapIfNeeded();

        // [순서 4] 뷰 및 풀링 (가장 마지막에 현재 상태 동기화)
        if (PoolManager.Instance != null) PoolManager.Instance.BootstrapIfNeeded();
        if (UIManager.Instance != null) UIManager.Instance.BootstrapIfNeeded();
        if (SoundManager.Instance != null) SoundManager.Instance.BootstrapIfNeeded();

        Debug.Log("<color=green>[Bootstrapper]</color> 모든 매니저가 성공적으로 초기화되었습니다.");
    }

    private void EnsureInstance<T>(T prefab) where T : MonoBehaviour
    {
        if (prefab == null) return;

        // 씬에 이미 존재하거나 DDOL로 넘어온 인스턴스가 있는지 확인
        if (FindAnyObjectByType<T>() != null) return;

        Instantiate(prefab);
    }
}