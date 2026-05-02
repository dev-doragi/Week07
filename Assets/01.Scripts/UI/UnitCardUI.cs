using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UnitCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _costText;
    [SerializeField] private Button _button;

    [SerializeField] private Image _backGround;
    [SerializeField] private Color _selectedColor = new Color(1f, 0.85f, 0f, 1f);
    private Color _defaultColor;

    private Action<UnitDataSO> _onSelect;
    private UnitDataSO _data;
    private UnitTooltipUI _tooltip;
    private bool _isUnlocked;

    public void Setup(UnitDataSO data, Action<UnitDataSO> onSelect, UnitTooltipUI tooltip)
    {
        Setup(data, onSelect, tooltip, true);
    }

    public void SetSelected(bool selected)
    {
        if(_backGround == null) return;
        _backGround.color = selected ? _selectedColor : _defaultColor;
    }

    public void Setup(UnitDataSO data, Action<UnitDataSO> onSelect, UnitTooltipUI tooltip, bool isUnlocked)
    {
        if(_backGround == null) _backGround = GetComponent<Image>();
        if(_backGround != null) _defaultColor = _backGround.color;

        _data = data;
        _tooltip = tooltip;
        _onSelect = onSelect;
        _isUnlocked = isUnlocked;

        if (_icon != null)
        {
            _icon.sprite = data.Icon;
            _icon.enabled = data.Icon != null;
            _icon.color = isUnlocked ? Color.white : Color.black;
        }

        if(_nameText != null) _nameText.text = isUnlocked ? data.UnitName : string.Empty;
        if(_costText != null) _costText.text = isUnlocked ? data.Cost.ToString() : string.Empty;

        if(_button == null) return;
        _button.onClick.RemoveAllListeners();
        _button.interactable = isUnlocked;
        if (isUnlocked)
            _button.onClick.AddListener(() => _onSelect?.Invoke(data));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(!_isUnlocked) return;
        _tooltip?.Show(_data, GetComponent<RectTransform>());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _tooltip?.Hide();
    }
}
