using UnityEngine;
using UnityEngine.UI;

public class DoctrineTooltipUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Texts")]
    [SerializeField] private Text nodeNameText;
    [SerializeField] private Text doctrineTypeText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text effectSummaryText;
    [SerializeField] private Text stateHintText;

    [Header("Position")]
    [SerializeField] private bool followMouse = true;
    [SerializeField] private Vector2 mouseOffset = new Vector2(20f, -20f);

    private bool _isVisible;

    private void Awake()
    {
        if (tooltipRoot == null)
        {
            tooltipRoot = transform as RectTransform;
        }

        Hide();
    }

    private void Update()
    {
        if (!_isVisible || !followMouse || tooltipRoot == null)
        {
            return;
        }

        tooltipRoot.position = (Vector2)Input.mousePosition + mouseOffset;
    }

    public void Show(DoctrineNodeData data, DoctrineNodeState state)
    {
        if (data == null)
        {
            Hide();
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

        if (followMouse && tooltipRoot != null)
        {
            tooltipRoot.position = (Vector2)Input.mousePosition + mouseOffset;
        }

        _isVisible = true;
    }

    public void Hide()
    {
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
