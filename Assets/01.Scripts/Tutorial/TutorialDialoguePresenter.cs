using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialDialoguePresenter : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private TutorialDialogueDataSO _dialogueData;

    [Header("UI (optional, auto-created when null)")]
    [SerializeField] private GameObject _rootPanel;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _bodyText;
    [SerializeField] private TextMeshProUGUI _stepText;
    [SerializeField] private Button _nextButton;
    [SerializeField] private TextMeshProUGUI _nextButtonLabel;

    [Header("Runtime")]
    [SerializeField] private bool _autoCreateUI = true;

    private readonly Dictionary<int, string[]> _fallbackLines = new Dictionary<int, string[]>
    {
        { 0, new[] { "카메라를 움직여 전장을 확인하세요.\n우클릭 드래그, 휠 줌을 사용합니다." } },
        { 1, new[] { "생산시설을 1개 배치하세요.\n자원 순환의 시작입니다." } },
        { 2, new[] { "가속 버튼을 눌러 전투 흐름을 당겨보세요." } },
        { 3, new[] { "방어 유닛을 배치해 전열을 안정화하세요." } },
        { 4, new[] { "스킬을 1회 사용해 전황 개입을 익히세요." } },
        { 5, new[] { "공격 유닛을 배치해 화력을 확보하세요." } },
        { 6, new[] { "적을 1회 처치하면 튜토리얼이 완료됩니다." } }
    };

    private int _currentStepIndex = -1;
    private int _currentLineIndex = 0;
    private List<string> _currentLines = new List<string>();

    private void Awake()
    {
        if (_autoCreateUI)
            EnsureUiReferences();

        if (_nextButton != null)
            _nextButton.onClick.AddListener(OnNextClicked);
    }

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<TutorialStepStartedEvent>(OnStepStarted);
        EventBus.Instance?.Subscribe<TutorialStepCompletedEvent>(OnStepCompleted);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<TutorialStepStartedEvent>(OnStepStarted);
        EventBus.Instance?.Unsubscribe<TutorialStepCompletedEvent>(OnStepCompleted);
    }

    private void OnDestroy()
    {
        if (_nextButton != null)
            _nextButton.onClick.RemoveListener(OnNextClicked);
    }

    private void OnStepStarted(TutorialStepStartedEvent evt)
    {
        _currentStepIndex = evt.StepIndex;
        _currentLineIndex = 0;

        BuildLinesForStep(evt.StepIndex);
        SetVisible(true);
        RefreshTexts(evt.StepIndex, evt.TotalStepCount);
    }

    private void OnStepCompleted(TutorialStepCompletedEvent _)
    {
        // 다음 스텝 시작 이벤트에서 메시지가 교체되므로 별도 숨김 처리하지 않습니다.
    }

    private void OnNextClicked()
    {
        if (_currentLines.Count <= 1) return;
        if (_currentLineIndex >= _currentLines.Count - 1) return;

        _currentLineIndex++;
        _bodyText.text = _currentLines[_currentLineIndex];
        RefreshNextButtonState();
    }

    private void BuildLinesForStep(int stepIndex)
    {
        _currentLines.Clear();

        if (_dialogueData != null && _dialogueData.TryGetDialogue(stepIndex, out TutorialDialogueDataSO.StepDialogue data))
        {
            if (data.Lines != null)
            {
                for (int i = 0; i < data.Lines.Count; i++)
                {
                    string line = data.Lines[i];
                    if (!string.IsNullOrWhiteSpace(line))
                        _currentLines.Add(line);
                }
            }

            if (_titleText != null)
                _titleText.text = string.IsNullOrWhiteSpace(data.Title) ? $"튜토리얼 {stepIndex + 1}" : data.Title;
        }
        else
        {
            if (_titleText != null)
                _titleText.text = $"튜토리얼 {stepIndex + 1}";

            if (_fallbackLines.TryGetValue(stepIndex, out string[] lines))
            {
                for (int i = 0; i < lines.Length; i++)
                    _currentLines.Add(lines[i]);
            }
        }

        if (_currentLines.Count == 0)
            _currentLines.Add("진행 조건을 달성하세요.");
    }

    private void RefreshTexts(int stepIndex, int totalStepCount)
    {
        if (_stepText != null)
            _stepText.text = $"STEP {stepIndex + 1}/{totalStepCount}";

        if (_bodyText != null)
            _bodyText.text = _currentLines[_currentLineIndex];

        RefreshNextButtonState();
    }

    private void RefreshNextButtonState()
    {
        if (_nextButton == null) return;

        bool canNext = _currentLines.Count > 1 && _currentLineIndex < _currentLines.Count - 1;
        _nextButton.gameObject.SetActive(_currentLines.Count > 1);
        _nextButton.interactable = canNext;

        if (_nextButtonLabel != null)
            _nextButtonLabel.text = canNext ? "다음" : "마지막";
    }

    private void SetVisible(bool visible)
    {
        if (_rootPanel != null)
            _rootPanel.SetActive(visible);
    }

    private void EnsureUiReferences()
    {
        if (_rootPanel != null && _titleText != null && _bodyText != null && _stepText != null && _nextButton != null)
            return;

        Canvas targetCanvas = FindAnyObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            GameObject canvasGo = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            targetCanvas = canvasGo.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        if (_rootPanel == null)
        {
            _rootPanel = CreateUIObject("TutorialDialoguePanel", targetCanvas.transform).gameObject;
            Image bg = _rootPanel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.68f);

            RectTransform panelRt = _rootPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0f);
            panelRt.anchorMax = new Vector2(0.5f, 0f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.sizeDelta = new Vector2(900f, 220f);
            panelRt.anchoredPosition = new Vector2(0f, 30f);
        }

        if (_stepText == null)
            _stepText = CreateText("StepText", _rootPanel.transform, new Vector2(-420f, 82f), 24, TextAlignmentOptions.Left);
        if (_titleText == null)
            _titleText = CreateText("TitleText", _rootPanel.transform, new Vector2(-420f, 48f), 34, TextAlignmentOptions.Left);
        if (_bodyText == null)
            _bodyText = CreateText("BodyText", _rootPanel.transform, new Vector2(-420f, -18f), 30, TextAlignmentOptions.TopLeft);

        if (_nextButton == null)
        {
            GameObject buttonGo = CreateUIObject("NextButton", _rootPanel.transform).gameObject;
            Image buttonBg = buttonGo.AddComponent<Image>();
            buttonBg.color = new Color(1f, 1f, 1f, 0.18f);
            _nextButton = buttonGo.AddComponent<Button>();

            RectTransform brt = buttonGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(1f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(1f, 0f);
            brt.sizeDelta = new Vector2(170f, 62f);
            brt.anchoredPosition = new Vector2(-20f, 20f);
        }

        if (_nextButtonLabel == null && _nextButton != null)
        {
            _nextButtonLabel = CreateText("NextButtonText", _nextButton.transform, Vector2.zero, 28, TextAlignmentOptions.Center);
            RectTransform lrt = _nextButtonLabel.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _nextButtonLabel.text = "다음";
        }
    }

    private static RectTransform CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchoredPos, float fontSize, TextAlignmentOptions alignment)
    {
        RectTransform rt = CreateUIObject(name, parent);
        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.text = string.Empty;

        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(760f, 90f);
        return text;
    }
}
