using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 실습 종류 정의
public enum TutorialCondition { None, CameraMove, PartPlacement, EnemyDefeated }

[System.Serializable]
public class TutorialStep
{
    [Header("Step Info")]
    [Min(0)] public int StepIndex;
    public string StepName = "Step";

    [Header("Module Configurations")]
    public PauseModuleConfig PauseConfig;
    public DialogueModuleConfig DialogueConfig;
    public QuestModuleConfig QuestConfig;
    public PlacementModuleConfig PlacementConfig;
    public CameraModuleConfig CameraConfig;
    public EnemySpawnModuleConfig EnemySpawnConfig;
    public EnemyDefeatedModuleConfig EnemyDefeatedConfig;
    public AutoAdvanceModuleConfig AutoAdvanceConfig;

    /// <summary>
    /// 이 스텝에서 어떤 조건을 사용할지 결정
    /// </summary>
    [Header("Condition Type")]
    public TutorialCondition Condition = TutorialCondition.None;

    public TutorialStep()
    {
        PauseConfig = new PauseModuleConfig();
        DialogueConfig = new DialogueModuleConfig();
        QuestConfig = new QuestModuleConfig();
        PlacementConfig = new PlacementModuleConfig();
        CameraConfig = new CameraModuleConfig();
        EnemySpawnConfig = new EnemySpawnModuleConfig();
        EnemyDefeatedConfig = new EnemyDefeatedModuleConfig();
        AutoAdvanceConfig = new AutoAdvanceModuleConfig();
    }
}

[DefaultExecutionOrder(-90)]
public class TutorialManager : Singleton<TutorialManager>
{
    private const float CAMERA_DEFAULT_SIZE = 10f;
    private const float CAMERA_DEFAULT_POS_Z = -10f;
    private const int DEFENSE_PART_MIN = 1;
    private const int DEFENSE_PART_MAX = 3;
    private const int ATTACK_PART_MIN = 4;
    private const int ATTACK_PART_MAX = 6;

    [Header("Tutorial Data")]
    [SerializeField] private TutorialStep[] _steps;
    [SerializeField] private bool _startOnAwake = true;
    [SerializeField] private TutorialDialoguePresenter _dialoguePresenter;

    [Header("Debug")]
    [Tooltip("체크 시 달성 조건을 무시하고 클릭만으로 튜토리얼을 넘길 수 있습니다 (다이얼로그 확인용)")]
    [SerializeField] private bool _ignoreConditionsForPlaytest = true;

    private int _currentStepIndex = 0;
    private bool _moveNextClicked = false;
    private bool _isPlaying = false;

    private void OnEnable()
    {
        if (EventBus.Instance == null)
        {
            Debug.LogError("EventBus.Instance가 초기화되지 않았습니다.");
            return;
        }

        EventBus.Instance.Subscribe<StageLoadedEvent>(OnStageLoaded);
        // ✅ Presenter로부터 클릭 신호를 이벤트로 수신 (순환 참조 제거)
        EventBus.Instance.Subscribe<TutorialNextRequestedEvent>(OnNextRequested);
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<StageLoadedEvent>(OnStageLoaded);
            // ✅ 정리
            EventBus.Instance.Unsubscribe<TutorialNextRequestedEvent>(OnNextRequested);
        }
    }

    private void Start()
    {
        if (!StageLoadContext.HasValue)
        {
            StageLoadContext.SetStageTutorial();
        }

        if (_startOnAwake)
        {
            TryStartTutorial();
        }
    }

    private void OnStageLoaded(StageLoadedEvent evt)
    {
        if (_isPlaying || _steps == null || _steps.Length == 0) return;
        TryStartTutorial();
    }

    // ✅ 이벤트 핸들러 — Presenter로부터 클릭 신호 수신 (순환 참조 제거)
    private void OnNextRequested(TutorialNextRequestedEvent evt)
    {
        _moveNextClicked = true;
    }

    public void TryStartTutorial()
    {
        if (_isPlaying) return;

        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.ClearAllProgress();
        }
        else
        {
            Debug.LogError("ProgressManager.Instance가 씬에 존재하지 않습니다.");
        }

        _isPlaying = true;
        StartCoroutine(TutorialSequenceRoutine());
    }

    // ✅ 하위 호환용 — SkipButton 등 외부에서 호출 가능 (내부적으로 이벤트 발행)
    public void OnNextButtonClicked()
    {
        EventBus.Instance?.Publish(new TutorialNextRequestedEvent());
    }

    private IEnumerator TutorialSequenceRoutine()
    {
        yield return null; // 씬 초기화 대기

        for (_currentStepIndex = 0; _currentStepIndex < _steps.Length; _currentStepIndex++)
        {
            yield return StartCoroutine(ProcessStepRoutine(_steps[_currentStepIndex], _currentStepIndex));
        }

        CleanupTutorial();
        EventBus.Instance?.Publish(new TutorialCompletedEvent { RewardStageIndex = 0 });
    }

    private IEnumerator ProcessStepRoutine(TutorialStep step, int stepIndex)
    {
        _moveNextClicked = false;

        // 1. UI 파트에 시작 알림
        EventBus.Instance?.Publish(new TutorialStepStartedEvent
        {
            StepIndex = stepIndex,
            TotalStepCount = _steps.Length,
            StepData = step
        });

        // 2. 모듈 구성 (빌더 패턴)
        var modules = new List<ITutorialModule>();

        // 일시정지 및 입력 제어 (가장 먼저 실행)
        modules.Add(new PauseModule());

        // 대사 모듈 (DialogueConfig가 있으면 추가)
        if (step.DialogueConfig != null && step.DialogueConfig.DialogueDataSO != null && _dialoguePresenter != null)
        {
            var dialogueModule = new DialogueModule(_dialoguePresenter);
            dialogueModule.SetStepIndex(stepIndex);
            modules.Add(dialogueModule);
        }

        // 목표/UI 강조 모듈 (TargetUI가 있으면 추가)
        if (step.QuestConfig?.TargetUI != null && _dialoguePresenter != null)
        {
            modules.Add(new QuestModule(_dialoguePresenter));
        }

        // 적 소환 모듈 (스폰할 적이 있으면 추가)
        if (step.EnemySpawnConfig?.EnemySpawnCycles > 0)
        {
            modules.Add(new EnemySpawnModule());
        }

        // 조건 모듈
        if (step.Condition == TutorialCondition.PartPlacement)
        {
            modules.Add(new PlacementModule());
        }
        else if (step.Condition == TutorialCondition.CameraMove)
        {
            modules.Add(new CameraModule());
        }
        else if (step.Condition == TutorialCondition.EnemyDefeated)
        {
            modules.Add(new EnemyDefeatedModule());
        }

        // 자동 진행 또는 클릭 대기 모듈
        modules.Add(new AutoAdvanceModule(
            step.Condition == TutorialCondition.None ? step.AutoAdvanceConfig?.AutoAdvanceDelay ?? 0f : -1f,
            () => _moveNextClicked,
            _ignoreConditionsForPlaytest
        ));

        // 3. 모듈 초기화
        foreach (var module in modules)
        {
            module.Initialize(step);
        }

        try
        {
            // 4. 모듈 순차 실행
            foreach (var module in modules)
            {
                yield return StartCoroutine(module.Execute());
            }

            // 5. 카메라 리셋 (CameraMove 조건 후)
            if (step.Condition == TutorialCondition.CameraMove)
            {
                yield return StartCoroutine(ResetCameraRoutine(1f));
            }
        }
        finally
        {
            // 6. 모듈 정리 (역순)
            for (int i = modules.Count - 1; i >= 0; i--)
            {
                modules[i].Cleanup();
            }
        }

        EventBus.Instance?.Publish(new TutorialStepCompletedEvent { StepIndex = stepIndex });
    }

    private IEnumerator ResetCameraRoutine(float duration)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) yield break;

        float startSize = mainCam.orthographicSize;
        Vector3 startPos = mainCam.transform.position;
        Vector3 targetPos = new Vector3(0f, 0f, CAMERA_DEFAULT_POS_Z);
        float elapsed = 0f;

        if (InputReader.Instance != null) InputReader.Instance.SetInputBlocked(true);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            mainCam.orthographicSize = Mathf.Lerp(startSize, CAMERA_DEFAULT_SIZE, t);
            mainCam.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        mainCam.orthographicSize = CAMERA_DEFAULT_SIZE;
        mainCam.transform.position = targetPos;
        if (InputReader.Instance != null) InputReader.Instance.SetInputBlocked(false);
    }

    public void SkipTutorial()
    {
        StopAllCoroutines();
        CleanupTutorial();
        EventBus.Instance?.Publish(new TutorialCompletedEvent { RewardStageIndex = 0 });
    }

    private void CleanupTutorial()
    {
        _isPlaying = false;
        if (InputReader.Instance != null) InputReader.Instance.SetInputBlocked(false);
        Time.timeScale = 1f;
        StageLoadContext.TutorialClear();
    }
}