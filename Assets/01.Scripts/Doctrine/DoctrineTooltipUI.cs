using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DoctrineTooltipUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI nodeNameText;
    [SerializeField] private TextMeshProUGUI doctrineTypeText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI effectSummaryText;
    [SerializeField] private TextMeshProUGUI stateHintText;

    [Header("Position")]
    [SerializeField] private bool followMouse = true;
    [SerializeField] private Vector2 mouseOffset = new Vector2(20f, -20f);

    private bool _isVisible;
    private bool _isPinned;

    private void Awake()
    {
        if (tooltipRoot == null)
        {
            tooltipRoot = transform as RectTransform;
        }

        UnpinAndHide();
    }

    private void Update()
    {
        if (!_isVisible || !followMouse || tooltipRoot == null)
        {
            return;
        }

        if (TryGetMouseScreenPosition(out Vector2 mousePosition))
        {
            tooltipRoot.position = mousePosition + mouseOffset;
        }
    }

    public void Show(DoctrineNodeData data, DoctrineNodeState state)
    {
        ShowInternal(data, state, false);
    }

    public void Pin(DoctrineNodeData data, DoctrineNodeState state)
    {
        _isPinned = true;
        ShowInternal(data, state, true);
    }

    public void UnpinAndHide()
    {
        _isPinned = false;
        Hide(true);
    }

    public void Hide()
    {
        Hide(false);
    }

    private void ShowInternal(DoctrineNodeData data, DoctrineNodeState state, bool allowWhilePinned)
    {
        if (_isPinned && !allowWhilePinned)
        {
            return;
        }

        if (data == null)
        {
            Hide(true);
            return;
        }

        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (nodeNameText != null)
        {
            nodeNameText.text = string.IsNullOrWhiteSpace(data.nodeName) ? "(Unnamed Doctrine)" : data.nodeName;
        }

        if (doctrineTypeText != null)
        {
            doctrineTypeText.text = $"[{GetDoctrineTypeLabel(data.doctrineType)}]";
        }

        if (descriptionText != null)
        {
            descriptionText.text = string.IsNullOrWhiteSpace(data.description) ? "설명이 없습니다." : data.description;
        }

        if (effectSummaryText != null)
        {
            string summary = string.IsNullOrWhiteSpace(data.effectSummary) ? "효과 요약이 없습니다." : data.effectSummary;
            effectSummaryText.text = $"효과:\n{summary}";
        }

        if (stateHintText != null)
        {
            stateHintText.text = state == DoctrineNodeState.Locked ? "아직 해금되지 않음" : string.Empty;
        }

        if (followMouse && tooltipRoot != null && TryGetMouseScreenPosition(out Vector2 mousePosition))
        {
            tooltipRoot.position = mousePosition + mouseOffset;
        }

        _isVisible = true;
    }

    private void Hide(bool force)
    {
        if (_isPinned && !force)
        {
            return;
        }

        _isVisible = false;

        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private static bool TryGetMouseScreenPosition(out Vector2 mousePosition)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            mousePosition = Vector2.zero;
            return false;
        }

        mousePosition = mouse.position.ReadValue();
        return true;
    }

    private static string GetDoctrineTypeLabel(DoctrineType type)
    {
        switch (type)
        {
            case DoctrineType.Tower:
                return "공격 타워";
            case DoctrineType.Ritual:
                return "의식";
            case DoctrineType.Ram:
                return "충각";
            default:
                return type.ToString();
        }
    }
}
