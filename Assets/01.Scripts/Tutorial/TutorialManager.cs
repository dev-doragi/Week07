using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-90)]
public class TutorialManager : Singleton<TutorialManager>
{
    [Header("Tutorial Data")]
    [SerializeField] private TutorialStep[] _steps;
    [SerializeField] private bool _startOnAwake = true;
    [SerializeField] private TutorialDialoguePresenter _dialoguePresenter;
    [SerializeField] private RectTransform _highlighter;

    [Header("Tutorial UI Roots")]
    [SerializeField] private GameObject _inGamePanel;
    [SerializeField] private GameObject _blockPanel;
    [SerializeField] private GameObject _shopButtonGroup;
    [SerializeField] private GameObject _productionGridRoot;
    [SerializeField] private GameObject _waveStartButton;
    [SerializeField] private GameObject _chargeButton;

    [Header("Debug")]
    [Tooltip("체크 시 달성 조건을 무시하고 클릭만으로 튜토리얼을 넘길 수 있습니다 (다이얼로그 확인용)")]
    [SerializeField] private bool _ignoreConditionsForPlaytest = true;

    private int _currentStepIndex = 0;
    private bool _moveNextClicked = false;
    private bool _isPlaying = false;
    private bool _isTutorialCompleted = false;

    // 튜토리얼 중에 미리 격파된 적 추적 (Early Enemy Defeat 대응)
    private static int _pregrindEnemiesDefeated = 0;

    public bool IsTutorialCompleted => _isTutorialCompleted;

    private void OnEnable()
    {
        if (EventBus.Instance == null)
        {
            Debug.LogError("EventBus.Instance가 초기화되지 않았습니다.");
            return;
        }

        EventBus.Instance.Subscribe<StageLoadedEvent>(OnStageLoaded);
        EventBus.Instance.Subscribe<TutorialNextRequestedEvent>(OnNextRequested);
        EventBus.Instance.Subscribe<TutorialEnemyDefeatedEvent>(OnPregrindEnemyDefeated);
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<StageLoadedEvent>(OnStageLoaded);
            EventBus.Instance.Unsubscribe<TutorialNextRequestedEvent>(OnNextRequested);
            EventBus.Instance.Unsubscribe<TutorialEnemyDefeatedEvent>(OnPregrindEnemyDefeated);
        }
    }

    private void Start()
    {
    if (!StageLoadContext.HasValue)
    {
        StageLoadContext.SetStageTutorial();

        Debug.Log("현재 스테이지는 튜토리얼 전용 스테이지가 되었습니다.");
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

private void OnNextRequested(TutorialNextRequestedEvent evt)
{
    _moveNextClicked = true;
}

/**
 * 튜토리얼 적 격파 이벤트를 미리 추적 (Early Defeat 대응)
 */
 private void OnPregrindEnemyDefeated(TutorialEnemyDefeatedEvent evt)
 {
     if (!_isPlaying || _steps == null)
         return;

     // 현재 또는 이후 스텝 중에서 EnemyDefeated 조건을 찾기
     for (int i = _currentStepIndex; i < _steps.Length; i++)
     {
         TutorialStep step = _steps[i];
         if (step.Condition == TutorialCondition.EnemyDefeated && 
             step.EnemyDefeatedConfig != null)
         {
             // 이 스텝의 필터 기준으로 검증
             if (IsCountableForDefeatTarget(evt, step.EnemyDefeatedConfig.Target))
             {
                 _pregrindEnemiesDefeated++;
                 Debug.Log($"[TutorialManager] Early Enemy Defeat 카운팅: {_pregrindEnemiesDefeated}");
             }
             // 첫 번째 매칭하는 스텝만 카운팅
             break;
         }
     }
 }

    /// <summary>
    /// 격파 대상이 필터 기준을 만족하는지 확인
    /// </summary>
    private bool IsCountableForDefeatTarget(TutorialEnemyDefeatedEvent evt, TutorialEnemyDefeatTarget target)
    {
        switch (target)
        {
            case TutorialEnemyDefeatTarget.CoreOnly:
                return evt.Category == UnitCategory.Core;

            case TutorialEnemyDefeatTarget.NonCoreOnly:
                return evt.Category != UnitCategory.Core;

            default: // Any
                return true;
        }
    }

    /// <summary>
    /// 미리 격파된 적의 수를 조회하고 초기화
    /// </summary>
    public static int GetAndResetPregrindsEnemyCount()
    {
        int count = _pregrindEnemiesDefeated;
        _pregrindEnemiesDefeated = 0;
        return count;
    }

    public void OnNextButtonClicked()
    {
        EventBus.Instance?.Publish(new TutorialNextRequestedEvent());
    }

    public void PublishTutorialInteraction(string interactionId)
    {
        EventBus.Instance?.Publish(new TutorialInteractionTriggeredEvent
        {
            InteractionId = interactionId
        });
    }

    public void PublishWaveStartButtonInteraction()
    {
        PublishTutorialInteraction("WaveStartButton");
    }

    public void TryStartTutorial()
    {
        if (_isPlaying) return;

        // 튜토리얼 재시작 시 글로벌 상태 초기화
        Time.timeScale = 1f;
        if (InputReader.Instance != null)
        {
            InputReader.Instance.SetInputBlocked(false);
        }

        // 튜토리얼 플래그 초기화
        _isTutorialCompleted = false;

        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.ClearAllProgress();
        }
        else
        {
            Debug.LogError("ProgressManager.Instance가 씬에 존재하지 않습니다.");
        }

        _isPlaying = true;
        _pregrindEnemiesDefeated = 0;

        if (_steps != null && _steps.Length > 0)
        {
            ApplyTutorialUiState(_steps[0]);
        }

        StartCoroutine(TutorialSequenceRoutine());
    }

    private IEnumerator TutorialSequenceRoutine()
    {
        yield return null;

        var stepRunner = new TutorialStepRunner(_dialoguePresenter, () => _moveNextClicked, _ignoreConditionsForPlaytest, this, _highlighter);
        for (_currentStepIndex = 0; _currentStepIndex < _steps.Length; _currentStepIndex++)
        {
            ApplyTutorialUiState(_steps[_currentStepIndex]);
            _moveNextClicked = false;
            yield return stepRunner.RunStep(_steps[_currentStepIndex], _currentStepIndex, _steps.Length);
        }

        _isTutorialCompleted = true;
        CleanupTutorial();
        EventBus.Instance?.Publish(new TutorialCompletedEvent { RewardStageIndex = 0 });
    }

    private void ApplyTutorialUiState(TutorialStep step)
    {
        if (step == null || step.UIStateConfig == null || !step.UIStateConfig.OverrideUIState)
            return;

        var config = step.UIStateConfig;

        SetInGamePanelVisible(config.ShowInGamePanel);
        SetActive(_blockPanel, config.ShowBlockPanel);
        SetActive(_shopButtonGroup, config.ShowShopButtonGroup);
        SetActive(_productionGridRoot, config.ShowProductionGrid);
        SetActive(_waveStartButton, config.ShowWaveStartButton);
        SetActive(_chargeButton, config.ShowChargeButton);
    }

    private void SetInGamePanelVisible(bool visible)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetInGamePanelVisible(visible);
            return;
        }

        SetActive(_inGamePanel, visible);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    // 버튼에서 호출할 수 있도록 StageManager의 ManualSpawnEnemy을 직접 호출
    public void ManualSpawnEnemy(int waveIndex)
    {
        StageManager.Instance.ManualSpawnEnemy(waveIndex);
    }

    public void SkipTutorial()
    {
        StopAllCoroutines();
        CleanupTutorial();
        EventBus.Instance?.Publish(new TutorialCompletedEvent { RewardStageIndex = 0 });

        // TODO: 이후 로딩 없이 바로 플레이 가능하도록 변경
        // SceneLoader.Instance.LoadScene("Main");
    }

    private void CleanupTutorial()
    {
        _isPlaying = false;
        // _isTutorialCompleted는 초기화하지 않음 (튜토리얼 완료 상태 유지)
        _pregrindEnemiesDefeated = 0;
        if (InputReader.Instance != null) InputReader.Instance.SetInputBlocked(false);
        Time.timeScale = 1f;
        SetActive(_blockPanel, false);
        StageLoadContext.TutorialClear();
    }
}