using UnityEngine;

[DefaultExecutionOrder(-100)]
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    [SerializeField] private bool _isDontDestroyOnLoad = true;

    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogError($"{typeof(T).Name} 인스턴스가 씬에 존재하지 않습니다.");
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this as T;

        if (_isDontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        ManagerRegistry.Register<T>(_instance);

        Init();
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    protected virtual void Init() { }
}