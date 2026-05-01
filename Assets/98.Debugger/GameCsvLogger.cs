using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central CSV logger.
/// Usage:
/// GameCsvLogger.Instance.LogEvent(
///     eventType: GameLogEventType.DamageDealt,
///     actor: attackerGameObject,
///     target: targetGameObject,
///     value: damageValue,
///     metadata: new Dictionary<string, object> { { "attackType", "Direct" } });
///
/// CSV sample:
/// 2026-04-29T15:21:10.1200000+09:00,abc123,,,,GameStart,,,,,,,0,,,scene=InGame
/// 2026-04-29T15:21:13.0100000+09:00,abc123,,0,,StageStart,,,,,,,0,,,stageIndex=0
/// 2026-04-29T15:21:40.2200000+09:00,abc123,,0,0,UnitPlaced,,,,,,,40,,,unitKey=2;unitName=Archer;gridX=10;gridY=2
/// 2026-04-29T15:21:43.9100000+09:00,abc123,,0,0,DamageDealt,go_101,Arrow,Player,unit_12_302,EnemyCore,Enemy,18.5,12.1,4.0,areaType=Single
/// 2026-04-29T15:21:55.0000000+09:00,abc123,,0,0,WaveEnd,,,,,,,0,,,stageIndex=0;waveIndex=0;isWin=True
/// </summary>
public class GameCsvLogger : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private bool loggingEnabled = true;
    [SerializeField] private bool writeHeader = true;
    [SerializeField] private string fileNamePrefix = "game_log";

    [Header("Buffer")]
    [SerializeField] private int flushInterval = 100;

    [Header("Category Toggles")]
    [SerializeField] private bool enableGameFlowLog = true;
    [SerializeField] private bool enableResourceLog = true;
    [SerializeField] private bool enableBuildLog = true;
    [SerializeField] private bool enableCombatLog = true;
    [SerializeField] private bool enableSkillLog = true;
    [SerializeField] private bool enableUILog = true;

    private const string CsvHeader = "timestamp,session_id,run_id,stage_id,wave_id,event_type,actor_id,actor_name,actor_team,target_id,target_name,target_team,value,position_x,position_y,metadata";

    private static GameCsvLogger _instance;
    public static GameCsvLogger Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = FindFirstObjectByType<GameCsvLogger>();
            if (_instance != null) return _instance;

            var go = new GameObject("GameCsvLogger");
            _instance = go.AddComponent<GameCsvLogger>();
            DontDestroyOnLoad(go);
            return _instance;
        }
    }

    private readonly List<string> _buffer = new List<string>(256);
    private readonly object _sync = new object();
    private static readonly Encoding Utf8BomEncoding = new UTF8Encoding(true);

    private string _sessionId;
    private string _filePath;
    private bool _initialized;
    private bool _eventsSubscribed;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        TrySubscribeEvents();
    }

    private void OnDisable()
    {
        Flush();
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        UnsubscribeEvents();
    }

    private void OnApplicationQuit()
    {
        Flush();
    }

    private void OnDestroy()
    {
        Flush();
    }

    [ContextMenu("Test GameStart Log")]
    public void TestGameStartLog()
    {
        LogEvent(GameLogEventType.GameStart, metadata: new Dictionary<string, object>
        {
            { "source", "context_menu" }
        });
    }

    [ContextMenu("Test Damage Log")]
    public void TestDamageLog()
    {
        LogEvent(GameLogEventType.DamageDealt, actor: gameObject, target: null, value: 25f,
            metadata: new Dictionary<string, object>
            {
                { "attackType", "Test" },
                { "isCritical", false },
                { "projectileId", "dummy_01" }
            });
    }

    [ContextMenu("Test Resource Log")]
    public void TestResourceLog()
    {
        LogEvent(GameLogEventType.ResourceGain, value: 10f, metadata: new Dictionary<string, object>
        {
            { "reason", "context_menu" }
        });
    }

    public void LogEvent(
        GameLogEventType eventType,
        GameObject actor = null,
        GameObject target = null,
        float value = 0f,
        Dictionary<string, object> metadata = null)
    {
        if (!loggingEnabled) return;
        if (!IsEventEnabled(eventType)) return;

        InitializeIfNeeded();
        TrySubscribeEvents();

        EntitySnapshot actorInfo = BuildEntitySnapshot(actor);
        EntitySnapshot targetInfo = BuildEntitySnapshot(target);

        float posX = !float.IsNaN(actorInfo.PositionX) ? actorInfo.PositionX : targetInfo.PositionX;
        float posY = !float.IsNaN(actorInfo.PositionY) ? actorInfo.PositionY : targetInfo.PositionY;

        string row = string.Join(",", new[]
        {
            EscapeCsv(DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture)),
            EscapeCsv(_sessionId),
            EscapeCsv(GameLogContext.RunId),
            EscapeCsv(GameLogContext.StageId),
            EscapeCsv(GameLogContext.WaveId),
            EscapeCsv(eventType.ToString()),
            EscapeCsv(actorInfo.Id),
            EscapeCsv(actorInfo.Name),
            EscapeCsv(actorInfo.Team),
            EscapeCsv(targetInfo.Id),
            EscapeCsv(targetInfo.Name),
            EscapeCsv(targetInfo.Team),
            EscapeCsv(value.ToString(CultureInfo.InvariantCulture)),
            EscapeCsv(float.IsNaN(posX) ? string.Empty : posX.ToString(CultureInfo.InvariantCulture)),
            EscapeCsv(float.IsNaN(posY) ? string.Empty : posY.ToString(CultureInfo.InvariantCulture)),
            EscapeCsv(SerializeMetadata(metadata))
        });

        lock (_sync)
        {
            _buffer.Add(row);
            if (_buffer.Count >= Mathf.Max(1, flushInterval))
            {
                FlushNoLock();
            }
        }
    }

    public void RecordIncomeZoneBonusChanged(
        string bonusType,
        int zoneIndex,
        string zoneName,
        int occupiedCells,
        float previousBonus,
        float newBonus,
        float bonusPerCell)
    {
        LogEvent(GameLogEventType.IncomeZoneBonusChanged, value: newBonus, metadata: new Dictionary<string, object>
        {
            { "bonusType", bonusType },
            { "zoneIndex", zoneIndex },
            { "zoneName", zoneName },
            { "occupiedCells", occupiedCells },
            { "previousBonus", previousBonus },
            { "newBonus", newBonus },
            { "delta", newBonus - previousBonus },
            { "bonusPerCell", bonusPerCell }
        });
    }

    public void Flush()
    {
        lock (_sync)
        {
            FlushNoLock();
        }
    }

    private void InitializeIfNeeded()
    {
        if (_initialized) return;

        _sessionId = Guid.NewGuid().ToString("N");
        string safePrefix = string.IsNullOrWhiteSpace(fileNamePrefix) ? "game_log" : fileNamePrefix.Trim();
        string fileName = safePrefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv";
        string logDirectory = ResolveLogDirectory();
        _filePath = Path.Combine(logDirectory, fileName);

        Directory.CreateDirectory(logDirectory);

        if (writeHeader)
        {
            File.WriteAllText(_filePath, CsvHeader + Environment.NewLine, Utf8BomEncoding);
        }
        else
        {
            // Create the file with UTF-8 BOM so spreadsheet tools detect encoding reliably.
            using (var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                if (stream.Length == 0)
                {
                    byte[] bom = Utf8BomEncoding.GetPreamble();
                    stream.Write(bom, 0, bom.Length);
                }
            }
        }

        _initialized = true;

        LogEvent(GameLogEventType.GameStart, metadata: new Dictionary<string, object>
        {
            { "scene", SceneManager.GetActiveScene().name }
        });
    }

    private static string ResolveLogDirectory()
    {
        try
        {
#if UNITY_EDITOR
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrWhiteSpace(projectRoot))
                return Path.Combine(projectRoot, "Logs");
#else
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDir))
                return Path.Combine(baseDir, "Logs");
#endif
        }
        catch
        {
            // Fall back below.
        }

        return Application.persistentDataPath;
    }

    private void FlushNoLock()
    {
        if (_buffer.Count == 0) return;

        try
        {
            File.AppendAllLines(_filePath, _buffer, Utf8BomEncoding);
            _buffer.Clear();
        }
        catch (Exception ex)
        {
            Debug.LogError("[GameCsvLogger] Flush failed: " + ex.Message);
        }
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        Flush();
    }

    private void TrySubscribeEvents()
    {
        if (_eventsSubscribed) return;
        if (EventBus.Instance == null) return;

        EventBus.Instance.Subscribe<StageLoadedEvent>(OnStageLoaded);
        EventBus.Instance.Subscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance.Subscribe<WaveEndedEvent>(OnWaveEnded);
        EventBus.Instance.Subscribe<StageClearedEvent>(OnStageCleared);
        EventBus.Instance.Subscribe<StageFailedEvent>(OnStageFailed);
        EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        EventBus.Instance.Subscribe<StageMapNodeSelectedEvent>(OnStageMapNodeSelected);
        EventBus.Instance.Subscribe<StageMapRewardAppliedEvent>(OnStageMapRewardApplied);
        _eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_eventsSubscribed) return;
        if (EventBus.Instance == null) return;

        EventBus.Instance.Unsubscribe<StageLoadedEvent>(OnStageLoaded);
        EventBus.Instance.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance.Unsubscribe<WaveEndedEvent>(OnWaveEnded);
        EventBus.Instance.Unsubscribe<StageClearedEvent>(OnStageCleared);
        EventBus.Instance.Unsubscribe<StageFailedEvent>(OnStageFailed);
        EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        EventBus.Instance.Unsubscribe<StageMapNodeSelectedEvent>(OnStageMapNodeSelected);
        EventBus.Instance.Unsubscribe<StageMapRewardAppliedEvent>(OnStageMapRewardApplied);
        _eventsSubscribed = false;
    }

    private void OnStageLoaded(StageLoadedEvent evt)
    {
        LogEvent(GameLogEventType.StageStart, metadata: new Dictionary<string, object> { { "stageIndex", evt.StageIndex } });
    }

    private void OnWaveStarted(WaveStartedEvent evt)
    {
        LogEvent(GameLogEventType.WaveStart, metadata: new Dictionary<string, object> { { "stageIndex", evt.StageIndex }, { "waveIndex", evt.WaveIndex } });
    }

    private void OnWaveEnded(WaveEndedEvent evt)
    {
        LogEvent(GameLogEventType.WaveEnd, metadata: new Dictionary<string, object> { { "stageIndex", evt.StageIndex }, { "waveIndex", evt.WaveIndex }, { "isWin", evt.IsWin } });
    }

    private void OnStageCleared(StageClearedEvent evt)
    {
        LogEvent(GameLogEventType.StageEnd, metadata: new Dictionary<string, object> { { "stageIndex", evt.StageIndex }, { "isFinalStage", evt.IsFinalStage }, { "result", "clear" } });
    }

    private void OnStageFailed(StageFailedEvent evt)
    {
        LogEvent(GameLogEventType.StageEnd, metadata: new Dictionary<string, object> { { "stageIndex", evt.StageIndex }, { "result", "failed" } });
    }

    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        if (evt.NewState == GameState.Paused)
            LogEvent(GameLogEventType.Pause);
        else if (evt.NewState == GameState.Playing)
            LogEvent(GameLogEventType.Resume);
        else if (evt.NewState == GameState.GameOver || evt.NewState == GameState.GameClear || evt.NewState == GameState.Ready)
            LogEvent(GameLogEventType.GameEnd, metadata: new Dictionary<string, object> { { "state", evt.NewState.ToString() } });
    }

    private void OnStageMapNodeSelected(StageMapNodeSelectedEvent evt)
    {
        LogEvent(GameLogEventType.MapNodeSelected, metadata: new Dictionary<string, object>
        {
            { "nodeId", evt.NodeId },
            { "stageIndex", evt.StageIndex }
        });
    }

    private void OnStageMapRewardApplied(StageMapRewardAppliedEvent evt)
    {
        LogEvent(GameLogEventType.RewardSelected, value: evt.Amount, metadata: new Dictionary<string, object>
        {
            { "nodeId", evt.NodeId },
            { "rewardType", evt.RewardType.ToString() },
            { "rewardId", evt.RewardId },
            { "displayName", evt.DisplayName }
        });
    }

    private bool IsEventEnabled(GameLogEventType eventType)
    {
        switch (eventType)
        {
            case GameLogEventType.GameStart:
            case GameLogEventType.GameEnd:
            case GameLogEventType.StageStart:
            case GameLogEventType.StageEnd:
            case GameLogEventType.WaveStart:
            case GameLogEventType.WaveEnd:
            case GameLogEventType.Pause:
            case GameLogEventType.Resume:
                return enableGameFlowLog;

            case GameLogEventType.ResourceGain:
            case GameLogEventType.ResourceSpend:
            case GameLogEventType.SacrificeGain:
            case GameLogEventType.SacrificeSpend:
            case GameLogEventType.IncomeZoneBonusChanged:
                return enableResourceLog;

            case GameLogEventType.UnitPlaced:
            case GameLogEventType.UnitRemoved:
            case GameLogEventType.FacilityPlaced:
            case GameLogEventType.FacilityRemoved:
            case GameLogEventType.BuildStarted:
            case GameLogEventType.BuildCompleted:
            case GameLogEventType.BuildCancelled:
                return enableBuildLog;

            case GameLogEventType.AttackStarted:
            case GameLogEventType.ProjectileSpawned:
            case GameLogEventType.ProjectileHit:
            case GameLogEventType.DamageDealt:
            case GameLogEventType.DamageReceived:
            case GameLogEventType.UnitKilled:
            case GameLogEventType.StructureDestroyed:
            case GameLogEventType.RamStarted:
            case GameLogEventType.RamHit:
            case GameLogEventType.RamEnded:
                return enableCombatLog;

            case GameLogEventType.SkillUsed:
            case GameLogEventType.RitualUsed:
            case GameLogEventType.BuffApplied:
            case GameLogEventType.BuffExpired:
                return enableSkillLog;

            case GameLogEventType.ButtonClicked:
            case GameLogEventType.ShopOpened:
            case GameLogEventType.ShopClosed:
            case GameLogEventType.MapNodeSelected:
            case GameLogEventType.RewardSelected:
            case GameLogEventType.DoctrineSelected:
                return enableUILog;

            default:
                return true;
        }
    }

    private static string EscapeCsv(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        bool mustQuote = input.Contains(",") || input.Contains("\n") || input.Contains("\r") || input.Contains("\"");
        if (!mustQuote) return input;

        string escaped = input.Replace("\"", "\"\"");
        return "\"" + escaped + "\"";
    }

    private static string SerializeMetadata(Dictionary<string, object> metadata)
    {
        if (metadata == null || metadata.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        bool first = true;

        foreach (var pair in metadata)
        {
            if (!first) sb.Append(';');
            first = false;

            string key = EscapeMetadataToken(pair.Key ?? string.Empty);
            string value = EscapeMetadataToken(pair.Value != null ? pair.Value.ToString() : string.Empty);
            sb.Append(key).Append('=').Append(value);
        }

        return sb.ToString();
    }

    private static string EscapeMetadataToken(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input
            .Replace("\\", "\\\\")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace(";", "\\;")
            .Replace("=", "\\=")
            .Replace("\"", "\\\"");
    }

    private static EntitySnapshot BuildEntitySnapshot(GameObject go)
    {
        if (go == null)
            return EntitySnapshot.Empty;

        string id = string.Empty;
        string name = go.name;
        string team = "Neutral";
        float x = go.transform.position.x;
        float y = go.transform.position.y;

        LoggableEntity loggable = go.GetComponentInParent<LoggableEntity>();
        if (loggable != null)
        {
            id = loggable.EntityId;
            if (!string.IsNullOrWhiteSpace(loggable.DisplayName)) name = loggable.DisplayName;
            if (!string.IsNullOrWhiteSpace(loggable.Team)) team = loggable.Team;
        }

        Unit unit = go.GetComponentInParent<Unit>();
        if (unit != null)
        {
            UnitDataSO data = unit.Data;
            if (string.IsNullOrWhiteSpace(id) && data != null)
                id = "unit_" + data.Key + "_" + unit.GetInstanceID();

            if (data != null && !string.IsNullOrWhiteSpace(data.UnitName))
                name = data.UnitName;

            team = unit.Team.ToString();
        }

        if (string.IsNullOrWhiteSpace(id))
            id = "go_" + go.GetInstanceID();

        return new EntitySnapshot(id, name, team, x, y);
    }

    private readonly struct EntitySnapshot
    {
        public static readonly EntitySnapshot Empty = new EntitySnapshot(string.Empty, string.Empty, string.Empty, float.NaN, float.NaN);

        public readonly string Id;
        public readonly string Name;
        public readonly string Team;
        public readonly float PositionX;
        public readonly float PositionY;

        public EntitySnapshot(string id, string name, string team, float x, float y)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Team = team ?? string.Empty;
            PositionX = x;
            PositionY = y;
        }
    }
}
