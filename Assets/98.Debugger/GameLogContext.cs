using UnityEngine;

public static class GameLogContext
{
    public static string RunId { get; set; } = string.Empty;
    public static string StageId
    {
        get
        {
            StageManager stageManager = Object.FindFirstObjectByType<StageManager>();
            if (stageManager == null)
                return string.Empty;
            return stageManager.CurrentStageIndex.ToString();
        }
    }

    public static string WaveId
    {
        get
        {
            StageManager stageManager = Object.FindFirstObjectByType<StageManager>();
            if (stageManager == null)
                return string.Empty;
            return stageManager.CurrentWaveIndex.ToString();
        }
    }
}
