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

    private void OnEnable()
    {
        if (EventBus.Instance == null)
        {
            Debug.LogError("EventBus.Instance가 초기화되지 않았습니다.");
            return;
        }

        EventBus.Instance.Subscribe<StageLoadedEvent>(OnStageLoaded);
        EventBus.Instance.Subscribe<TutorialNextRequestedEvent>(OnNextRequested);
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<StageLoadedEvent>(OnStageLoaded);
            EventBus.Instance.Unsubscribe<TutorialNextRequestedEvent>(OnNextRequested);
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

        if (_steps != null && _steps.Length > 0)
        {
            ApplyTutorialUiState(_steps[0]);
        }

        StartCoroutine(TutorialSequenceRoutine());
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

        CleanupTutorial();
        EventBus.Instance?.Publish(new TutorialCompletedEvent { RewardStageIndex = 0 });
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
        SetActive(_blockPanel, false);
        StageLoadContext.TutorialClear();
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
}