using UnityEngine;

/// <summary>
/// 게임의 최상단 캔버스 패널(InGame, GameOver, TutorialClear, Pause 등)의 가시성을 제어하는 매니저입니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - 전역 게임 상태(GameState)에 따른 UI 패널 자동 활성/비활성화
/// - UI 버튼들의 OnClick 이벤트와 코어 시스템(SceneLoader, Pause 등) 연결
///
/// [이벤트 흐름]
/// - Subscribe: GameStateChangedEvent, InGameStateChangedEvent
/// </remarks>
[DefaultExecutionOrder(-100)]
public class UIManager : Singleton<UIManager>
{
    [Header("Main UI Panels")]
    [SerializeField] private GameObject _inGamePanel;
    [SerializeField] private GameObject _gameOverPanel;
    [SerializeField] private GameObject _gameClearPanel;
    [SerializeField] private GameObject _pausePanel;

    [Header("Tutorial")]
    [SerializeField] private bool _autoShowInGamePanelOnPlaying = true;

    [Header("TutorialClear Panel Elements")]
    [SerializeField] private GameObject _gameClearText;
    [SerializeField] private GameObject _stageClearText;
    [SerializeField] private GameObject _resumeButton;

    private void OnEnable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Instance.Subscribe<InGameStateChangedEvent>(OnInGameStateChanged);
            EventBus.Instance.Subscribe<TutorialCompletedEvent>(OnTutorialCompleted);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Instance.Unsubscribe<InGameStateChangedEvent>(OnInGameStateChanged);
            EventBus.Instance.Unsubscribe<TutorialCompletedEvent>(OnTutorialCompleted);
        }
    }

    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        switch (evt.NewState)
        {
            case GameState.Playing:
                HideAllPanels();
                if (_autoShowInGamePanelOnPlaying && _inGamePanel != null) _inGamePanel.SetActive(true);
                break;
            case GameState.Paused:
                if (_pausePanel != null) _pausePanel.SetActive(true);
                break;
            case GameState.GameOver:
                if (_gameOverPanel != null) _gameOverPanel.SetActive(true);
                break;
            case GameState.GameClear:
                ShowClearPanel(isAllGameClear: true);
                break;
            case GameState.Ready:
                HideAllPanels();
                break;
        }
    }

    private void OnInGameStateChanged(InGameStateChangedEvent evt)
    {
        if (evt.NewState == InGameState.StageCleared)
        {
            if (StageMapController.ShouldSuppressStageClearScreen())
            {
                HideAllPanels();
                return;
            }

            if (GameManager.Instance.CurrentState != GameState.GameClear)
            {
                ShowClearPanel(isAllGameClear: false);
            }
        }
    }

    private void ShowClearPanel(bool isAllGameClear)
    {
        if (_gameClearPanel == null) return;
        _gameClearPanel.SetActive(true);

        if (isAllGameClear)
        {
            if (_gameClearText != null) _gameClearText.SetActive(true);
            if (_stageClearText != null) _stageClearText.SetActive(false);
            if (_resumeButton != null) _resumeButton.SetActive(false);
        }
        else
        {
            if (_gameClearText != null) _gameClearText.SetActive(false);
            if (_stageClearText != null) _stageClearText.SetActive(true);
            if (_resumeButton != null) _resumeButton.SetActive(true);
        }
    }

    public void HideAllPanels()
    {
        if (_inGamePanel != null) _inGamePanel.SetActive(false);
        if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
        if (_gameClearPanel != null) _gameClearPanel.SetActive(false);
        if (_pausePanel != null) _pausePanel.SetActive(false);
    }

    public void ShowInGamePanel()
    {
        HideAllPanels();
        if (_inGamePanel != null) _inGamePanel.SetActive(true);
    }

    public void SetInGamePanelVisible(bool isVisible)
    {
        if (isVisible)
        {
            ShowInGamePanel();
            return;
        }

        if (_inGamePanel != null) _inGamePanel.SetActive(false);
    }

    // Button handlers (connect from Inspector)

    public void OnPauseClicked()
    {
        GameCsvLogger.Instance.LogEvent(GameLogEventType.ButtonClicked, actor: gameObject, metadata: new System.Collections.Generic.Dictionary<string, object> { { "button", "Pause" } });
        PauseManager.Instance.TogglePause(true);
    }

    public void OnResumeClicked()
    {
        GameCsvLogger.Instance.LogEvent(GameLogEventType.ButtonClicked, actor: gameObject, metadata: new System.Collections.Generic.Dictionary<string, object> { { "button", "Resume" } });
        PauseManager.Instance.TogglePause(false);
    }

    public void OnGoToLobbyClicked()
    {
        GameCsvLogger.Instance.LogEvent(GameLogEventType.ButtonClicked, actor: gameObject, metadata: new System.Collections.Generic.Dictionary<string, object> { { "button", "GoToLobby" } });
        SceneLoader.Instance.GoToLobby();
    }

    public void OnRetryClicked()
    {
        GameCsvLogger.Instance.LogEvent(GameLogEventType.ButtonClicked, actor: gameObject, metadata: new System.Collections.Generic.Dictionary<string, object> { { "button", "Retry" } });
        HideAllPanels();  // 패널 먼저 닫기
        SiegeCache.Clear();
        SceneLoader.Instance.ReloadCurrentScene();
    }

    public void OnNextStageClicked()
    {
        GameCsvLogger.Instance.LogEvent(GameLogEventType.ButtonClicked, actor: gameObject, metadata: new System.Collections.Generic.Dictionary<string, object> { { "button", "NextStage" } });
        // 차량 데이터 저장 로직은 제거

        HideAllPanels();
        if (_inGamePanel != null) _inGamePanel.SetActive(true);
        StageManager.Instance.LoadNextStage();
    }

    public void OnGoToStageSelectClicked()
    {
        GameCsvLogger.Instance.LogEvent(GameLogEventType.ButtonClicked, actor: gameObject, metadata: new System.Collections.Generic.Dictionary<string, object> { { "button", "GoToStageSelect" } });
        HideAllPanels();
        SceneLoader.Instance.GoToStageSelect();
    }

    private void OnTutorialCompleted(TutorialCompletedEvent evt)
    {
        HideAllPanels();
        //ShowClearPanel(isAllGameClear: false); 
        if (_resumeButton != null) _resumeButton.SetActive(false);

        Time.timeScale = 0f;
        if (InputReader.Instance != null) InputReader.Instance.SetInputBlocked(true);
    }

    protected override void OnBootstrap()
    {
        // 부트스트랩 시점에 현재 전역 및 인게임 상태를 읽어 UI를 동기화합니다.
        if (GameManager.Instance != null)
        {
            OnGameStateChanged(new GameStateChangedEvent { NewState = GameManager.Instance.CurrentState });
        }

        if (GameFlowManager.Instance != null)
        {
            OnInGameStateChanged(new InGameStateChangedEvent { NewState = GameFlowManager.Instance.CurrentInGameState });
        }
    }
}
