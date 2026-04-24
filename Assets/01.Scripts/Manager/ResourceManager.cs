using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 게임 내 핵심 재화(Mouse) 및 시스템 활성화를 관리하는 경제 매니저입니다.
/// </summary>
/// <remarks>
/// [최적화 사항]
/// - Dirty Flag 패턴: 자원 변경 시 즉시 UI를 갱신하지 않고, 프레임 끝에 한 번만 갱신하여 CPU 부하를 방지합니다.
/// </remarks>
[DefaultExecutionOrder(-100)]
public class ResourceManager : Singleton<ResourceManager>
{
    [Header("Resource Settings")]
    [SerializeField] private int _currentMouseCount = 10;
    [SerializeField] private int _maxMouseCount = 500;

    [Header("Generation & Consumption")]
    [SerializeField] private float _genInterval = 1.0f; // 생성 주기
    [SerializeField] private float _subInterval = 1.0f; // 소비 주기

    private int _generatorCount = 0;  // 생성기(발전기) 개수
    private int _activeAltarCount = 0; // 활성화된 스펠/제단 개수
    private float _genTimer;
    private float _subTimer;
    private bool _isDirty = false; // UI 갱신이 필요한지 체크하는 플래그

    [Header("State")]
    private bool _isAltarSupportEnabled = true;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _countDisplayText;
    [SerializeField] private Slider _genGaugeSlider;
    [SerializeField] private Slider _subGaugeSlider;

    public int CurrentMouse => _currentMouseCount;
    public bool IsAltarSupportEnabled => _isAltarSupportEnabled;

    protected override void Awake()
    {
        base.Awake();
        _isDirty = true; // 시작 시 UI 초기 갱신 예약
    }

    private void Update()
    {
        // 웨이브 진행 중이 아니면 자원 로직 정지
        //if (StageManager.Instance == null || GameFlowManager.Instance.CurrentInGameState != InGameState.WavePlaying) return;

        HandleGeneration();
        HandleConsumption();
    }

    private void LateUpdate()
    {
        // 이번 프레임에 자원 수치가 변했다면 한 번만 UI를 업데이트
        if (_isDirty)
        {
            UpdateUI();
            _isDirty = false;
        }
    }

    #region 핵심 로직 (Generation / Consumption)

    private void HandleGeneration()
    {
        if (_generatorCount <= 0 || _currentMouseCount >= _maxMouseCount)
        {
            if (_genGaugeSlider != null) _genGaugeSlider.value = 0;
            return;
        }

        _genTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(_genTimer / _genInterval);
        if (_genGaugeSlider != null) _genGaugeSlider.value = progress;

        if (_genTimer >= _genInterval)
        {
            _genTimer = 0;
            AddMouseCount(_generatorCount); // 자원 추가
        }
    }

    private void HandleConsumption()
    {
        if (_activeAltarCount <= 0)
        {
            _subTimer = 0;
            if (_subGaugeSlider != null) _subGaugeSlider.value = 0;
            _isAltarSupportEnabled = true;
            return;
        }

        _subTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(_subTimer / _subInterval);
        if (_subGaugeSlider != null) _subGaugeSlider.value = progress;

        if (_subTimer >= _subInterval)
        {
            _subTimer = 0;
            // 자원 소모 체크
            if (_currentMouseCount >= _activeAltarCount)
            {
                SubtractMouseCount(_activeAltarCount);
                _isAltarSupportEnabled = true;
            }
            else
            {
                _isAltarSupportEnabled = false; // 자원 부족 시 버프 비활성화
            }
        }
    }
    #endregion

    #region 외부 인터페이스 (API)

    public void AddMouseCount(int amount)
    {
        int before = _currentMouseCount;
        _currentMouseCount = Mathf.Min(_currentMouseCount + amount, _maxMouseCount);
        _isDirty = true; // 변경됨을 알림
        Debug.Log($"[Resource] + {amount} | {before} -> {_currentMouseCount} / {_maxMouseCount}");
    }

    public bool SubtractMouseCount(int amount)
    {
        if (_currentMouseCount < amount)
        {
            Debug.Log($"[Resource] 자원 부족 | 보유: {_currentMouseCount} / 필요: {amount}");
            return false;
        }

        int before = _currentMouseCount;
        _currentMouseCount -= amount;
        _isDirty = true; // 변경됨을 알림
        Debug.Log($"[Resource] -{amount} | {before} → {_currentMouseCount} / {_maxMouseCount}");
        return true;
    }

    // TODO: 나중에 생성기 유닛이 추가되면 이 메서드들을 호출하게 됩니다.
    public void AddGenerator(int count) => _generatorCount += count;
    public void SubtractGenerator(int count) => _generatorCount = Mathf.Max(0, _generatorCount - count);

    public void AddActiveSpell(int count) => _activeAltarCount += count;
    public void SubtractActiveSpell(int count) => _activeAltarCount = Mathf.Max(0, _activeAltarCount - count);

    #endregion

    private void UpdateUI()
    {
        if (_countDisplayText != null)
            _countDisplayText.text = $"남은쥐 : {_currentMouseCount} / {_maxMouseCount}";
    }
}