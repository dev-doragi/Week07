using System.Collections;
using UnityEngine;

/// <summary>
/// Stage layout container: holds spawn points and spawns enemy siege prefab.
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

    private Coroutine _entranceRoutine;

    private void Awake()
    {
        if (_enemySiegePoint == null)
            Debug.LogError("[StageLayout] Enemy siege point is not assigned.");

        if (_allyBasePoint == null)
            Debug.LogError("[StageLayout] Ally base point is not assigned.");
    }

    /// <summary>
    /// Called by StageManager when a wave starts.
    /// </summary>
    public void SpawnEnemy(WaveData waveData)
    {
        if (waveData.EnemySiegePrefab == null)
        {
            Debug.LogWarning("[StageLayout] Enemy siege prefab is missing.");
            return;
        }

        if (_enemySiegePoint == null)
        {
            Debug.LogError("[StageLayout] Enemy siege point is not assigned.");
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
                Debug.Log($"[StageLayout] Unit initialized: {unit.name} / Team: {unit.Team} / Category: {unit.Category}");
            }
        }
        else
        {
            Debug.LogWarning($"[StageLayout] Spawned enemy has no Unit component: {_currentEnemySiege.name}");
        }

        EnemyGridManager enemyGrid = _currentEnemySiege.GetComponentInChildren<EnemyGridManager>(true);
        if (enemyGrid != null)
        {
            enemyGrid.RegisterExistingUnitsFromChildren();
        }
        float offscreenX = Camera.main.orthographicSize * Camera.main.aspect * 1.2f;
        _currentEnemySiege.transform.position += Vector3.right * offscreenX;

        if(_entranceRoutine != null) StopCoroutine(_entranceRoutine);
        _entranceRoutine = StartCoroutine(EnemyEntranceRoutine(_currentEnemySiege, _enemySiegePoint.position));
        Debug.Log($"[StageLayout] Enemy spawned: {waveData.EnemySiegePrefab.name} at {_enemySiegePoint.position} / total units: {units.Length}");
    }

    private IEnumerator EnemyEntranceRoutine(GameObject enemy, Vector3 targetPos)
    {
        float duration = 2f;
        Vector3 startPos = enemy.transform.position;
        float elapsed = 0f;

        while(elapsed < duration)
        {
            if(enemy == null) yield break;

            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 2f);
            enemy.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        if(enemy != null)
            enemy.transform.position = targetPos;

        _entranceRoutine = null;
    }

    /// <summary>
    /// Inject scene-specific values after layout instantiate.
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
        if(_entranceRoutine != null) StopCoroutine(_entranceRoutine);
        if (_currentEnemySiege != null)
        {
            Destroy(_currentEnemySiege);
        }
    }
}

