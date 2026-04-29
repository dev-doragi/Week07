using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// 실습 종류 정의
public enum TutorialCondition { None, CameraMove, PartPlacement, EnemyDefeated }
public enum RequiredPartGroup { Any, Defense, Attack, Custom }

[System.Serializable]
public class TutorialStep
{
    public string SpeakerName;
    [TextArea(3, 5)] public string Message;
    public Sprite PortraitSprite;
    public RectTransform TargetUI;
    public int[] RequiredPartKeys;
    public RequiredPartGroup RequiredGroup = RequiredPartGroup.Any;

    [Header("UI Motion")]
    public bool MoveDialogUp = false;
    public float MoveOffset = 300f;
    public float MoveDuration = 0.5f;

    [Header("Control Settings")]
    public bool ShouldPause = true;
    public bool BlockInput = true;

    [Header("Action Conditions")]
    public TutorialCondition Condition = TutorialCondition.None;
    public float RequiredAmount = 1.0f;
    public float AutoAdvanceDelay = 3.0f;
    public string PlacementLabel;

    [Header("Portrait Motion")]
    public bool MovePortraitLeft = false;
    public float PortraitMoveOffset = 150f;
    public float PortraitMoveDuration = 0.5f;
    public RectTransform PortraitTarget;

    [Header("Tutorial Enemy Spawn")]
    public bool SpawnEnemyForStep = false;
    public int EnemySpawnCycles = 1;
    public WaveData TutorialWaveData;
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

    [Header("Debug")]
    [Tooltip("체크 시 달성 조건을 무시하고 클릭만으로 튜토리얼을 넘길 수 있습니다 (다이얼로그 확인용)")]
    [SerializeField] private bool _ignoreConditionsForPlaytest = true;

    private int _currentStepIndex = 0;
    private float _currentProgress = 0f;
    private bool _isCurrentStepConditionMet = false;
    private bool _moveNextClicked = false;
    private bool _isPlaying = false;

    private string _currentPlacementLabel = "";
    private Coroutine _tutorialEnemyRoutine = null;

    private void OnEnable()
    {
        if (EventBus.Instance == null)
        {
            Debug.LogError("EventBus.Instance가 초기화되지 않았습니다.");
            return;
        }

        EventBus.Instance.Subscribe<StageLoadedEvent>(OnStageLoaded);
        EventBus.Instance.Subscribe<ScrollEvent>(OnCameraZoomed);
        EventBus.Instance.Subscribe<RightClickEvent>(OnCameraDragged);
        EventBus.Instance.Subscribe<PartPlacedEvent>(OnPartPlaced);
        EventBus.Instance.Subscribe<TutorialEnemyDefeatedEvent>(OnTutorialEnemyDefeated);
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<StageLoadedEvent>(OnStageLoaded);
            EventBus.Instance.Unsubscribe<ScrollEvent>(OnCameraZoomed);
            EventBus.Instance.Unsubscribe<RightClickEvent>(OnCameraDragged);
            EventBus.Instance.Unsubscribe<PartPlacedEvent>(OnPartPlaced);
            EventBus.Instance.Unsubscribe<TutorialEnemyDefeatedEvent>(OnTutorialEnemyDefeated);
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

    // Presenter(UI)에서 '다음' 버튼을 누르거나 스크린을 클릭했을 때 호출됨
    public void OnNextButtonClicked()
    {
        _moveNextClicked = true;
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
        _isCurrentStepConditionMet = false;
        _currentProgress = 0f;
        _moveNextClicked = false;

        // 라벨 초기화
        _currentPlacementLabel = GetLabelForStep(step);

        // 1. UI 파트에 시작 알림
        EventBus.Instance?.Publish(new TutorialStepStartedEvent
        {
            StepIndex = stepIndex,
            TotalStepCount = _steps.Length,
            StepData = step
        });

        Time.timeScale = step.ShouldPause ? 0f : 1f;

        if (InputReader.Instance != null)
        {
            InputReader.Instance.SetInputBlocked(step.BlockInput);
        }
        else
        {
            Debug.LogError("InputReader.Instance가 씬에 존재하지 않습니다.");
        }

        // 적 스폰
        if (step.SpawnEnemyForStep)
        {
            StartTutorialWaveSpawnCycle(Mathf.Max(1, step.EnemySpawnCycles), step.TutorialWaveData);
        }

        // 진행도 초기 방송
        BroadcastProgressUpdate();

        // 2. 조건 달성 혹은 딜레이/클릭 대기
        if (step.Condition != TutorialCondition.None)
        {
            Time.timeScale = 1f;
            if (InputReader.Instance != null) InputReader.Instance.SetInputBlocked(false);

            if (_ignoreConditionsForPlaytest)
            {
                // 플레이 테스트 치트 켜진 경우 조건 무시하고 클릭만 대기
                while (!_moveNextClicked) yield return null;
            }
            else
            {
                // 실제 조건 대기
                while (!_isCurrentStepConditionMet) yield return null;
            }

            if (step.Condition == TutorialCondition.CameraMove)
            {
                yield return StartCoroutine(ResetCameraRoutine(1f));
            }
        }
        else
        {
            if (step.AutoAdvanceDelay > 0f)
            {
                float elapsed = 0f;
                while (!_moveNextClicked && elapsed < step.AutoAdvanceDelay)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                while (!_moveNextClicked) yield return null;
            }
        }

        // 3. 스텝 종료 처리
        if (step.SpawnEnemyForStep)
        {
            StopTutorialEnemyCycle();
        }

        EventBus.Instance?.Publish(new TutorialStepCompletedEvent { StepIndex = stepIndex });
    }

    private void BroadcastProgressUpdate()
    {
        if (_steps == null || _currentStepIndex < 0 || _currentStepIndex >= _steps.Length) return;

        EventBus.Instance?.Publish(new TutorialProgressUpdatedEvent
        {
            CurrentProgress = _currentProgress,
            RequiredProgress = _steps[_currentStepIndex].RequiredAmount,
            Label = _currentPlacementLabel
        });
    }

    private void CheckCondition()
    {
        if (_steps == null || _currentStepIndex < 0 || _currentStepIndex >= _steps.Length) return;

        if (_currentProgress >= _steps[_currentStepIndex].RequiredAmount)
        {
            _isCurrentStepConditionMet = true;
        }
        BroadcastProgressUpdate();
    }

    private bool IsCurrentStepCondition(TutorialCondition condition)
    {
        if (!_isPlaying || _steps == null || _currentStepIndex < 0 || _currentStepIndex >= _steps.Length) return false;
        return _steps[_currentStepIndex].Condition == condition;
    }

    // 이벤트 리스너: 진행도 상승
    private void OnCameraZoomed(ScrollEvent e)
    {
        if (!IsCurrentStepCondition(TutorialCondition.CameraMove)) return;
        _currentProgress += 0.2f;
        CheckCondition();
    }

    private void OnCameraDragged(RightClickEvent e)
    {
        if (!IsCurrentStepCondition(TutorialCondition.CameraMove) || !e.IsStarted) return;
        _currentProgress += 0.1f;
        CheckCondition();
    }

    private void OnPartPlaced(PartPlacedEvent e)
    {
        if (!IsCurrentStepCondition(TutorialCondition.PartPlacement)) return;

        var step = _steps[_currentStepIndex];
        if (!IsPartKeyMatchingGroup(e.PartKey, step.RequiredGroup, step.RequiredPartKeys)) return;

        _currentProgress += 1.0f;
        CheckCondition();
    }

    private void OnTutorialEnemyDefeated(TutorialEnemyDefeatedEvent e)
    {
        if (!IsCurrentStepCondition(TutorialCondition.EnemyDefeated)) return;
        _currentProgress += 1.0f;
        CheckCondition();
    }

    private bool IsPartKeyMatchingGroup(int partKey, RequiredPartGroup group, int[] customKeys)
    {
        switch (group)
        {
            case RequiredPartGroup.Any: return true;
            case RequiredPartGroup.Defense: return partKey >= DEFENSE_PART_MIN && partKey <= DEFENSE_PART_MAX;
            case RequiredPartGroup.Attack: return partKey >= ATTACK_PART_MIN && partKey <= ATTACK_PART_MAX;
            case RequiredPartGroup.Custom:
                if (customKeys == null || customKeys.Length == 0) return false;
                foreach (var key in customKeys) { if (key == partKey) return true; }
                return false;
            default: return false;
        }
    }

    private string GetLabelForStep(TutorialStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.PlacementLabel)) return step.PlacementLabel;

        return step.RequiredGroup switch
        {
            RequiredPartGroup.Attack => "공격 유닛",
            RequiredPartGroup.Defense => "방어 유닛",
            RequiredPartGroup.Custom => "선택 유닛",
            _ => "유닛"
        };
    }

    // 웨이브 트리거 및 적 관리
    public void StartTutorialWaveSpawnCycle(int cycles = 1, WaveData waveData = default)
    {
        if (_tutorialEnemyRoutine != null) StopCoroutine(_tutorialEnemyRoutine);
        _tutorialEnemyRoutine = StartCoroutine(TutorialWaveSpawnRoutine(Mathf.Max(1, cycles), waveData));
    }

    public void StopTutorialEnemyCycle()
    {
        if (_tutorialEnemyRoutine != null)
        {
            StopCoroutine(_tutorialEnemyRoutine);
            _tutorialEnemyRoutine = null;
        }
    }

    private IEnumerator TutorialWaveSpawnRoutine(int cycles, WaveData waveData)
    {
        // StageLayout을 직접 참조하여 StageManager/GameFlowManager 흐름 완전 우회
        StageLayout layout = StageManager.Instance?.CurrentLayout;
        if (layout == null)
        {
            Debug.LogError("[Tutorial] CurrentLayout 없음 - 튜토리얼 씬에 StageLayout이 있어야 합니다.");
            _tutorialEnemyRoutine = null;
            yield break;
        }

        if (waveData.EnemySiegePrefab == null)
        {
            Debug.LogError("[Tutorial] TutorialWaveData.EnemySiegePrefab이 할당되지 않았습니다.");
            _tutorialEnemyRoutine = null;
            yield break;
        }

        for (int i = 0; i < cycles; i++)
        {
            // StageManager.StartWave() 우회 → SpawnEnemy 직접 호출
            layout.SpawnEnemy(waveData);

            float wait = 0f;
            while (layout.CurrentEnemySiege == null && wait < 3f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }

            GameObject enemy = layout.CurrentEnemySiege;
            if (enemy == null)
            {
                yield return new WaitForSecondsRealtime(0.2f);
                continue;
            }

            // 스폰된 적 유닛 전체에 튜토리얼 플래그 설정
            Unit[] units = enemy.GetComponentsInChildren<Unit>(true);
            foreach (Unit unit in units)
                unit.SetAsTutorialEnemy();

            // 적이 파괴될 때까지 대기
            while (enemy != null) yield return null;

            yield return new WaitForSecondsRealtime(0.2f);
        }

        _tutorialEnemyRoutine = null;
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
        StopTutorialEnemyCycle();
        if (InputReader.Instance != null) InputReader.Instance.SetInputBlocked(false);
        Time.timeScale = 1f;
        StageLoadContext.TutorialClear();
    }
}