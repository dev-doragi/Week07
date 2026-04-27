using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Reflection;

public class TutorialManager : MonoBehaviour
{
    private enum TutorialStep
    {
        CameraControl = 0,
        ProductionPlacement = 1,
        AccelerationGuide = 2,
        DefensePlacement = 3,
        SkillGuide = 4,
        AttackPlacement = 5,
        EnemyDefeat = 6
    }

    [Header("Run Settings")]
    [SerializeField] private bool _autoStart = true;
    [SerializeField] private bool _runOnlyWhenTutorialFlag = true;
    [SerializeField] private bool _allowTutorialSceneNameFallback = true;

    [Header("Completion")]
    [SerializeField] private int _rewardStageIndex = 0;
    [SerializeField] private int _attackPlacementPartKey = 0;

    [Header("Debug")]
    [SerializeField] private bool _verboseLog = true;
    [SerializeField] private bool _autoSpawnDialoguePresenter = true;

    public int CurrentStepIndex => _currentStepIndex;
    public int TotalStepCount => StepCount;
    public bool IsCompleted => _isCompleted;
    public bool IsRunning => _isRunning;

    private const int StepCount = 7;

    private int _currentStepIndex;
    private bool _isRunning;
    private bool _isCompleted;

    private void Start()
    {
        if (_autoStart)
            BeginTutorial();
    }

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<TutorialCameraManipulatedEvent>(OnTutorialCameraManipulated);
        EventBus.Instance?.Subscribe<TutorialProductionFacilityPlacedEvent>(OnTutorialProductionFacilityPlaced);
        EventBus.Instance?.Subscribe<TutorialAccelerationButtonUsedEvent>(OnTutorialAccelerationButtonUsed);
        EventBus.Instance?.Subscribe<TutorialDefenseUnitPlacedEvent>(OnTutorialDefenseUnitPlaced);
        EventBus.Instance?.Subscribe<TutorialSkillUsedEvent>(OnTutorialSkillUsed);
        EventBus.Instance?.Subscribe<TutorialAttackUnitPlacedEvent>(OnTutorialAttackUnitPlaced);
        EventBus.Instance?.Subscribe<TutorialEnemyDefeatedEvent>(OnTutorialEnemyDefeated);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<TutorialCameraManipulatedEvent>(OnTutorialCameraManipulated);
        EventBus.Instance?.Unsubscribe<TutorialProductionFacilityPlacedEvent>(OnTutorialProductionFacilityPlaced);
        EventBus.Instance?.Unsubscribe<TutorialAccelerationButtonUsedEvent>(OnTutorialAccelerationButtonUsed);
        EventBus.Instance?.Unsubscribe<TutorialDefenseUnitPlacedEvent>(OnTutorialDefenseUnitPlaced);
        EventBus.Instance?.Unsubscribe<TutorialSkillUsedEvent>(OnTutorialSkillUsed);
        EventBus.Instance?.Unsubscribe<TutorialAttackUnitPlacedEvent>(OnTutorialAttackUnitPlaced);
        EventBus.Instance?.Unsubscribe<TutorialEnemyDefeatedEvent>(OnTutorialEnemyDefeated);
    }

    public void BeginTutorial()
    {
        if (!ShouldRunInCurrentScene())
        {
            _isRunning = false;
            return;
        }

        if (_autoSpawnDialoguePresenter)
            EnsureDialoguePresenterExists();

        _currentStepIndex = 0;
        _isCompleted = false;
        _isRunning = true;

        PublishStepEntryEvents();
        LogStepState("Tutorial started");
    }

    public void CompleteCurrentStepForDebug()
    {
        if (!_isRunning || _isCompleted) return;
        TryCompleteStep((TutorialStep)_currentStepIndex);
    }

    private bool ShouldRunInCurrentScene()
    {
        if (!_runOnlyWhenTutorialFlag)
            return true;

        if (StageLoadContext.IsTutorial)
            return true;

        if (!_allowTutorialSceneNameFallback)
            return false;

        string sceneName = SceneManager.GetActiveScene().name;
        return !string.IsNullOrEmpty(sceneName) && sceneName.Contains("Tutorial");
    }

    private void OnTutorialCameraManipulated(TutorialCameraManipulatedEvent _)
        => TryCompleteStep(TutorialStep.CameraControl);

    private void OnTutorialProductionFacilityPlaced(TutorialProductionFacilityPlacedEvent _)
        => TryCompleteStep(TutorialStep.ProductionPlacement);

    private void OnTutorialAccelerationButtonUsed(TutorialAccelerationButtonUsedEvent _)
        => TryCompleteStep(TutorialStep.AccelerationGuide);

    private void OnTutorialDefenseUnitPlaced(TutorialDefenseUnitPlacedEvent _)
        => TryCompleteStep(TutorialStep.DefensePlacement);

    private void OnTutorialSkillUsed(TutorialSkillUsedEvent _)
        => TryCompleteStep(TutorialStep.SkillGuide);

    private void OnTutorialAttackUnitPlaced(TutorialAttackUnitPlacedEvent _)
        => TryCompleteStep(TutorialStep.AttackPlacement);

    private void OnTutorialEnemyDefeated(TutorialEnemyDefeatedEvent _)
        => TryCompleteStep(TutorialStep.EnemyDefeat);

    private void TryCompleteStep(TutorialStep expectedStep)
    {
        if (!_isRunning || _isCompleted) return;
        if ((int)expectedStep != _currentStepIndex) return;

        EventBus.Instance?.Publish(new TutorialStepCompletedEvent
        {
            StepIndex = _currentStepIndex,
            TotalStepCount = StepCount
        });

        if (expectedStep == TutorialStep.AttackPlacement)
            EventBus.Instance?.Publish(new AttackPlacementTutorialEndedEvent());

        _currentStepIndex++;

        if (_currentStepIndex >= StepCount)
        {
            CompleteTutorial();
            return;
        }

        PublishStepEntryEvents();
        LogStepState($"Step completed: {expectedStep}");
    }

    private void PublishStepEntryEvents()
    {
        if (!_isRunning || _isCompleted) return;

        EventBus.Instance?.Publish(new TutorialStepStartedEvent
        {
            StepIndex = _currentStepIndex,
            TotalStepCount = StepCount
        });

        if ((TutorialStep)_currentStepIndex == TutorialStep.AttackPlacement)
        {
            EventBus.Instance?.Publish(new AttackPlacementTutorialRequestedEvent
            {
                PartKey = _attackPlacementPartKey
            });
        }
    }

    private void CompleteTutorial()
    {
        if (_isCompleted) return;

        _isCompleted = true;
        _isRunning = false;

        EventBus.Instance?.Publish(new TutorialCompletedEvent
        {
            RewardStageIndex = _rewardStageIndex
        });

        LogStepState("Tutorial completed");
    }

    private void LogStepState(string message)
    {
        if (!_verboseLog) return;
        Debug.Log($"[TutorialManager] {message} | Step={_currentStepIndex}/{StepCount}");
    }

    private static void EnsureDialoguePresenterExists()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null) continue;
            if (behaviour.GetType().Name == "TutorialDialoguePresenter")
                return;
        }

        Type presenterType = FindTypeInLoadedAssemblies("TutorialDialoguePresenter");
        if (presenterType == null)
            return;

        GameObject presenterObject = new GameObject("TutorialDialoguePresenter");
        presenterObject.AddComponent(presenterType);
    }

    private static Type FindTypeInLoadedAssemblies(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type found = assemblies[i].GetType(typeName);
            if (found != null)
                return found;
        }

        return null;
    }
}
