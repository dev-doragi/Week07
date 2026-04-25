using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DoctrineNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Data")]
    [SerializeField] private DoctrineNodeData data;

    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image glowImage;
    [SerializeField] private Image lockOverlay;
    [SerializeField] private Button button;

    [Header("Colors")]
    [SerializeField] private Color lockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color pendingColor = new Color(1f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color confirmedColor = new Color(1f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private DoctrineNodeState _state = DoctrineNodeState.Locked;
    private DoctrineManager _manager;
    private DoctrineTooltipUI _tooltip;

    public int RowIndex => data != null ? data.rowIndex : -1;
    public int ColumnIndex => data != null ? data.columnIndex : -1;

    public DoctrineNodeData GetData()
    {
        return data;
    }

    public DoctrineNodeState GetState()
    {
        return _state;
    }

    private void Reset()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }
    }

    public void Initialize(DoctrineManager manager, DoctrineTooltipUI tooltip)
    {
        _manager = manager;
        _tooltip = tooltip;

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }

        SetState(_state);
    }

    public void SetState(DoctrineNodeState newState)
    {
        _state = newState;

        if (iconImage != null)
        {
            iconImage.color = GetColorByState(newState);
        }

        bool isGlowOn = newState == DoctrineNodeState.Pending || newState == DoctrineNodeState.Confirmed;
        if (glowImage != null)
        {
            glowImage.gameObject.SetActive(isGlowOn);
        }

        bool showOverlay = newState == DoctrineNodeState.Locked || newState == DoctrineNodeState.Disabled;
        if (lockOverlay != null)
        {
            lockOverlay.gameObject.SetActive(showOverlay);
        }

        if (button != null)
        {
            button.interactable = newState == DoctrineNodeState.Available || newState == DoctrineNodeState.Pending;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (_manager == null)
        {
            Debug.LogWarning($"[DoctrineNodeUI] Manager is not assigned on {name}");
            return;
        }

        _manager.TrySelectNode(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip == null)
        {
            return;
        }

        _tooltip.Show(data, _state);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tooltip == null)
        {
            return;
        }

        _tooltip.Hide();
    }

    private Color GetColorByState(DoctrineNodeState state)
    {
        switch (state)
        {
            case DoctrineNodeState.Locked:
                return lockedColor;
            case DoctrineNodeState.Available:
                return availableColor;
            case DoctrineNodeState.Pending:
                return pendingColor;
            case DoctrineNodeState.Confirmed:
                return confirmedColor;
            case DoctrineNodeState.Disabled:
                return disabledColor;
            default:
                return availableColor;
        }
    }
}
