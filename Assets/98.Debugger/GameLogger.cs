using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-110)]
/// <summary>
/// 중앙 로그 기록기입니다. 게임 세션 동안 발생하는 핵심 이벤트와 Unity 로그를 비동기 파일로 기록합니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - 비동기(백그라운드 스레드) 파일 기록을 통한 성능 보장
/// - Unity 콘솔 로그(Application.logMessageReceived) 및 핵심 게임 이벤트(EventBus)를 기록
///
/// [이벤트 흐름]
/// - Subscribe: StageLoadedEvent, StageGenerateCompleteEvent, WaveStartedEvent, WaveEndedEvent,
///              StageClearedEvent, StageFailedEvent, GameStateChangedEvent, InGameStateChangedEvent
/// - Publish: (없음)
/// </remarks>
public class GameLogger : Singleton<GameLogger>
{
    private string _logFilePath;
    public string LogFilePath => _logFilePath;

    private readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
    private StreamWriter _streamWriter;
    private Thread _writeThread;
    private bool _isRunning = false;

    protected override void Init()
    {
        try
        {
            string logDir = Path.Combine(Application.persistentDataPath, "logs");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

            string fileName = $"GameLog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            _logFilePath = Path.Combine(logDir, fileName);

            _streamWriter = new StreamWriter(_logFilePath, true, Encoding.UTF8) { AutoFlush = true };

            _isRunning = true;
            _writeThread = new Thread(ProcessLogQueue) { IsBackground = true };
            _writeThread.Start();

            Application.logMessageReceived += HandleUnityLog;

            EnqueueLog("=== Game Session Started ===");
            EnqueueLog($"Platform: {Application.platform}, PersistentDataPath: {Application.persistentDataPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameLogger] Failed to initialize logger: {ex}");
        }
    }

    private void OnEnable()
    {
        if (EventBus.Instance == null) return;

        EventBus.Instance.Subscribe<StageLoadedEvent>(OnStageLoaded);
        EventBus.Instance.Subscribe<StageGenerateCompleteEvent>(OnStageGenerated);
        EventBus.Instance.Subscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance.Subscribe<WaveEndedEvent>(OnWaveEnded);
        EventBus.Instance.Subscribe<StageClearedEvent>(OnStageCleared);
        EventBus.Instance.Subscribe<StageFailedEvent>(OnStageFailed);
        EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        EventBus.Instance.Subscribe<InGameStateChangedEvent>(OnInGameStateChanged);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<StageLoadedEvent>(OnStageLoaded);
            EventBus.Instance.Unsubscribe<StageGenerateCompleteEvent>(OnStageGenerated);
            EventBus.Instance.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Instance.Unsubscribe<WaveEndedEvent>(OnWaveEnded);
            EventBus.Instance.Unsubscribe<StageClearedEvent>(OnStageCleared);
            EventBus.Instance.Unsubscribe<StageFailedEvent>(OnStageFailed);
            EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Instance.Unsubscribe<InGameStateChangedEvent>(OnInGameStateChanged);
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Application.logMessageReceived -= HandleUnityLog;

        // 스레드 종료 및 파일 스트림 닫기
        _isRunning = false;
        if (_writeThread != null && _writeThread.IsAlive)
        {
            _writeThread.Join(500); // 스레드가 종료될 때까지 최대 0.5초 대기
        }

        _streamWriter?.Dispose();
    }

    public void Log(string message)
    {
        string formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Debug.Log(formatted);
        EnqueueLog(formatted);
    }

    private void EnqueueLog(string message)
    {
        _logQueue.Enqueue(message);
    }

    private void ProcessLogQueue()
    {
        while (_isRunning)
        {
            if (_logQueue.TryDequeue(out string log))
            {
                try
                {
                    _streamWriter?.WriteLine(log);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameLogger] Failed to write log in background thread: {ex}");
                }
            }
            else
            {
                Thread.Sleep(10);
            }
        }

        // 스레드 종료 시 남아있는 큐 마저 쓰기
        while (_logQueue.TryDequeue(out string log))
        {
            try
            {
                _streamWriter?.WriteLine(log);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameLogger] Failed to write log during shutdown: {ex}");
            }
        }
    }

    private void HandleUnityLog(string logString, string stackTrace, LogType type)
    {
        if (!string.IsNullOrEmpty(logString) && logString.StartsWith("[")) return;

        string formatted = $"[{DateTime.Now:HH:mm:ss}] [{type}] {logString}";
        if (type == LogType.Error || type == LogType.Exception)
            formatted += $"\n{stackTrace}";

        EnqueueLog(formatted);
    }

    // ---------------------- Event handlers ----------------------
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Log($"SceneLoaded: {scene.name} (mode={mode})");
    private void OnStageLoaded(StageLoadedEvent evt) => Log($"StageLoaded: index={evt.StageIndex}");
    private void OnStageGenerated(StageGenerateCompleteEvent evt) => Log("StageGenerateComplete: grid and objects placement finished");
    private void OnWaveStarted(WaveStartedEvent evt) => Log($"WaveStarted: index={evt.WaveIndex}");
    private void OnWaveEnded(WaveEndedEvent evt) => Log($"WaveEnded: isWin={evt.IsWin}");
    private void OnStageCleared(StageClearedEvent evt) => Log($"StageCleared: index={evt.StageIndex}, isFinal={evt.IsFinalStage}");
    private void OnStageFailed(StageFailedEvent evt) => Log($"StageFailed: index={evt.StageIndex}");
    private void OnGameStateChanged(GameStateChangedEvent evt) => Log($"GameStateChanged: {evt.NewState}");
    private void OnInGameStateChanged(InGameStateChangedEvent evt) => Log($"InGameStateChanged: {evt.NewState}");
}
