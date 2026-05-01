using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Unity 씬(Scene) 전환과 관련된 컨텍스트 설정을 담당하는 유틸리티 매니저입니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - 로비, 튜토리얼, 스테이지 선택, 인게임 간의 안전한 씬 이동
/// - 씬 이동 전 StageLoadContext를 통한 데이터(타겟 스테이지 인덱스 등) 주입
/// - 씬 전환 시 타임 스케일 복구 (튜토리얼 클리어 후 0으로 설정된 상태 해제)
/// - 씬 전환 시 입력 차단 플래그 초기화
/// </remarks>

[DefaultExecutionOrder(-180)]
public class SceneLoader : Singleton<SceneLoader>
{
    [Header("Scene Settings")]
    [SerializeField] private string _lobbySceneName = "01.LobbyScene";
    [SerializeField] private string _tutorialSceneName = "02.TutorialScene";
    [SerializeField] private string _stageSelectSceneName = "03.StageSelectScene";
    [SerializeField] private string _inGameSceneName = "04.InGameScene";

    /// <summary>
    /// 씬 전환 시 필요한 글로벌 상태를 초기화합니다.
    /// </summary>
    private void ResetGlobalState()
    {
        // 타임 스케일 복구 (튜토리얼 클리어 시 0으로 설정되므로)
        Time.timeScale = 1f;

        // 입력 차단 해제 (튜토리얼 클리어 시 true로 설정되므로)
        if (InputReader.Instance != null)
        {
            InputReader.Instance.SetInputBlocked(false);
        }
    }

    public void GoToLobby()
    {
        ResetGlobalState();
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameState.Ready);
        }

        GameLogContext.RunId = string.Empty;
        SceneManager.LoadScene(_lobbySceneName);
    }

    public void GoToStageSelect()
    {
        ResetGlobalState();
        SceneManager.LoadScene(_stageSelectSceneName);
    }

    public void EnterTutorial()
    {
        ResetGlobalState();

        // 튜토리얼 플래그 설정
        StageLoadContext.SetStageTutorial();
        SceneManager.LoadScene(_tutorialSceneName);
    }

    public void EnterInGameFromTutorial(int stageIndex)
    {
        ResetGlobalState();

        BeginNewRun();

        // 튜토리알에서 게임 진행
        StageLoadContext.SetStageIndex(stageIndex);
        SceneManager.LoadScene(_inGameSceneName);
    }

    public void EnterInGame(int stageIndex)
    {
        ResetGlobalState();

        BeginNewRun();

        // 스테이지 정보 설정
        StageLoadContext.SetStageIndex(stageIndex);
        SceneManager.LoadScene(_inGameSceneName);
    }

    public void ReloadCurrentScene()
    {
        ResetGlobalState();

        // 게임 상태를 Playing으로 변경 (UI 패널 초기화)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameState.Playing);
        }

        // UI 패널 명시적 초기화
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideAllPanels();
        }

        string currentSceneName = SceneManager.GetActiveScene().name;

        if (currentSceneName == _inGameSceneName)
        {
            BeginNewRun();
            StageLoadContext.SetStageIndex(0);
        }

        SceneManager.LoadScene(currentSceneName);
    }

    public void Quit()
    {
        Application.Quit();
    }

    private static void BeginNewRun()
    {
        GameLogContext.RunId = Guid.NewGuid().ToString("N");
    }
}
