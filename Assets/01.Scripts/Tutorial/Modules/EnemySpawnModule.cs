using System.Collections;
using UnityEngine;

/// <summary>
/// 튜토리얼 적 소환 모듈
/// - StageManager의 기존 스테이지/웨이브 시작 로직을 사용
/// - 튜토리얼 스테이지 데이터만 주입
/// - 완료 조건은 별도 completion module이 담당
/// </summary>
public class EnemySpawnModule : ITutorialModule
{
    private EnemySpawnModuleConfig _config;
    private StageManager _stageManager;

    public void Initialize(TutorialStep step)
    {
        _config = step.EnemySpawnConfig;
        _stageManager = StageManager.Instance;
    }

    public IEnumerator Execute()
    {
        if (_config == null || _config.TutorialStageData == null)
        {
            yield break;
        }

        if (_stageManager == null)
        {
            Debug.LogError("[EnemySpawnModule] StageManager.Instance가 없습니다.");
            yield break;
        }

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
        int waveIndex = Mathf.Max(0, _config.TutorialWaveIndex);

        if (_stageManager.CurrentStageIndex != stageIndex || _stageManager.CurrentLayout == null)
        {
            _stageManager.LoadStage(stageIndex);
            yield return null;
        }

        if (_stageManager.CurrentLayout == null)
        {
            Debug.LogError("[EnemySpawnModule] StageManager.CurrentLayout이 없습니다.");
            yield break;
        }

        _stageManager.StartWave(waveIndex);

        yield break;
    }

    public void Cleanup()
    {
        // 남은 적이 있으면 정리 (필요시)
    }
}
