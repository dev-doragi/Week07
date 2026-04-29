using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// 튜토리얼 적 소환 모듈
/// - 튜토리얼 웨이브 데이터를 기반으로 적 소환
/// - 모든 적이 죽을 때까지 대기
/// - 여러 사이클 반복 가능
/// </summary>
public class EnemySpawnModule : ITutorialModule
{
    private EnemySpawnModuleConfig _config;
    private int _cyclesCompleted = 0;

    public void Initialize(TutorialStep step)
    {
        _config = step.EnemySpawnConfig;
        _cyclesCompleted = 0;
    }

    public IEnumerator Execute()
    {
        if (_config == null || !(_config.EnemySpawnCycles > 0))
        {
            yield break;
        }

        StageLayout layout = StageManager.Instance?.CurrentLayout;
        if (layout == null)
        {
            Debug.LogError("[EnemySpawnModule] CurrentLayout이 없습니다. StageLayout이 필요합니다.");
            yield break;
        }

        if (_config.EnemySiegePrefab == null)
        {
            Debug.LogError("[EnemySpawnModule] EnemySiegePrefab이 할당되지 않았습니다.");
            yield break;
        }

        int cycles = Mathf.Max(1, _config.EnemySpawnCycles);

        for (int i = 0; i < cycles; i++)
        {
            // 웨이브 시작 이벤트
            EventBus.Instance?.Publish(new WaveStartedEvent());

            // 적 소환 - StageLayout의 SpawnEnemy 메서드 직접 사용
            // 이 경우 EnemySiegePrefab을 직접 전달할 수 있는 메서드가 필요함
            // 임시로 GameObject를 생성
            GameObject enemyGO = Object.Instantiate(_config.EnemySiegePrefab, layout.transform);

            // 스폰된 적 유닛에 튜토리얼 플래그 설정
            Unit[] units = enemyGO.GetComponentsInChildren<Unit>(true);
            foreach (Unit unit in units)
            {
                unit.SetAsTutorialEnemy();
            }

            // 적이 파괴될 때까지 대기
            while (enemyGO != null)
            {
                yield return null;
            }

            // 웨이브 종료 이벤트
            EventBus.Instance?.Publish(new WaveEndedEvent());

            yield return new WaitForSecondsRealtime(0.2f);
            _cyclesCompleted++;
        }
    }

    public void Cleanup()
    {
        // 남은 적이 있으면 정리 (필요시)
    }
}
