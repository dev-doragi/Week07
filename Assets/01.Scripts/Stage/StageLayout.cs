using UnityEngine;

/// <summary>
/// 스테이지의 씬 구성 요소 참조만 보유하는 껍데기 클래스.
/// 실제 게임 로직은 StageManager가 담당합니다.
/// </summary>
public class StageLayout : MonoBehaviour
{
    [Header("Player (Ally) Point")]
    [SerializeField] private Transform _allyBasePoint;

    [Header("Enemy Siege Point")]
    [SerializeField] private Transform _enemySiegePoint;

    public Transform AllyBasePoint => _allyBasePoint;
    public Transform EnemySiegePoint => _enemySiegePoint;

    private GameObject _currentEnemySiege;
    public GameObject CurrentEnemySiege => _currentEnemySiege;

    private void Awake()
    {
        if (_enemySiegePoint == null)
            Debug.LogError("[StageLayout] 적 공성병기 소환 위치(_enemySiegePoint)가 할당되지 않았습니다.");

        if (_allyBasePoint == null)
            Debug.LogError("[StageLayout] 아군 본진 위치(_allyBasePoint)가 할당되지 않았습니다.");
    }

    /// <summary>
    /// StageManager가 웨이브 시작 시 호출하여 적 공성병기를 스폰합니다.
    /// </summary>
    public void SpawnEnemy(WaveData waveData)
    {
        if (waveData.EnemySiegePrefab == null)
        {
            Debug.LogWarning("[StageLayout] 스폰할 적 프리팹이 없습니다!");
            return;
        }

        if (_enemySiegePoint == null)
        {
            Debug.LogError("[StageLayout] 적 스폰 위치가 설정되지 않았습니다!");
            return;
        }

        if (_currentEnemySiege != null)
        {
            Destroy(_currentEnemySiege);
        }

        _currentEnemySiege = Instantiate(
            waveData.EnemySiegePrefab,
            _enemySiegePoint.position,
            _enemySiegePoint.rotation,
            transform
        );

        Unit[] units = _currentEnemySiege.GetComponentsInChildren<Unit>(includeInactive: true);
        if (units.Length > 0)
        {
            foreach (Unit unit in units)
            {
                unit.InitializeRuntime();
                Debug.Log($"[StageLayout] Unit 초기화 완료: {unit.name} / Team: {unit.Team} / Category: {unit.Category}");
            }
        }
        else
        {
            Debug.LogWarning($"[StageLayout] 스폰된 오브젝트에 Unit 컴포넌트가 없습니다: {_currentEnemySiege.name}");
        }

        Debug.Log($"[StageLayout] 적 스폰 완료: {waveData.EnemySiegePrefab.name} at {_enemySiegePoint.position} / 총 {units.Length}개 Unit 초기화");
    }

    /// <summary>
    /// StageManager가 프리팹을 씬에 생성한 직후에 호출하여 씬의 정보를 주입합니다.
    /// </summary>
    public void InitLayout(Vector3 gridOriginPos)
    {
        if (_allyBasePoint != null)
        {
            _allyBasePoint.position = gridOriginPos;
        }
    }

    private void OnDestroy()
    {
        if (_currentEnemySiege != null)
        {
            Destroy(_currentEnemySiege);
        }
    }
}