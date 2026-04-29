using System.Collections;
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
    private int _dropRatRewardFeedbackAmount;
    private Coroutine _dropRatRewardTextRoutine;
    private bool _isDirty = false; // UI 갱신이 필요한지 체크하는 플래그

    [Header("State")]
    private bool _isAltarSupportEnabled = true;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _countDisplayText;
    [SerializeField] private TextMeshProUGUI _waveWaitDisplayText;
    [SerializeField] private Slider _genGaugeSlider;
    [SerializeField] private Slider _subGaugeSlider;

    [Header("Drop Rat Reward Feedback")]
    [SerializeField] private TextMeshProUGUI _dropRatRewardText;
    [SerializeField] private TMP_FontAsset _dropRatRewardFont;
    [SerializeField] private Color _dropRatRewardTextColor = new Color(1f, 0.9f, 0.25f, 1f);
    [SerializeField, Min(1f)] private float _dropRatRewardTextSize = 32f;
    [SerializeField, Min(0.1f)] private float _dropRatRewardTextDuration = 1f;
    [SerializeField] private Vector2 _dropRatRewardTextOffset = new Vector2(0f, 32f);
    [SerializeField] private float _dropRatRewardTextRiseDistance = 24f;

    public int CurrentMouse => _currentMouseCount;
    public bool IsAltarSupportEnabled => _isAltarSupportEnabled;

    protected override void Awake()
    {
        base.Awake();
        EnsureWaveWaitDisplay();
        EnsureDropRatRewardText();
        _isDirty = true; // 시작 시 UI 초기 갱신 예약
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_dropRatRewardFont == null)
            _dropRatRewardFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/10.Fonts/Galmuri9 SDF.asset");
    }
#endif

    private void OnEnable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<WaveWaitTimerTickEvent>(OnWaveWaitTimerTick);
            EventBus.Instance.Subscribe<WaveStartedEvent>(OnWaveStarted);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<WaveWaitTimerTickEvent>(OnWaveWaitTimerTick);
            EventBus.Instance.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
        }

        if (_dropRatRewardTextRoutine != null)
        {
            StopCoroutine(_dropRatRewardTextRoutine);
            _dropRatRewardTextRoutine = null;
        }

        _dropRatRewardFeedbackAmount = 0;

        if (_dropRatRewardText != null)
            _dropRatRewardText.gameObject.SetActive(false);
    }

    private void Update()
    {
        // TODO: 웨이브 진행 중이 아니면 자원 로직 정지, 현재는 디버깅 용으로 주석 처리해놓음!!
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
        //Debug.Log($"[Resource] -{amount} | {before} → {_currentMouseCount} / {_maxMouseCount}");
        return true;
    }

    // TODO: 나중에 생성기 유닛이 추가되면 이 메서드들을 호출하게 됩니다.
    public void AddGenerator(int count) => _generatorCount += count;
    public void SubtractGenerator(int count) => _generatorCount = Mathf.Max(0, _generatorCount - count);

    public void AddActiveSpell(int count) => _activeAltarCount += count;
    public void SubtractActiveSpell(int count) => _activeAltarCount = Mathf.Max(0, _activeAltarCount - count);

    #endregion

    public void ShowDropRatRewardFeedback(int rewardAmount)
    {
        if (rewardAmount <= 0)
            return;

        EnsureDropRatRewardText();
        if (_dropRatRewardText == null)
            return;

        _dropRatRewardFeedbackAmount += rewardAmount;

        if (_dropRatRewardTextRoutine != null)
            StopCoroutine(_dropRatRewardTextRoutine);

        _dropRatRewardTextRoutine = StartCoroutine(DropRatRewardTextRoutine());
    }

    private void UpdateUI()
    {
        if (_countDisplayText != null)
            _countDisplayText.text = $"남은쥐 : {_currentMouseCount} / {_maxMouseCount}";
    }

    private void OnWaveWaitTimerTick(WaveWaitTimerTickEvent evt)
    {
        EnsureWaveWaitDisplay();

        if (_waveWaitDisplayText == null)
            return;

        float remainingTime = Mathf.Max(0f, evt.RemainingTime);
        _waveWaitDisplayText.gameObject.SetActive(remainingTime > 0f);
        _waveWaitDisplayText.text = $"Wave Start : {Mathf.CeilToInt(remainingTime)}";
    }

    private void OnWaveStarted(WaveStartedEvent evt)
    {
        if (_waveWaitDisplayText != null)
            _waveWaitDisplayText.gameObject.SetActive(false);
    }

    [ContextMenu("Build Wave Wait UI")]
    public void BuildWaveWaitUI()
    {
        EnsureWaveWaitDisplay();
        EnsureDropRatRewardText();

        if (_waveWaitDisplayText != null && !Application.isPlaying)
            _waveWaitDisplayText.gameObject.SetActive(true);
    }

    private void EnsureWaveWaitDisplay()
    {
        if (_waveWaitDisplayText != null || _countDisplayText == null)
            return;

        RectTransform countRect = _countDisplayText.transform as RectTransform;
        if (countRect == null)
            return;

        Transform existing = countRect.parent != null ? countRect.parent.Find("WaveWaitText") : null;
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI existingText))
        {
            _waveWaitDisplayText = existingText;
            _waveWaitDisplayText.gameObject.SetActive(false);
            return;
        }

        GameObject displayObj = new GameObject("WaveWaitText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform displayRect = displayObj.GetComponent<RectTransform>();
        displayRect.SetParent(countRect.parent, false);
        displayRect.anchorMin = countRect.anchorMin;
        displayRect.anchorMax = countRect.anchorMax;
        displayRect.pivot = countRect.pivot;
        displayRect.sizeDelta = countRect.sizeDelta;
        displayRect.anchoredPosition = countRect.anchoredPosition + new Vector2(0f, -32f);

        _waveWaitDisplayText = displayObj.GetComponent<TextMeshProUGUI>();
        _waveWaitDisplayText.font = _countDisplayText.font;
        _waveWaitDisplayText.fontSize = Mathf.Max(18f, _countDisplayText.fontSize * 0.8f);
        _waveWaitDisplayText.alignment = _countDisplayText.alignment;
        _waveWaitDisplayText.color = _countDisplayText.color;
        _waveWaitDisplayText.raycastTarget = false;
        _waveWaitDisplayText.text = string.Empty;
        _waveWaitDisplayText.gameObject.SetActive(false);
    }

    private void EnsureDropRatRewardText()
    {
        if (_dropRatRewardText != null || _countDisplayText == null)
            return;

        RectTransform countRect = _countDisplayText.transform as RectTransform;
        if (countRect == null)
            return;

        Transform existing = countRect.parent != null ? countRect.parent.Find("DropRatRewardText") : null;
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI existingText))
        {
            _dropRatRewardText = existingText;
            ApplyDropRatRewardTextStyle();
            _dropRatRewardText.gameObject.SetActive(false);
            return;
        }

        GameObject displayObj = new GameObject("DropRatRewardText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform displayRect = displayObj.GetComponent<RectTransform>();
        displayRect.SetParent(countRect.parent, false);
        displayRect.anchorMin = countRect.anchorMin;
        displayRect.anchorMax = countRect.anchorMax;
        displayRect.pivot = countRect.pivot;
        displayRect.sizeDelta = countRect.sizeDelta;
        displayRect.anchoredPosition = countRect.anchoredPosition + _dropRatRewardTextOffset;

        _dropRatRewardText = displayObj.GetComponent<TextMeshProUGUI>();
        ApplyDropRatRewardTextStyle();
        _dropRatRewardText.text = string.Empty;
        _dropRatRewardText.gameObject.SetActive(false);
    }

    private void ApplyDropRatRewardTextStyle()
    {
        if (_dropRatRewardText == null)
            return;

        if (_dropRatRewardFont != null)
            _dropRatRewardText.font = _dropRatRewardFont;
        else if (_countDisplayText != null)
            _dropRatRewardText.font = _countDisplayText.font;

        _dropRatRewardText.fontSize = _dropRatRewardTextSize;
        _dropRatRewardText.alignment = TextAlignmentOptions.Center;
        _dropRatRewardText.color = _dropRatRewardTextColor;
        _dropRatRewardText.raycastTarget = false;
        _dropRatRewardText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private IEnumerator DropRatRewardTextRoutine()
    {
        RectTransform rewardRect = _dropRatRewardText.transform as RectTransform;
        RectTransform countRect = _countDisplayText != null ? _countDisplayText.transform as RectTransform : null;
        if (rewardRect == null || countRect == null)
            yield break;

        ApplyDropRatRewardTextStyle();

        Vector2 start = countRect.anchoredPosition + _dropRatRewardTextOffset;
        Vector2 end = start + Vector2.up * _dropRatRewardTextRiseDistance;
        Color baseColor = _dropRatRewardTextColor;
        float duration = Mathf.Max(0.1f, _dropRatRewardTextDuration);
        float elapsed = 0f;

        rewardRect.anchoredPosition = start;
        _dropRatRewardText.text = $"+ {_dropRatRewardFeedbackAmount}";
        _dropRatRewardText.color = baseColor;
        _dropRatRewardText.gameObject.SetActive(true);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            rewardRect.anchoredPosition = Vector2.Lerp(start, end, t);
            Color fadeColor = baseColor;
            fadeColor.a *= 1f - t;
            _dropRatRewardText.color = fadeColor;

            yield return null;
        }

        _dropRatRewardText.gameObject.SetActive(false);
        _dropRatRewardFeedbackAmount = 0;
        _dropRatRewardTextRoutine = null;
    }
}
