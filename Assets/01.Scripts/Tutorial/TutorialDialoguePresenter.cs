using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class TutorialDialoguePresenter : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject _dialogPanel;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _dialogText;
    [SerializeField] private Image _portraitImage;
    [SerializeField] private RectTransform _highlighter;
    [SerializeField] private Button _clickButton; // 스크린 전체 판정 버튼 권장
    [SerializeField] private GameObject _skipButton;
    [SerializeField] private GameObject _postPanel;

    [Header("Placement Progress UI")]
    [SerializeField] private GameObject _placementProgressPanel;
    [SerializeField] private TextMeshProUGUI _placementProgressText;

    [Header("Settings")]
    [SerializeField] private float _typingSpeed = 0.05f;
    [SerializeField] private float _pulseScale = 1.1f;
    [SerializeField] private float _pulseSpeed = 4.0f;

    [Header("Portrait Talk Animation")]
    [SerializeField] private Sprite _portraitIdleSprite;          // 기본(입 닫힘)
    [SerializeField] private Sprite _portraitTalkingSprite;       // 말할 때(입 열림)
    [SerializeField, Min(0.03f)] private float _portraitBlinkInterval = 0.12f;

    // State Flags & Cache
    private bool _isTyping = false;
    private bool _cancelTyping = false;
    private string _fullTargetText = "";
    private TutorialStep _currentStepData;

    private Coroutine _typingCoroutine = null;
    private Coroutine _dialogMoveCoroutine = null;
    private Coroutine _pulseCoroutine = null;
    private Coroutine _portraitMoveCoroutine = null;
    private Coroutine _dialogCameraCoroutine = null;
    private Coroutine _portraitTalkCoroutine;

    // Original Position Cache
    private Vector2 _dialogOriginalAnchoredPos = Vector2.zero;
    private Vector2 _highlighterBaseSize = Vector2.zero;
    private Vector2 _clickButtonOriginalAnchoredPos = Vector2.zero;
    private Vector2 _portraitOriginalAnchoredPos = Vector2.zero;
    private bool _portraitOriginalCached = false;

    // Move State Cache (중복 이동 방지용)
    private float _currentDialogOffset = 0f;
    private Vector2 _currentPortraitTargetPos = Vector2.zero;
    private bool _isPortraitMoved = false;
    private Sprite _currentPortraitIdleSprite;

    private void Awake()
    {
        if (_dialogPanel != null)
        {
            _dialogPanel.SetActive(false);
            var rt = _dialogPanel.GetComponent<RectTransform>();
            if (rt != null) _dialogOriginalAnchoredPos = rt.anchoredPosition;
        }

        if (_highlighter != null) _highlighter.gameObject.SetActive(false);
        if (_placementProgressPanel != null) _placementProgressPanel.SetActive(false);
        if (_postPanel != null) _postPanel.SetActive(false);

        if (_portraitImage != null)
        {
            var portraitRt = _portraitImage.GetComponent<RectTransform>();
            if (portraitRt != null)
            {
                _portraitOriginalAnchoredPos = portraitRt.anchoredPosition;
                _currentPortraitTargetPos = _portraitOriginalAnchoredPos;
                _portraitOriginalCached = true;
            }
        }

        if (_clickButton != null)
        {
            var cbRt = _clickButton.GetComponent<RectTransform>();
            if (cbRt != null) _clickButtonOriginalAnchoredPos = cbRt.anchoredPosition;
            _clickButton.onClick.AddListener(OnScreenClicked);
        }
    }

    private void OnEnable()
    {
        if (EventBus.Instance == null)
        {
            Debug.LogError("EventBus.Instance가 존재하지 않습니다.");
            return;
        }

        EventBus.Instance.Subscribe<TutorialStepStartedEvent>(OnStepStarted);
        EventBus.Instance.Subscribe<TutorialProgressUpdatedEvent>(OnProgressUpdated);
        EventBus.Instance.Subscribe<TutorialStepCompletedEvent>(OnStepCompleted);
        EventBus.Instance.Subscribe<TutorialCompletedEvent>(OnTutorialCompleted);
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<TutorialStepStartedEvent>(OnStepStarted);
            EventBus.Instance.Unsubscribe<TutorialProgressUpdatedEvent>(OnProgressUpdated);
            EventBus.Instance.Unsubscribe<TutorialStepCompletedEvent>(OnStepCompleted);
            EventBus.Instance.Unsubscribe<TutorialCompletedEvent>(OnTutorialCompleted);
        }

        StopPortraitTalkAnimation();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && _dialogPanel != null && _dialogPanel.activeSelf)
        {
            OnScreenClicked();
        }
    }

    private void OnScreenClicked()
    {
        if (_isTyping)
        {
            _cancelTyping = true; // 타이핑 스킵
        }
        else
        {
            EventBus.Instance?.Publish(new TutorialNextRequestedEvent());
        }
    }

    private void OnStepStarted(TutorialStepStartedEvent evt)
    {
        _currentStepData = evt.StepData;
        var dialogueConfig = _currentStepData?.DialogueConfig;

        bool hasDialogue = dialogueConfig != null && dialogueConfig.HasDialogue;

        if (_dialogPanel != null) _dialogPanel.SetActive(hasDialogue);

        // 초기화 (대사 없음)
        if (!hasDialogue)
        {
            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
            _isTyping = false;
            _cancelTyping = false;
            _fullTargetText = string.Empty;

            if (_nameText != null) _nameText.text = string.Empty;
            if (_dialogText != null) _dialogText.text = string.Empty;
        }

        // 카메라 고정
        if (_currentStepData.Condition != TutorialCondition.CameraMove)
        {
            if (_dialogCameraCoroutine != null) StopCoroutine(_dialogCameraCoroutine);
            _dialogCameraCoroutine = StartCoroutine(DialogCameraLockRoutine(0.25f));
        }

        // 하이라이터 처리
        var questConfig = _currentStepData?.QuestConfig;
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        if (_highlighter != null)
        {
            if (questConfig?.TargetUI != null)
            {
                _highlighter.gameObject.SetActive(true);
                _highlighter.position = questConfig.TargetUI.position;
                _highlighterBaseSize = questConfig.TargetUI.sizeDelta;
                _highlighter.sizeDelta = _highlighterBaseSize;
                _pulseCoroutine = StartCoroutine(HighlighterPulseRoutine());
            }
            else
            {
                _highlighter.gameObject.SetActive(false);
            }
        }

        // 진행도 패널 표시 여부
        bool showProgress = _currentStepData.Condition == TutorialCondition.PartPlacement
                         || _currentStepData.Condition == TutorialCondition.CameraMove
                         || _currentStepData.Condition == TutorialCondition.EnemyDefeated;
        if (_placementProgressPanel != null) _placementProgressPanel.SetActive(showProgress);
    }

    private void OnProgressUpdated(TutorialProgressUpdatedEvent evt)
    {
        if (_placementProgressText == null) return;

        if (_currentStepData != null && _currentStepData.Condition == TutorialCondition.CameraMove)
        {
            _placementProgressText.text = !string.IsNullOrWhiteSpace(evt.Label) ? evt.Label : "";
            return;
        }

        int current = Mathf.FloorToInt(evt.CurrentProgress);
        int required = Mathf.Max(1, Mathf.FloorToInt(evt.RequiredProgress));

        if (!string.IsNullOrWhiteSpace(evt.Label))
            _placementProgressText.text = $"{evt.Label} ({current}/{required})";
        else
            _placementProgressText.text = $"({current}/{required})";
    }

    private void OnStepCompleted(TutorialStepCompletedEvent evt)
    {
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        if (_dialogCameraCoroutine != null) StopCoroutine(_dialogCameraCoroutine);
        if (_placementProgressPanel != null) _placementProgressPanel.SetActive(false);
    }

    private void OnTutorialCompleted(TutorialCompletedEvent evt)
    {
        if (_dialogPanel != null) _dialogPanel.SetActive(false);
        if (_highlighter != null) _highlighter.gameObject.SetActive(false);
        if (_placementProgressPanel != null) _placementProgressPanel.SetActive(false);
        if (_skipButton != null) _skipButton.SetActive(false);

        if (_portraitImage != null && _portraitOriginalCached)
        {
            var rt = _portraitImage.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = _portraitOriginalAnchoredPos;
        }

        if (_postPanel != null) _postPanel.SetActive(true);
    }

    // --- 애니메이션 코루틴 모음 ---
    private IEnumerator TypingRoutine()
    {
        _isTyping = true;
        _cancelTyping = false;

        if (_dialogText != null) _dialogText.text = "";

        foreach (char c in _fullTargetText.ToCharArray())
        {
            if (_cancelTyping) break;
            if (_dialogText != null) _dialogText.text += c;
            yield return new WaitForSecondsRealtime(_typingSpeed);
        }

        if (_dialogText != null) _dialogText.text = _fullTargetText;
        _isTyping = false;
    }

    private IEnumerator HighlighterPulseRoutine()
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * _pulseSpeed) + 1f) / 2f;
            if (_highlighter != null)
            {
                _highlighter.sizeDelta = Vector2.Lerp(_highlighterBaseSize, _highlighterBaseSize * _pulseScale, t);
            }
            yield return null;
        }
    }

    private IEnumerator MovePortraitToRoutine(RectTransform portraitRt, Vector2 target, float duration)
    {
        if (portraitRt == null) yield break;
        Vector2 start = portraitRt.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            portraitRt.anchoredPosition = Vector2.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        portraitRt.anchoredPosition = target;
    }

    private IEnumerator MoveDialogRoutine(Vector2 targetPos, float duration)
    {
        if (_dialogPanel == null) yield break;
        var rt = _dialogPanel.GetComponent<RectTransform>();
        Vector2 start = rt.anchoredPosition;

        Vector2 buttonStart = Vector2.zero;
        Vector2 buttonTarget = Vector2.zero;
        RectTransform buttonTransform = null;

        if (_clickButton != null)
        {
            buttonTransform = _clickButton.GetComponent<RectTransform>();
            if (buttonTransform != null)
            {
                buttonStart = buttonTransform.anchoredPosition;
                // Click Button도 원본 위치 대비 Y축 오프셋만큼만 이동
                float yOffset = targetPos.y - _dialogOriginalAnchoredPos.y;
                buttonTarget = _clickButtonOriginalAnchoredPos + new Vector2(0f, yOffset);
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rt.anchoredPosition = Vector2.Lerp(start, targetPos, t);
            if (buttonTransform != null) buttonTransform.anchoredPosition = Vector2.Lerp(buttonStart, buttonTarget, t);
            yield return null;
        }

        rt.anchoredPosition = targetPos;
        if (buttonTransform != null) buttonTransform.anchoredPosition = buttonTarget;
    }

    private IEnumerator DialogCameraLockRoutine(float duration)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) yield break;

        float startSize = mainCam.orthographicSize;
        Vector3 startPos = mainCam.transform.position;
        Vector3 targetPos = new Vector3(0f, 0f, -10f); // default pos z

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            mainCam.orthographicSize = Mathf.Lerp(startSize, 10f, t);
            mainCam.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        mainCam.orthographicSize = 10f;
        mainCam.transform.position = targetPos;
        while (true) yield return null;
    }

    // --- 모듈용 공개 메서드 ---

    public IEnumerator ShowDialogueWithTyping(string speakerName, string message, Sprite portraitSprite)
    {
        if (_dialogPanel != null) _dialogPanel.SetActive(true);
        if (_nameText != null) _nameText.text = speakerName;

        if (_portraitImage != null)
        {
            bool hasPortrait = portraitSprite != null || _portraitIdleSprite != null;
            _portraitImage.gameObject.SetActive(hasPortrait);

            if (hasPortrait)
            {
                _currentPortraitIdleSprite = portraitSprite != null ? portraitSprite : _portraitIdleSprite;
                _portraitImage.sprite = _currentPortraitIdleSprite;
            }
        }

        _fullTargetText = message ?? "";
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        _typingCoroutine = StartCoroutine(TypingRoutine());

        StartPortraitTalkAnimation();
        yield return new WaitUntil(() => !_isTyping);
        StopPortraitTalkAnimation();
    }

    /// <summary>
    /// 초상화 위치 이동 시작 (초기 위치 기준 절대 이동)
    /// </summary>
    public void StartPortraitMove(RectTransform targetRect, float offset, float duration, float yOffset = 0f)
    {
        if (_portraitImage == null || !_portraitOriginalCached) return;

        // Treat offset as a directional X-axis delta. If a target rect is provided, apply the offset relative to it;
        // otherwise apply the offset relative to the portrait's original anchored position.
        Vector2 baseOrigin = targetRect != null ? targetRect.anchoredPosition : _portraitOriginalAnchoredPos;
        Vector2 baseTarget = baseOrigin + new Vector2(offset, 0f);

        // If dialog requested a downward safety offset (yOffset > 0), move portrait down accordingly to keep it in view.
        Vector2 finalTarget = baseTarget + Vector2.down * yOffset;

        // Skip if already at (or very near) target
        if (_isPortraitMoved && Vector2.Distance(_currentPortraitTargetPos, finalTarget) < 1f) return;

        _currentPortraitTargetPos = finalTarget;
        _isPortraitMoved = true;

        var portraitRt = _portraitImage.GetComponent<RectTransform>();
        if (_portraitMoveCoroutine != null) StopCoroutine(_portraitMoveCoroutine);
        _portraitMoveCoroutine = StartCoroutine(MovePortraitToRoutine(portraitRt, finalTarget, duration));
    }

    /// <summary>
    /// 초상화 원위치 복귀 (이미 원위치면 스킵)
    /// </summary>
    public void ResetPortraitPosition(float duration)
    {
        if (_portraitImage == null || !_portraitOriginalCached || !_isPortraitMoved) return;

        _isPortraitMoved = false;
        _currentPortraitTargetPos = _portraitOriginalAnchoredPos;

        var portraitRt = _portraitImage.GetComponent<RectTransform>();
        if (_portraitMoveCoroutine != null) StopCoroutine(_portraitMoveCoroutine);

        if (duration <= 0f)
        {
            portraitRt.anchoredPosition = _portraitOriginalAnchoredPos;
            return;
        }

        _portraitMoveCoroutine = StartCoroutine(MovePortraitToRoutine(portraitRt, _portraitOriginalAnchoredPos, duration));
    }

    /// <summary>
    /// 대사판 위로 이동 시작 (초기 위치 기준 절대 이동)
    /// </summary>
    public void StartDialogPanelMove(float offset, float duration)
    {
        // 동일한 오프셋이면 애니메이션 스킵
        if (Mathf.Approximately(_currentDialogOffset, offset)) return;

        _currentDialogOffset = offset;
        Vector2 targetPos = _dialogOriginalAnchoredPos + new Vector2(0f, offset);

        if (_dialogMoveCoroutine != null) StopCoroutine(_dialogMoveCoroutine);
        _dialogMoveCoroutine = StartCoroutine(MoveDialogRoutine(targetPos, duration));
    }

    /// <summary>
    /// 대사판 원위치 복귀 (이미 원위치면 스킵)
    /// </summary>
    public void ResetDialogPanelPosition(float duration)
    {
        // 이미 원위치(오프셋 0)면 스킵
        if (Mathf.Approximately(_currentDialogOffset, 0f)) return;

        _currentDialogOffset = 0f;

        if (_dialogMoveCoroutine != null) StopCoroutine(_dialogMoveCoroutine);
        _dialogMoveCoroutine = StartCoroutine(MoveDialogRoutine(_dialogOriginalAnchoredPos, duration));
    }

    public void ShowPlacementProgress(string label)
    {
        if (_placementProgressPanel != null) _placementProgressPanel.SetActive(true);
        if (_placementProgressText != null) _placementProgressText.text = label;
    }

    public void HidePlacementProgress()
    {
        if (_placementProgressPanel != null) _placementProgressPanel.SetActive(false);
    }

    public void StartUIHighlight(RectTransform targetUI)
    {
        if (_highlighter == null || targetUI == null) return;

        _highlighter.gameObject.SetActive(true);
        _highlighter.position = targetUI.position;
        _highlighterBaseSize = targetUI.sizeDelta;
        _highlighter.sizeDelta = _highlighterBaseSize;

        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        _pulseCoroutine = StartCoroutine(HighlighterPulseRoutine());
    }

    public void RemoveUIHighlight()
    {
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        if (_highlighter != null) _highlighter.gameObject.SetActive(false);
    }

    public void HideDialogue()
    {
        if (_dialogPanel != null) _dialogPanel.SetActive(false);
        if (_highlighter != null) _highlighter.gameObject.SetActive(false);
        if (_placementProgressPanel != null) _placementProgressPanel.SetActive(false);

        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        if (_dialogMoveCoroutine != null) StopCoroutine(_dialogMoveCoroutine);
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        if (_portraitMoveCoroutine != null) StopCoroutine(_portraitMoveCoroutine);
        if (_dialogCameraCoroutine != null) StopCoroutine(_dialogCameraCoroutine);

        // [4] 안전 정리: HideDialogue 마지막에 추가
        StopPortraitTalkAnimation();
    }

    // [2] 메서드 추가 (클래스 내부 아무 곳)
    private void StartPortraitTalkAnimation()
    {
        if (_portraitImage == null || !_portraitImage.gameObject.activeSelf) return;
        if (_portraitTalkingSprite == null) return;

        StopPortraitTalkAnimation();
        _portraitTalkCoroutine = StartCoroutine(PortraitTalkRoutine());
    }

    private void StopPortraitTalkAnimation()
    {
        if (_portraitTalkCoroutine != null)
        {
            StopCoroutine(_portraitTalkCoroutine);
            _portraitTalkCoroutine = null;
        }

        if (_portraitImage != null && _portraitImage.gameObject.activeSelf)
        {
            _portraitImage.sprite = _currentPortraitIdleSprite != null
                ? _currentPortraitIdleSprite
                : _portraitIdleSprite;
        }
    }

    private IEnumerator PortraitTalkRoutine()
    {
        bool toggle = false;

        while (_isTyping)
        {
            toggle = !toggle;
            _portraitImage.sprite = toggle ? _portraitTalkingSprite : _currentPortraitIdleSprite;
            yield return new WaitForSecondsRealtime(_portraitBlinkInterval);
        }

        _portraitImage.sprite = _currentPortraitIdleSprite;
        _portraitTalkCoroutine = null;
    }
}