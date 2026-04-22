//using UnityEngine;

//public enum GameState
//{
//    Ready,
//    Playing,
//    Paused,
//    GameOver,
//    GameClear
//}

//[DefaultExecutionOrder(-105)]
//public class GameManager : Singleton<GameManager>
//{
//    public GameState CurrentState { get; private set; } = GameState.Ready;

//    protected override void Init()
//    {
//        Application.targetFrameRate = 60;
//    }

//    private void OnEnable()
//    {
//        if (EventBus.Instance != null)
//        {
//            EventBus.Instance.Subscribe<StageLoadedEvent>(OnStageLoaded);
//            EventBus.Instance.Subscribe<StageClearedEvent>(OnStageCleared);
//            EventBus.Instance.Subscribe<StageFailedEvent>(OnStageFailed);
//        }
//    }

//    private void OnDisable()
//    {
//        if (EventBus.Instance != null)
//        {
//            EventBus.Instance.Unsubscribe<StageLoadedEvent>(OnStageLoaded);
//            EventBus.Instance.Unsubscribe<StageClearedEvent>(OnStageCleared);
//            EventBus.Instance.Unsubscribe<StageFailedEvent>(OnStageFailed);
//        }
//    }

//    private void OnStageLoaded(StageLoadedEvent evt)
//    {
//        ChangeState(GameState.Playing);
//    }

//    private void OnStageCleared(StageClearedEvent evt)
//    {
//        if (StageManager.Instance != null && StageManager.Instance.IsFinalStage)
//            ChangeState(GameState.GameClear);
//    }

//    private void OnStageFailed(StageFailedEvent evt)
//    {
//        ChangeState(GameState.GameOver);
//    }

//    public void ChangeState(GameState newState)
//    {
//        if (CurrentState == newState) return;

//        Debug.Log($"[GameManager] State Changed: {CurrentState} -> {newState}");

//        CurrentState = newState;

//        Time.timeScale = (CurrentState == GameState.Paused || CurrentState == GameState.GameOver || CurrentState == GameState.GameClear) ? 0f : 1f;

//        EventBus.Instance.Publish(new GameStateChangedEvent { NewState = CurrentState });
//    }

//    public void ExitGame()
//    {
//#if UNITY_EDITOR
//        UnityEditor.EditorApplication.isPlaying = false;
//#else
//        Application.Quit();
//#endif
//    }
//}