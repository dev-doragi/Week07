using System.Collections;
using UnityEngine;

/// <summary>
/// 일정 주기로 Income 그리드를 스캔해 점유 칸 수 기반 자원을 생산하고 ResourceManager에 반영한다.
/// </summary>
public class IncomeResourceProducer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private IncomeGridBoard _gridBoard;
    [SerializeField] private ResourceManager _resourceManager;

    [Header("Production")]
    [SerializeField] private float _scanInterval = 5f;
    [Min(4)]
    [SerializeField] private int _resourcePerCell = 4;
    [SerializeField] private bool _useUnscaledTime;
    [SerializeField] private bool _logProduction = true;

    public int TotalProduced { get; private set; }
    public int LastProduced { get; private set; }

    private Coroutine _scanRoutine;
    private bool _warnedMissingResourceManager;

    private void OnEnable()
    {
        if (_resourcePerCell < 4)
            _resourcePerCell = 4;

        StartScanning();
    }

    private void OnDisable()
    {
        StopScanning();
    }

    [ContextMenu("Produce Once")]
    public void ProduceOnce()
    {
        if (_gridBoard == null)
            return;

        int occupied = _gridBoard.GetOccupiedCellCount();
        int amount = occupied * Mathf.Max(1, _resourcePerCell);

        LastProduced = amount;
        if (amount <= 0)
            return;

        TotalProduced += amount;

        var manager = ResolveResourceManager();
        if (manager != null)
        {
            // 한번이라도 연결되면 누락 경고 상태는 해제한다.
            _warnedMissingResourceManager = false;

            int before = manager.CurrentMouse;
            manager.AddMouseCount(amount);
            if (_logProduction)
            {
                Debug.Log($"[IncomeResourceProducer] ResourceManager linked. Mouse: {before} -> {manager.CurrentMouse}");
            }
        }
        else if (!_warnedMissingResourceManager)
        {
            _warnedMissingResourceManager = true;
            Debug.LogWarning("[IncomeResourceProducer] ResourceManager was not found. Produced amount is not applied.");
        }

        if (_logProduction)
        {
            Debug.Log($"[IncomeResourceProducer] Produced {amount} resources (occupied: {occupied}, perCell: {_resourcePerCell}).");
        }
    }

    public void SetGridBoard(IncomeGridBoard gridBoard)
    {
        _gridBoard = gridBoard;
    }

    public void SetResourceManager(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public void StartScanning()
    {
        if (!isActiveAndEnabled || _scanRoutine != null)
            return;

        _scanRoutine = StartCoroutine(ScanRoutine());
    }

    public void StopScanning()
    {
        if (_scanRoutine == null)
            return;

        StopCoroutine(_scanRoutine);
        _scanRoutine = null;
    }

    private IEnumerator ScanRoutine()
    {
        while (true)
        {
            if (_useUnscaledTime)
                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, _scanInterval));
            else
                yield return new WaitForSeconds(Mathf.Max(0.1f, _scanInterval));

            ProduceOnce();
        }
    }

    private ResourceManager ResolveResourceManager()
    {
        if (_resourceManager != null)
            return _resourceManager;

        _resourceManager = FindSceneObject<ResourceManager>();
        return _resourceManager;
    }

    private static T FindSceneObject<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>();
    }
}
