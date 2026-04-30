using System.Collections;
using UnityEngine;

/// <summary>
/// 튜토리얼 적 소환 모듈 (자동 스폰 모드만 지원)
/// </summary>
public class EnemySpawnModule : ITutorialModule
{
    private EnemySpawnModuleConfig _config;
    private StageManager _stageManager;
    private bool _isAutoSpawn = true;

    public void Initialize(TutorialStep step)
    {
        _config = step.EnemySpawnConfig;
        _stageManager = StageManager.Instance;
        if (_config != null)
            _isAutoSpawn = _config.AutoSpawn;
    }

    public IEnumerator Execute()
    {
        if (!_isAutoSpawn)
            yield break;
        if (_config == null || _config.TutorialStageData == null)
            yield break;
        if (_stageManager == null)
        {
            Debug.LogError("[EnemySpawnModule] StageManager.Instance가 없습니다.");
            yield break;
        }
        if (_config.TutorialStageData.StageLayoutPrefab == null)
        {
            Debug.LogError("[EnemySpawnModule] TutorialStageData에 StageLayoutPrefab이 없습니다.");
            yield break;
        }
        int stageIndex = _config.TutorialStageData.StageIndex;
        if (_stageManager.CurrentStageIndex != stageIndex || _stageManager.CurrentLayout == null)
        {
            _stageManager.LoadStage(stageIndex);
            yield return null;
        }
        int waitCount = 0;
        while (_stageManager.CurrentLayout == null && waitCount < 30)
        {
            yield return null;
            waitCount++;
        }
        if (_stageManager.CurrentLayout == null)
        {
            Debug.LogError("[EnemySpawnModule] StageManager.CurrentLayout이 없습니다.");
            yield break;
        }
        int waveIndex = Mathf.Max(0, _config.TutorialWaveIndex);
        _stageManager.StartWave(waveIndex);
        yield break;
    }

    public void Cleanup()
    {
        // 필요시 정리
    }
}
