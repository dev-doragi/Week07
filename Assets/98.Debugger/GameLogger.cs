using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using Unity.Services.Core;
//using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UnityConsent;

/// <summary>
/// 파일 로그 기록 및 Unity Analytics 연동을 담당하는 로거 매니저입니다.
/// </summary>
[DefaultExecutionOrder(-200)]
public class GameLogger : Singleton<GameLogger>
{
    private const string ANALYTICS_CONSENT_PREF_KEY = "analytics_consent_v1";

    private string _logFilePath;
    public string LogFilePath => _logFilePath;

    private readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
    private StreamWriter _streamWriter;
    private Thread _writeThread;
    private bool _isRunning;

    private int _mainThreadId;
    private string _analyticsSessionId;
    private bool _isAnalyticsInitialized;
    private bool _isAnalyticsConsentGranted;

    public bool IsAnalyticsConsentGranted => PlayerPrefs.GetInt(ANALYTICS_CONSENT_PREF_KEY, 0) == 1;

    protected override void OnBootstrap()
    {
        try
        {
            // 1. 파일 로그 시스템 초기화
            InitializeFileSystem();

            // 2. 백그라운드 쓰레드 시작
            _isRunning = true;
            _writeThread = new Thread(ProcessLogQueue) { IsBackground = true };
            _writeThread.Start();

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _analyticsSessionId = Guid.NewGuid().ToString("N");

            // 3. 유니티 콜백 및 이벤트 구독 (DDOL이므로 여기서 1회만 수행)
            Application.logMessageReceived += HandleUnityLog;
            SceneManager.sceneLoaded += OnSceneLoaded;

            SubscribeEvents();

            Log("=== Game Session Started ===");
            Log($"Platform: {Application.platform}, PersistentDataPath: {Application.persistentDataPath}");

            // 4. Analytics 초기화
            _ = InitializeAnalyticsAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameLogger] Failed to initialize logger: {ex}");
        }
    }

    private void InitializeFileSystem()
    {
        string logDir = Path.Combine(Application.persistentDataPath, "logs");
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        string fileName = $"GameLog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
        _logFilePath = Path.Combine(logDir, fileName);
        _streamWriter = new StreamWriter(_logFilePath, true, Encoding.UTF8) { AutoFlush = true };
    }

    private void SubscribeEvents()
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
    }

    protected override void OnDestroy()
    {
        Application.logMessageReceived -= HandleUnityLog;
        SceneManager.sceneLoaded -= OnSceneLoaded;

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

        _isRunning = false;
        if (_writeThread != null && _writeThread.IsAlive)
        {
            _writeThread.Join(500);
        }

        _streamWriter?.Dispose();

        base.OnDestroy();
    }

    #region Logging Logic
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
                try { _streamWriter?.WriteLine(log); }
                catch (Exception ex) { Debug.LogError($"[GameLogger] Write Error: {ex}"); }
            }
            else
            {
                Thread.Sleep(10);
            }
        }

        // 종료 시 남은 로그 처리
        while (_logQueue.TryDequeue(out string pendingLog))
        {
            _streamWriter?.WriteLine(pendingLog);
        }
    }

    private void HandleUnityLog(string logString, string stackTrace, LogType type)
    {
        if (!string.IsNullOrEmpty(logString) && logString.StartsWith("[")) return;

        string formatted = $"[{DateTime.Now:HH:mm:ss}] [{type}] {logString}";
        if (type == LogType.Error || type == LogType.Exception)
        {
            formatted += $"\n{stackTrace}";
        }

        EnqueueLog(formatted);
    }
    #endregion

    #region Analytics Logic
    public void SetAnalyticsConsent(bool granted)
    {
        PlayerPrefs.SetInt(ANALYTICS_CONSENT_PREF_KEY, granted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyAnalyticsConsent(granted);
        Log($"[ANALYTICS] Consent {(granted ? "Granted" : "Denied")}");
    }

    private async Task InitializeAnalyticsAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();
            _isAnalyticsInitialized = true;
            ApplyAnalyticsConsent(IsAnalyticsConsentGranted);
            Log("[ANALYTICS] UGS initialized.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameLogger] Analytics Init Failed: {ex}");
        }
    }

    private void ApplyAnalyticsConsent(bool granted)
    {
        try
        {
            ConsentState consentState = EndUserConsent.GetConsentState();
            consentState.AnalyticsIntent = granted ? ConsentStatus.Granted : ConsentStatus.Denied;
            EndUserConsent.SetConsentState(consentState);
            _isAnalyticsConsentGranted = granted;
        }
        catch (Exception ex) { Debug.LogWarning($"[GameLogger] Consent Apply Error: {ex}"); }
    }

    private void TrackAnalyticsEvent(string eventName, Dictionary<string, object> parameters)
    {
        if (!_isAnalyticsInitialized || !_isAnalyticsConsentGranted) return;
        if (Thread.CurrentThread.ManagedThreadId != _mainThreadId) return;

        try
        {
            CustomEvent customEvent = new CustomEvent(eventName);
            customEvent["gl_session_id"] = _analyticsSessionId;
            customEvent["gl_client_ts_ms"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            customEvent["gl_scene_name"] = SceneManager.GetActiveScene().name;

            if (parameters != null)
            {
                foreach (var kv in parameters) customEvent[kv.Key] = kv.Value;
            }

            AnalyticsService.Instance.RecordEvent(customEvent);
        }
        catch (Exception ex) { Debug.LogWarning($"[GameLogger] Record Event Error '{eventName}': {ex}"); }
    }
    #endregion

    #region Event Handlers
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrackAnalyticsEvent("scene_loaded", new Dictionary<string, object> { { "scene_name", scene.name } });
        Log($"[SCENE] Load Completed >> {scene.name}");
    }

    private void OnStageLoaded(StageLoadedEvent evt) => Log($"[STAGE] Load Started >> Index: {evt.StageIndex}");
    private void OnStageGenerated(StageGenerateCompleteEvent evt) => Log("[STAGE] Generation Completed.");
    private void OnWaveStarted(WaveStartedEvent evt) => Log($"[WAVE] Started >> Index: {evt.WaveIndex}");
    private void OnWaveEnded(WaveEndedEvent evt) => Log($"[WAVE] Ended >> Result: {(evt.IsWin ? "SUCCESS" : "FAILED")}");
    private void OnStageCleared(StageClearedEvent evt) => Log($"[RESULT] Stage Cleared >> Index: {evt.StageIndex}");
    private void OnStageFailed(StageFailedEvent evt) => Log($"[RESULT] Stage Failed >> Index: {evt.StageIndex}");
    private void OnGameStateChanged(GameStateChangedEvent evt) => Log($"[SYSTEM] Global State: {evt.NewState}");
    private void OnInGameStateChanged(InGameStateChangedEvent evt) => Log($"[SYSTEM] In-Game Flow: {evt.NewState}");
    #endregion
}