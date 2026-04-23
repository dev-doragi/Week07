using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UnityConsent;

[DefaultExecutionOrder(-200)]
public class GameLogger : Singleton<GameLogger>
{
    private const string AnalyticsConsentPrefKey = "analytics_consent_v1";

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

    public bool IsAnalyticsConsentGranted => PlayerPrefs.GetInt(AnalyticsConsentPrefKey, 0) == 1;

    protected override void OnBootstrap()
    {
        try
        {
            string logDir = Path.Combine(Application.persistentDataPath, "logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            string fileName = $"GameLog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            _logFilePath = Path.Combine(logDir, fileName);

            _streamWriter = new StreamWriter(_logFilePath, true, Encoding.UTF8) { AutoFlush = true };

            _isRunning = true;
            _writeThread = new Thread(ProcessLogQueue) { IsBackground = true };
            _writeThread.Start();

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _analyticsSessionId = Guid.NewGuid().ToString("N");

            Application.logMessageReceived += HandleUnityLog;

            EnqueueLog("=== Game Session Started ===");
            EnqueueLog($"Platform: {Application.platform}, PersistentDataPath: {Application.persistentDataPath}");

            _ = InitializeAnalyticsAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameLogger] Failed to initialize logger: {ex}");
        }
    }

    private void OnEnable()
    {
        if (EventBus.Instance == null)
        {
            return;
        }

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

        _isRunning = false;
        if (_writeThread != null && _writeThread.IsAlive)
        {
            _writeThread.Join(500);
        }

        _streamWriter?.Dispose();
    }

    public void Log(string message)
    {
        string formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Debug.Log(formatted);
        EnqueueLog(formatted);
    }

    public void SetAnalyticsConsent(bool granted)
    {
        PlayerPrefs.SetInt(AnalyticsConsentPrefKey, granted ? 1 : 0);
        PlayerPrefs.Save();

        ApplyAnalyticsConsent(granted);
        Log($"[ANALYTICS] Consent {(granted ? "Granted" : "Denied")}");
    }

    public void RequestAnalyticsDataDeletion()
    {
        SetAnalyticsConsent(false);

        try
        {
            if (_isAnalyticsInitialized)
            {
                AnalyticsService.Instance.RequestDataDeletion();
                Log("[ANALYTICS] Data deletion requested.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameLogger] Failed to request analytics data deletion: {ex}");
        }
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

        while (_logQueue.TryDequeue(out string pendingLog))
        {
            try
            {
                _streamWriter?.WriteLine(pendingLog);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameLogger] Failed to write log during shutdown: {ex}");
            }
        }
    }

    private void HandleUnityLog(string logString, string stackTrace, LogType type)
    {
        if (!string.IsNullOrEmpty(logString) && logString.StartsWith("["))
        {
            return;
        }

        string formatted = $"[{DateTime.Now:HH:mm:ss}] [{type}] {logString}";
        if (type == LogType.Error || type == LogType.Exception)
        {
            formatted += $"\n{stackTrace}";
        }

        EnqueueLog(formatted);
    }

    private async Task InitializeAnalyticsAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();
            _isAnalyticsInitialized = true;

            bool storedConsentGranted = IsAnalyticsConsentGranted;
            ApplyAnalyticsConsent(storedConsentGranted);
            Log($"[ANALYTICS] UGS initialized. Consent: {(storedConsentGranted ? "Granted" : "Denied")}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameLogger] Failed to initialize analytics service: {ex}");
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
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameLogger] Failed to apply analytics consent state: {ex}");
        }
    }

    private void TrackAnalyticsEvent(string eventName, Dictionary<string, object> parameters)
    {
        if (!_isAnalyticsInitialized || !_isAnalyticsConsentGranted)
        {
            return;
        }

        if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
        {
            Debug.LogWarning($"[GameLogger] Analytics event '{eventName}' skipped because it was not called on the main thread.");
            return;
        }

        try
        {
            CustomEvent customEvent = new CustomEvent(eventName);

            customEvent["gl_session_id"] = _analyticsSessionId;
            customEvent["gl_client_ts_ms"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            customEvent["gl_app_version"] = Application.version;
            customEvent["gl_platform"] = Application.platform.ToString();
            customEvent["gl_scene_name"] = SceneManager.GetActiveScene().name;

            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    customEvent[kv.Key] = kv.Value;
                }
            }

            AnalyticsService.Instance.RecordEvent(customEvent);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameLogger] Failed to record analytics event '{eventName}': {ex}");
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrackAnalyticsEvent("scene_loaded", new Dictionary<string, object>
        {
            { "scene_name", scene.name },
            { "load_mode", mode.ToString() }
        });

        Log($"[SCENE] Load Completed >> Name: {scene.name} | Mode: {mode}");
    }

    private void OnStageLoaded(StageLoadedEvent evt)
    {
        TrackAnalyticsEvent("stage_load_started", new Dictionary<string, object>
        {
            { "stage_index", evt.StageIndex }
        });

        Log($"[STAGE] Load Process Started >> Target Index: {evt.StageIndex}");
    }

    private void OnStageGenerated(StageGenerateCompleteEvent evt)
    {
        int stageIndex = StageManager.Instance != null ? StageManager.Instance.CurrentStageIndex : -1;

        TrackAnalyticsEvent("stage_generation_completed", new Dictionary<string, object>
        {
            { "stage_index", stageIndex }
        });

        Log("[STAGE] Generation Completed >> All Grid and Objects are placed in the scene.");
    }

    private void OnWaveStarted(WaveStartedEvent evt)
    {
        int stageIndex = StageManager.Instance != null ? StageManager.Instance.CurrentStageIndex : -1;

        TrackAnalyticsEvent("wave_started", new Dictionary<string, object>
        {
            { "stage_index", stageIndex },
            { "wave_index", evt.WaveIndex }
        });

        Log($"[WAVE] Battle Started >> Current Wave Index: {evt.WaveIndex}");
    }

    private void OnWaveEnded(WaveEndedEvent evt)
    {
        int stageIndex = StageManager.Instance != null ? StageManager.Instance.CurrentStageIndex : -1;
        int waveIndex = StageManager.Instance != null ? StageManager.Instance.CurrentWaveIndex : -1;

        TrackAnalyticsEvent("wave_ended", new Dictionary<string, object>
        {
            { "stage_index", stageIndex },
            { "wave_index", waveIndex },
            { "is_win", evt.IsWin }
        });

        Log($"[WAVE] Battle Ended >> Result: {(evt.IsWin ? "SUCCESS" : "FAILED")}");
    }

    private void OnStageCleared(StageClearedEvent evt)
    {
        TrackAnalyticsEvent("stage_cleared", new Dictionary<string, object>
        {
            { "stage_index", evt.StageIndex },
            { "is_final_stage", evt.IsFinalStage }
        });

        Log($"[RESULT] Stage Cleared >> Index: {evt.StageIndex} | Total Game Clear: {evt.IsFinalStage}");
    }

    private void OnStageFailed(StageFailedEvent evt)
    {
        TrackAnalyticsEvent("stage_failed", new Dictionary<string, object>
        {
            { "stage_index", evt.StageIndex }
        });

        Log($"[RESULT] Stage Failed >> Player defeated at Stage Index: {evt.StageIndex}");
    }

    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        TrackAnalyticsEvent("game_state_changed", new Dictionary<string, object>
        {
            { "new_state", evt.NewState.ToString() }
        });

        Log($"[SYSTEM] Global Game State Changed >> New State: {evt.NewState}");
    }

    private void OnInGameStateChanged(InGameStateChangedEvent evt)
    {
        TrackAnalyticsEvent("ingame_state_changed", new Dictionary<string, object>
        {
            { "new_state", evt.NewState.ToString() }
        });

        Log($"[SYSTEM] In-Game Flow State Changed >> New Flow: {evt.NewState}");
    }
}
