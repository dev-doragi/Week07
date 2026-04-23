using UnityEngine;

/// <summary>
/// 재단(Altar) 유닛의 특수 기능을 관리하며 ResourceManager와 연동합니다.
/// </summary>
public class AltarConnector : MonoBehaviour
{
    [SerializeField] private int _count = 1; // 매 틱 당 추가 소모되는 쥐 개수

    private bool _isRegistered;

    public bool IsAltarActive
    {
        get
        {
            if (ResourceManager.Instance == null) return false;
            return ResourceManager.Instance.IsAltarSupportEnabled;
        }
    }

    private void Start()
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError($"[{name}] ResourceManager 인스턴스가 씬에 존재하지 않습니다.");
        }
    }

    private void OnEnable()
    {
        TryRegister();
    }

    private void OnDisable()
    {
        TryUnregister();
    }

    private void OnDestroy()
    {
        TryUnregister();
    }

    private void TryRegister()
    {
        if (_isRegistered || ResourceManager.Instance == null) return;

        // ResourceManager의 'ActiveSpell' 카운트를 증가시켜 틱당 소모량을 설정
        ResourceManager.Instance.AddActiveSpell(_count);
        _isRegistered = true;
    }

    private void TryUnregister()
    {
        if (!_isRegistered || ResourceManager.Instance == null) return;

        ResourceManager.Instance.SubtractActiveSpell(_count);
        _isRegistered = false;
    }
}