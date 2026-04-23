using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[DefaultExecutionOrder(-97)]
public class PoolManager : Singleton<PoolManager>
{
    private readonly Dictionary<string, IObjectPool<GameObject>> _pools = new();

    [Header("Pool Setup")]
    [SerializeField] private Transform _poolRoot;
    [SerializeField] private RectTransform _uiPoolRoot;

    [Header("Global Pool Setup")]
    [Tooltip("게임 내내 쓰일 공통 투사체와 이펙트를 여기에 한 번만 등록하세요")]
    [SerializeField] private List<PoolSetupData> _globalPools = new List<PoolSetupData>();

    protected override void OnBootstrap()
    {
        if (_poolRoot == null)
        {
            GameObject rootObj = new GameObject("PoolRoot");
            DontDestroyOnLoad(rootObj);
            _poolRoot = rootObj.transform;
        }

        if (_uiPoolRoot == null)
        {
            //GameObject uiRootObj = new GameObject("UI_PoolRoot", typeof(Canvas), typeof(RectTransform));
            //uiRootObj.GetComponent<Canvas>().enabled = false; // 렌더링 방지
            //DontDestroyOnLoad(uiRootObj);
            //_uiPoolRoot = uiRootObj.GetComponent<RectTransform>();
        }

        foreach (var setup in _globalPools)
        {
            if (setup.Prefab != null)
            {
                CreatePool(setup.Prefab, setup.InitialSize, setup.MaxSize);
            }
        }
    }

    public void CreatePool(GameObject prefab, int initialSize, int maxSize = 100)
    {
        if (prefab == null)
        {
            Debug.LogError("[PoolManager] 생성할 프리팹이 null입니다.");
            return;
        }

        string key = prefab.name;
        if (_pools.ContainsKey(key)) return;

        // 1. UI 여부 판별 및 타겟 루트 결정
        bool isUI = prefab.GetComponent<RectTransform>() != null;

        if (isUI && _uiPoolRoot == null)
        {
            Debug.LogWarning($"[PoolManager] '{prefab.name}'은 UI 프리팹이지만 _uiPoolRoot가 설정되지 않았습니다. 풀링을 스킵합니다.");
            return;
        }

        Transform targetRoot = isUI ? _uiPoolRoot : _poolRoot;

        IObjectPool<GameObject> pool = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                // [수정] _poolRoot가 아니라 계산된 targetRoot를 사용해야 함
                GameObject obj = Instantiate(prefab, targetRoot);
                obj.name = prefab.name;
                return obj;
            },
            actionOnGet: obj => obj.SetActive(true),
            actionOnRelease: obj => obj.SetActive(false),
            actionOnDestroy: obj => Destroy(obj),
            collectionCheck: false,
            defaultCapacity: initialSize,
            maxSize: maxSize
        );

        _pools.Add(key, pool);

        // Prewarm (동일)
        if (initialSize > 0)
        {
            GameObject[] prewarmObjects = new GameObject[initialSize];
            for (int i = 0; i < initialSize; i++) prewarmObjects[i] = pool.Get();
            for (int i = 0; i < initialSize; i++) pool.Release(prewarmObjects[i]);
        }
    }

    public GameObject Spawn(string prefabName, Vector3 position, Quaternion rotation)
    {
        if (!_pools.TryGetValue(prefabName, out var pool))
        {
            Debug.LogError($"[PoolManager] '{prefabName}'에 해당하는 풀이 존재하지 않습니다. 먼저 CreatePool을 호출하세요.");
            return null;
        }

        GameObject obj = pool.Get();
        obj.transform.SetPositionAndRotation(position, rotation);
        return obj;
    }

    public void Despawn(GameObject obj)
    {
        string key = obj.name;
        if (!_pools.TryGetValue(key, out var pool))
        {
            Debug.LogError($"[PoolManager] '{key}' 오브젝트를 반환할 풀이 없습니다. 파괴 처리합니다.");
            Destroy(obj);
            return;
        }

        pool.Release(obj);

        bool isUI = obj.GetComponent<RectTransform>() != null;
        obj.transform.SetParent(isUI ? _uiPoolRoot : _poolRoot);
    }

    public void ClearAllPools()
    {
        foreach (var pool in _pools.Values)
        {
            pool.Clear(); // 내부 객체 Destroy
        }
        _pools.Clear();
    }
}