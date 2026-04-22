using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-99)]
/// <summary>
/// Unity 씬(Scene) 전환과 관련된 컨텍스트 설정을 담당하는 유틸리티 매니저입니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - 로비, 튜토리얼, 스테이지 선택, 인게임 간의 안전한 씬 이동
/// - 씬 이동 전 StageLoadContext를 통한 데이터(타겟 스테이지 인덱스 등) 주입
/// </remarks>
public class SceneLoader : Singleton<SceneLoader>
{
    [Header("Scene Settings")]
    [SerializeField] private string _lobbySceneName = "01.LobbyScene";
    [SerializeField] private string _tutorialSceneName = "02.TutorialScene";
    [SerializeField] private string _stageSelectSceneName = "03.StageSelectScene";
    [SerializeField] private string _inGameSceneName = "04.InGameScene";

    public void GoToLobby()
    {
        if (ManagerRegistry.TryGet(out GameManager gameManager))
        {
            gameManager.ChangeState(GameState.Ready);
        }

        SceneManager.LoadScene(_lobbySceneName);
    }

    public void GoToStageSelect()
    {
        SceneManager.LoadScene(_stageSelectSceneName);
    }

    public void EnterTutorial()
    {
        // 튜토리얼 플래그 설정
        StageLoadContext.SetStageTutorial();
        SceneManager.LoadScene(_tutorialSceneName);
    }

    public void EnterInGameFromTutorial(int stageIndex)
    {
        // 튜토리얼에서 게임 진행
        StageLoadContext.SetStageIndex(stageIndex);
        SceneManager.LoadScene(_inGameSceneName);
    }

    public void EnterInGame(int stageIndex)
    {
        // 스테이지 정보 설정
        StageLoadContext.SetStageIndex(stageIndex);

        SceneManager.LoadScene(_inGameSceneName);
    }

    public void ReloadCurrentScene()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;

        if (currentSceneName == _inGameSceneName)
        {
            int currentStageIndex = StageManager.Instance.CurrentStageIndex;
            StageLoadContext.SetStageIndex(currentStageIndex);
        }

        SceneManager.LoadScene(currentSceneName);
    }

    public void Quit()
    {
        Application.Quit();
    }
}