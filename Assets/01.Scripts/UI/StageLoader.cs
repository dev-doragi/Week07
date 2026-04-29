using UnityEngine;

public class StageLoader : MonoBehaviour
{
    [Header("디버깅용 스테이지 인덱스")]
    public int stageIndex = 0;

    // 버튼에 직접 할당해서, 인스펙터에서 지정한 인덱스의 스테이지를 로드
    public void LoadStage(int stageIndex)
    {
        SceneLoader.Instance.EnterInGame(stageIndex);
    }

    // 버튼에 직접 할당해서 스테이지 선택 씬으로 이동
    public void LoadStageSelect()
    {
        SceneLoader.Instance.GoToStageSelect();
    }

    // 버튼에 직접 할당해서 튜토리얼 씬으로 이동
    public void LoadTutorial()
    {
        SceneLoader.Instance.EnterTutorial();
    }

    public void QuitGame()
    {
        SceneLoader.Instance.Quit();
    }
}
