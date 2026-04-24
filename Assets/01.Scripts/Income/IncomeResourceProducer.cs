using System.Collections;
using UnityEngine;

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

#pragma warning disable CS0618
        _resourceManager = FindObjectOfType<ResourceManager>();
#pragma warning restore CS0618

        return _resourceManager;
    }
}
