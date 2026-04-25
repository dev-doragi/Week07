using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mirrors ResourceManager.CurrentMouse by spawning/despawning UI mouse prefabs.
/// </summary>
public class MouseCountVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private GameObject _mousePrefab;
    [SerializeField] private Transform _spawnLocation;
    [SerializeField] private RectTransform _movementBounds;

    [Header("Options")]
    [SerializeField] private bool _despawnAllOnDisable = true;
    [SerializeField] private int _poolInitialSize = 30;
    [SerializeField] private int _poolMaxSize = 500;

    private readonly List<GameObject> _activeMice = new List<GameObject>();
    private int _lastSyncedCount = -1;
    private bool _poolPrepared = false;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        _lastSyncedCount = -1;
    }

    private void Start()
    {
        if (TryPreparePool())
            SyncToResourceCount(force: true);
    }

    private void Update()
    {
        if (!TryPreparePool())
            return;

        SyncToResourceCount(force: false);
    }

    private void OnDisable()
    {
        if (_despawnAllOnDisable)
            DespawnMouse(_activeMice.Count);

        _lastSyncedCount = -1;
        _poolPrepared = false;
    }

    [ContextMenu("Sync Mouse Count")]
    public void ForceSync()
    {
        SyncToResourceCount(force: true);
    }

    private void ResolveDependencies()
    {
        if (_resourceManager == null)
            _resourceManager = ResourceManager.Instance;
    }

    private bool TryPreparePool()
    {
        if (_poolPrepared)
            return true;

        if (_mousePrefab == null)
            return false;

        if (PoolManager.Instance == null)
            return false;

        PoolManager.Instance.BootstrapIfNeeded();
        PoolManager.Instance.CreatePool(_mousePrefab, _poolInitialSize, _poolMaxSize);
        _poolPrepared = true;
        return true;
    }

    private void SyncToResourceCount(bool force)
    {
        if (_resourceManager == null || _mousePrefab == null || _spawnLocation == null)
            return;

        int targetCount = Mathf.Max(0, _resourceManager.CurrentMouse);
        if (!force && targetCount == _lastSyncedCount)
            return;

        int currentCount = CountAliveMice();

        if (targetCount > currentCount)
            SpawnMouse(targetCount - currentCount);
        else if (targetCount < currentCount)
            DespawnMouse(currentCount - targetCount);

        _lastSyncedCount = targetCount;
    }

    private int CountAliveMice()
    {
        for (int i = _activeMice.Count - 1; i >= 0; i--)
        {
            if (_activeMice[i] == null)
                _activeMice.RemoveAt(i);
        }

        return _activeMice.Count;
    }

    private void SpawnMouse(int amount)
    {
        if (amount <= 0)
            return;

        for (int i = 0; i < amount; i++)
        {
            GameObject obj = PoolManager.Instance.Spawn(_mousePrefab.name, _spawnLocation.position, Quaternion.identity);
            if (obj == null)
                continue;

            _activeMice.Add(obj);

            if (obj.TryGetComponent(out MouseAgent agent))
                agent.Setup(_movementBounds);
        }
    }

    private void DespawnMouse(int amount)
    {
        if (amount <= 0)
            return;

        int count = Mathf.Min(amount, _activeMice.Count);
        for (int i = 0; i < count; i++)
        {
            int lastIndex = _activeMice.Count - 1;
            GameObject target = _activeMice[lastIndex];
            _activeMice.RemoveAt(lastIndex);

            if (target != null)
                PoolManager.Instance.Despawn(target);
        }
    }
}
