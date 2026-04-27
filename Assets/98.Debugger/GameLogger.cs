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

/// <summary>
/// Current-game statistics logger.
/// Writes local session logs and mirrors compact custom events to Unity Analytics.
/// </summary>
[DefaultExecutionOrder(-200)]
public class GameLogger : Singleton<GameLogger>
{
    private const string AnalyticsConsentPrefKey = "analytics_consent_v1";

    private readonly struct StageStats
    {
        public readonly int Attempts;
        public readonly int Wins;
        public readonly int Losses;

        public StageStats(int attempts, int wins, int losses)
        {
            Attempts = attempts;
            Wins = wins;
            Losses = losses;
        }

        public StageStats WithResult(bool isWin)
        {
            return new StageStats(Attempts + 1, Wins + (isWin ? 1 : 0), Losses + (isWin ? 0 : 1));
        }
    }

    private readonly struct RatTileAnalyticsCount
    {
        public readonly int UnitKey;
        public readonly string UnitName;
        public readonly string UnitCategory;
        public readonly int Count;

        public RatTileAnalyticsCount(int unitKey, string unitName, string unitCategory, int count)
        {
            UnitKey = unitKey;
            UnitName = unitName;
            UnitCategory = unitCategory;
            Count = count;
        }

        public RatTileAnalyticsCount WithIncrement()
        {
            return new RatTileAnalyticsCount(UnitKey, UnitName, UnitCategory, Count + 1);
        }
    }

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

    private readonly Dictionary<int, StageStats> _stageStats = new Dictionary<int, StageStats>();
    private readonly Dictionary<string, int> _doctrineTypeCounts = new Dictionary<string, int>();
    private readonly Dictionary<int, string> _doctrinePathByRow = new Dictionary<int, string>();
    private readonly Dictionary<int, int> _ritualSkillUseCounts = new Dictionary<int, int>();
    private readonly Dictionary<int, Dictionary<string, int>> _ratTileFinalCountsByStage = new Dictionary<int, Dictionary<string, int>>();
    private readonly Dictionary<string, int> _rewardCounts = new Dictionary<string, int>();
    private int _ramUseCount;

    public bool IsAnalyticsConsentGranted => PlayerPrefs.GetInt(AnalyticsConsentPrefKey, 0) == 1;

    protected override void OnBootstrap()
    {
        try
        {
            InitializeFileSystem();

            _isRunning = true;
            _writeThread = new Thread(ProcessLogQueue) { IsBackground = true };
            _writeThread.Start();

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _analyticsSessionId = Guid.NewGuid().ToString("N");

            Application.logMessageReceived += HandleUnityLog;
            SceneManager.sceneLoaded += OnSceneLoaded;

            SubscribeEvents();

            Log("=== Game Statistics Session Started ===");
            Log($"Platform: {Application.platform}, LogFilePath: {_logFilePath}");

            _ = InitializeAnalyticsAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameLogger] Failed to initialize logger: {ex}");
        }
    }

    private void InitializeFileSystem()
    {
        string logDir = GetDefaultLogDirectory();

        try
        {
            Directory.CreateDirectory(logDir);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameLogger] Failed to create build Logs directory '{logDir}'. Falling back to persistentDataPath. Error: {ex}");
            logDir = Path.Combine(Application.persistentDataPath, "Logs");
            Directory.CreateDirectory(logDir);
        }

        string fileName = $"GameLogs_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
        _logFilePath = Path.Combine(logDir, fileName);
        _streamWriter = new StreamWriter(_logFilePath, true, Encoding.UTF8) { AutoFlush = true };
    }

    private static string GetDefaultLogDirectory()
    {
        DirectoryInfo dataDirectory = Directory.GetParent(Application.dataPath);
        string rootPath = dataDirectory != null ? dataDirectory.FullName : Application.persistentDataPath;
        return Path.Combine(rootPath, "Logs");
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
        EventBus.Instance.Subscribe<StageMapRewardAppliedEvent>(OnStageMapRewardApplied);
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
            EventBus.Instance.Unsubscribe<StageMapRewardAppliedEvent>(OnStageMapRewardApplied);
            EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Instance.Unsubscribe<InGameStateChangedEvent>(OnInGameStateChanged);
        }

        LogSessionSummary("session_end");

        _isRunning = false;
        if (_writeThread != null && _writeThread.IsAlive)
        {
            _writeThread.Join(500);
        }

        _streamWriter?.Dispose();

        base.OnDestroy();
    }

    #region Public Record API

    public void RecordDoctrineSelected(DoctrineNodeData data)
    {
        if (data == null) return;

        string doctrineType = data.doctrineType.ToString();
        Increment(_doctrineTypeCounts, doctrineType, 1);
        _doctrinePathByRow[data.rowIndex] = doctrineType;

        string path = BuildDoctrinePath();
        Log($"[DOCTRINE] Selected | Row: {data.rowIndex + 1}, Type: {doctrineType}, NodeId: {data.nodeId}, Path: {path}");

        TrackAnalyticsEvent("doctrine_selected", new Dictionary<string, object>
        {
            { "row_index", data.rowIndex },
            { "row_step", data.rowIndex + 1 },
            { "column_index", data.columnIndex },
            { "doctrine_type", doctrineType },
            { "node_id", SafeString(data.nodeId) },
            { "node_name", SafeString(data.nodeName) },
            { "path", path }
        });
    }

    public void RecordRitualSkillUsed(int skillIndex, string skillId = null)
    {
        Increment(_ritualSkillUseCounts, skillIndex, 1);
        int total = SumValues(_ritualSkillUseCounts);

        Log($"[RITUAL] Skill Used | Skill: {skillIndex}, SkillId: {SafeString(skillId, "RitualSkill" + skillIndex)}, TotalUses: {total}");

        TrackAnalyticsEvent("ritual_skill_used", new Dictionary<string, object>
        {
            { "skill_index", skillIndex },
            { "skill_id", SafeString(skillId, "RitualSkill" + skillIndex) },
            { "skill_session_count", _ritualSkillUseCounts[skillIndex] },
            { "ritual_total_uses", total }
        });
    }

    public void RecordRamUsed(string source = null)
    {
        _ramUseCount += 1;

        Log($"[RAM] Used | Source: {SafeString(source, "Unknown")}, TotalUses: {_ramUseCount}");

        TrackAnalyticsEvent("ram_used", new Dictionary<string, object>
        {
            { "source", SafeString(source, "Unknown") },
            { "ram_total_uses", _ramUseCount }
        });
    }

    #endregion

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
        PlayerPrefs.SetInt(AnalyticsConsentPrefKey, granted ? 1 : 0);
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
        catch (Exception ex) { Debug.LogWarning($"[GameLogger] Record Event Error '{eventName}': {ex}"); }
    }

    #endregion

    #region Event Handlers

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrackAnalyticsEvent("scene_loaded", new Dictionary<string, object>
        {
            { "scene_name", scene.name },
            { "load_mode", mode.ToString() }
        });
        Log($"[SCENE] Load Completed | Scene: {scene.name}");
    }

    private void OnStageLoaded(StageLoadedEvent evt)
    {
        Log($"[STAGE] Loaded | Stage: {evt.StageIndex}");
        TrackAnalyticsEvent("stage_loaded", new Dictionary<string, object> { { "stage_index", evt.StageIndex } });
    }

    private void OnStageGenerated(StageGenerateCompleteEvent evt)
    {
        Log("[STAGE] Generation Completed");
    }

    private void OnWaveStarted(WaveStartedEvent evt)
    {
        Log($"[WAVE] Started | Stage: {evt.StageIndex}, Wave: {evt.WaveIndex}");
    }

    private void OnWaveEnded(WaveEndedEvent evt)
    {
        Log($"[WAVE] Ended | Stage: {evt.StageIndex}, Wave: {evt.WaveIndex}, Result: {(evt.IsWin ? "SUCCESS" : "FAILED")}");
    }

    private void OnStageCleared(StageClearedEvent evt)
    {
        RecordStageResult(evt.StageIndex, true, evt.IsFinalStage);
        LogStageTileSnapshot(evt.StageIndex, true, evt.IsFinalStage);

        LogSessionSummary(evt.IsFinalStage ? "final_stage_cleared" : $"stage_{evt.StageIndex}_cleared");
    }

    private void OnStageFailed(StageFailedEvent evt)
    {
        RecordStageResult(evt.StageIndex, false, false);
        LogStageTileSnapshot(evt.StageIndex, false);
    }

    private void OnStageMapRewardApplied(StageMapRewardAppliedEvent evt)
    {
        string rewardKind = evt.RewardType switch
        {
            StageMapRewardType.ProductionFacility => "IncomeReward",
            StageMapRewardType.RatTowerUnlock => "TowerReward",
            _ => evt.RewardType.ToString()
        };

        Increment(_rewardCounts, rewardKind, 1);

        Log($"[REWARD] Selected | Type: {rewardKind}, Node: {SafeString(evt.NodeId)}, RewardId: {SafeString(evt.RewardId)}, Amount: {evt.Amount}, Total: {_rewardCounts[rewardKind]}");

        TrackAnalyticsEvent("map_reward_selected", new Dictionary<string, object>
        {
            { "node_id", SafeString(evt.NodeId) },
            { "reward_kind", rewardKind },
            { "reward_type", evt.RewardType.ToString() },
            { "reward_id", SafeString(evt.RewardId) },
            { "amount", evt.Amount },
            { "reward_session_count", _rewardCounts[rewardKind] }
        });
    }

    private void OnGameStateChanged(GameStateChangedEvent evt) => Log($"[SYSTEM] Global State: {evt.NewState}");
    private void OnInGameStateChanged(InGameStateChangedEvent evt) => Log($"[SYSTEM] In-Game Flow: {evt.NewState}");

    #endregion

    #region Statistics

    private void RecordStageResult(int stageIndex, bool isWin, bool isFinalStage)
    {
        if (!_stageStats.TryGetValue(stageIndex, out StageStats stats))
        {
            stats = new StageStats(0, 0, 0);
        }

        stats = stats.WithResult(isWin);
        _stageStats[stageIndex] = stats;

        float winRate = stats.Attempts > 0 ? (float)stats.Wins / stats.Attempts : 0f;
        Log($"[STAGE_RESULT] Stage: {stageIndex}, Result: {(isWin ? "WIN" : "LOSE")}, Attempts: {stats.Attempts}, Wins: {stats.Wins}, Losses: {stats.Losses}, SessionWinRate: {winRate:P1}");

        TrackAnalyticsEvent("stage_result", new Dictionary<string, object>
        {
            { "stage_index", stageIndex },
            { "is_win", isWin },
            { "is_final_stage", isFinalStage },
            { "stage_attempts_in_session", stats.Attempts },
            { "stage_wins_in_session", stats.Wins },
            { "stage_losses_in_session", stats.Losses },
            { "stage_win_rate_in_session", winRate }
        });
    }

    private void LogStageTileSnapshot(int stageIndex, bool isStageClear, bool isFinalStage = false)
    {
        GridManager grid = GridManager.Instance;
        if (grid == null)
        {
            Log($"[TILE_SNAPSHOT] Stage: {stageIndex}, GridManager missing");
            return;
        }

        List<PlacedUnit> placedUnits = grid.GetPlacedUnitsSnapshot(includeInitialUnits: false);
        Dictionary<string, int> tileCounts = new Dictionary<string, int>();
        Dictionary<string, RatTileAnalyticsCount> ratTileAnalyticsCounts = new Dictionary<string, RatTileAnalyticsCount>();
        int attackCount = 0;
        int defenseCount = 0;
        int supportCount = 0;
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        StringBuilder layout = new StringBuilder();
        placedUnits.Sort((a, b) =>
        {
            int yCompare = a.OriginCell.y.CompareTo(b.OriginCell.y);
            return yCompare != 0 ? yCompare : a.OriginCell.x.CompareTo(b.OriginCell.x);
        });

        for (int i = 0; i < placedUnits.Count; i++)
        {
            PlacedUnit placed = placedUnits[i];
            if (placed == null || placed.Data == null) continue;

            UnitDataSO data = placed.Data;
            string tileId = BuildTileId(data);
            Increment(tileCounts, tileId, 1);
            IncrementRatTileAnalyticsCount(ratTileAnalyticsCounts, tileId, data);

            if (data.Category == UnitCategory.Attack) attackCount++;
            if (data.Category == UnitCategory.Defense) defenseCount++;
            if (data.Category == UnitCategory.Support) supportCount++;

            minX = Mathf.Min(minX, placed.OriginCell.x);
            minY = Mathf.Min(minY, placed.OriginCell.y);
            maxX = Mathf.Max(maxX, placed.OriginCell.x + data.Size.x - 1);
            maxY = Mathf.Max(maxY, placed.OriginCell.y + data.Size.y - 1);

            if (layout.Length > 0) layout.Append(" | ");
            layout.Append(tileId)
                .Append("@")
                .Append(placed.OriginCell.x)
                .Append(",")
                .Append(placed.OriginCell.y)
                .Append("[")
                .Append(data.Size.x)
                .Append("x")
                .Append(data.Size.y)
                .Append("]");
        }

        string layoutString = layout.Length > 0 ? layout.ToString() : "empty";
        string tileCountString = FormatCounts(tileCounts);
        int layoutWidth = placedUnits.Count > 0 ? maxX - minX + 1 : 0;
        int layoutHeight = placedUnits.Count > 0 ? maxY - minY + 1 : 0;
        string layoutSignature = StableHash(layoutString);

        Log($"[TILE_SNAPSHOT] Stage: {stageIndex}, Result: {(isStageClear ? "CLEAR" : "FAILED")}, UnitCount: {placedUnits.Count}, Counts: {tileCountString}");
        Log($"[TILE_LAYOUT] Stage: {stageIndex}, Signature: {layoutSignature}, Bounds: {layoutWidth}x{layoutHeight}, Layout: {layoutString}");

        if (isStageClear)
        {
            _ratTileFinalCountsByStage[stageIndex] = new Dictionary<string, int>(tileCounts);
            Log($"[RAT_TILE_FINAL] Stage: {stageIndex}, Counts: {tileCountString}");
            TrackRatTileFinalCountEvents(stageIndex, isFinalStage, layoutSignature, placedUnits.Count, ratTileAnalyticsCounts);
        }

        TrackAnalyticsEvent("stage_tile_snapshot", new Dictionary<string, object>
        {
            { "stage_index", stageIndex },
            { "is_stage_clear", isStageClear },
            { "unit_count", placedUnits.Count },
            { "attack_count", attackCount },
            { "defense_count", defenseCount },
            { "support_count", supportCount },
            { "layout_width", layoutWidth },
            { "layout_height", layoutHeight },
            { "layout_signature", layoutSignature },
            { "tile_counts", tileCountString }
        });
    }

    private void TrackRatTileFinalCountEvents(int stageIndex, bool isFinalStage, string layoutSignature, int finalUnitTotal, Dictionary<string, RatTileAnalyticsCount> counts)
    {
        foreach (RatTileAnalyticsCount count in counts.Values)
        {
            TrackAnalyticsEvent("rat_tile_final_count", new Dictionary<string, object>
            {
                { "stage_index", stageIndex },
                { "is_final_stage", isFinalStage },
                { "unit_key", count.UnitKey },
                { "unit_name", count.UnitName },
                { "unit_category", count.UnitCategory },
                { "unit_count", count.Count },
                { "final_unit_total", finalUnitTotal },
                { "layout_signature", layoutSignature }
            });
        }
    }

    private void LogSessionSummary(string reason)
    {
        Log($"=== Game Statistics Summary ({reason}) ===");
        Log($"[SUMMARY] StageResults: {FormatStageStats()}");
        Log($"[SUMMARY] DoctrineTypeCounts: {FormatCounts(_doctrineTypeCounts)}");
        Log($"[SUMMARY] DoctrinePath: {BuildDoctrinePath()}");
        Log($"[SUMMARY] RitualSkillUses: {FormatCounts(_ritualSkillUseCounts)}, Total: {SumValues(_ritualSkillUseCounts)}");
        Log($"[SUMMARY] RatTileStageFinals: {FormatStageTileFinalCounts()}");
        Log($"[SUMMARY] RewardSelections: {FormatCounts(_rewardCounts)}");
        Log($"[SUMMARY] RamUses: {_ramUseCount}");

        TrackAnalyticsEvent("game_statistics_summary", new Dictionary<string, object>
        {
            { "reason", reason },
            { "doctrine_path", BuildDoctrinePath() },
            { "ritual_total_uses", SumValues(_ritualSkillUseCounts) },
            { "ram_total_uses", _ramUseCount },
            { "reward_counts", FormatCounts(_rewardCounts) },
            { "rat_tile_stage_finals", FormatStageTileFinalCounts() }
        });
    }

    #endregion

    #region Helpers

    private string BuildDoctrinePath()
    {
        if (_doctrinePathByRow.Count == 0) return "none";

        List<int> rows = new List<int>(_doctrinePathByRow.Keys);
        rows.Sort();

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < rows.Count; i++)
        {
            if (builder.Length > 0) builder.Append(" - ");
            int row = rows[i];
            builder.Append(row + 1).Append(":").Append(_doctrinePathByRow[row]);
        }

        return builder.ToString();
    }

    private string FormatStageStats()
    {
        if (_stageStats.Count == 0) return "none";

        List<int> stages = new List<int>(_stageStats.Keys);
        stages.Sort();

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < stages.Count; i++)
        {
            if (builder.Length > 0) builder.Append(", ");
            int stage = stages[i];
            StageStats stats = _stageStats[stage];
            float winRate = stats.Attempts > 0 ? (float)stats.Wins / stats.Attempts : 0f;
            builder.Append("Stage")
                .Append(stage)
                .Append("=")
                .Append(stats.Wins)
                .Append("/")
                .Append(stats.Attempts)
                .Append("(")
                .Append((winRate * 100f).ToString("0.#"))
                .Append("%)");
        }

        return builder.ToString();
    }

    private string FormatStageTileFinalCounts()
    {
        if (_ratTileFinalCountsByStage.Count == 0) return "none";

        List<int> stages = new List<int>(_ratTileFinalCountsByStage.Keys);
        stages.Sort();

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < stages.Count; i++)
        {
            if (builder.Length > 0) builder.AppendLine();
            int stage = stages[i];
            builder.Append("Stage")
                .Append(stage)
                .Append("=")
                .Append(FormatCounts(_ratTileFinalCountsByStage[stage]));
        }

        return builder.ToString();
    }

    private static string BuildTileId(UnitDataSO data)
    {
        if (data == null) return "Unknown";
        string name = string.IsNullOrWhiteSpace(data.UnitName) ? data.name : data.UnitName;
        return $"{data.Key}:{name}:{data.Category}";
    }

    private static void IncrementRatTileAnalyticsCount(Dictionary<string, RatTileAnalyticsCount> counts, string tileId, UnitDataSO data)
    {
        if (counts.TryGetValue(tileId, out RatTileAnalyticsCount current))
        {
            counts[tileId] = current.WithIncrement();
            return;
        }

        string name = string.IsNullOrWhiteSpace(data.UnitName) ? data.name : data.UnitName;
        counts.Add(tileId, new RatTileAnalyticsCount(data.Key, name, data.Category.ToString(), 1));
    }

    private static string FormatCounts<TKey>(Dictionary<TKey, int> counts)
    {
        if (counts == null || counts.Count == 0) return "none";

        List<TKey> keys = new List<TKey>(counts.Keys);
        keys.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < keys.Count; i++)
        {
            if (builder.Length > 0) builder.Append(", ");
            TKey key = keys[i];
            builder.Append(key).Append("=").Append(counts[key]);
        }

        return builder.ToString();
    }

    private static void Increment<TKey>(Dictionary<TKey, int> counts, TKey key, int amount)
    {
        if (counts.TryGetValue(key, out int current))
        {
            counts[key] = current + amount;
        }
        else
        {
            counts.Add(key, amount);
        }
    }

    private static int SumValues<TKey>(Dictionary<TKey, int> counts)
    {
        int total = 0;
        foreach (int value in counts.Values)
        {
            total += value;
        }
        return total;
    }

    private static string SafeString(string value, string fallback = "none")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619;
            }
            return hash.ToString("X8");
        }
    }

    #endregion
}
