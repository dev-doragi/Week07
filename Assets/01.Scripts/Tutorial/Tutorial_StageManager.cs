using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Tutorial_StageManager : MonoBehaviour
{
    [Header("Tutorial Stage")]
    [SerializeField] private bool _autoLoadTutorialStage = true;
    [SerializeField] private bool _autoStartFirstWave = true;
    [SerializeField] private int _tutorialStageIndex = 0;
    [SerializeField] private bool _runOnlyWhenTutorialFlag = true;
    [SerializeField] private bool _allowTutorialSceneNameFallback = true;

    private IEnumerator Start()
    {
        if (!_autoLoadTutorialStage) yield break;
        if (!ShouldRunInCurrentScene()) yield break;
        if (StageManager.Instance == null) yield break;

        StageManager.Instance.LoadStage(_tutorialStageIndex);
        yield return null;

        if (_autoStartFirstWave)
            StageManager.Instance.StartWave(0);
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
}
