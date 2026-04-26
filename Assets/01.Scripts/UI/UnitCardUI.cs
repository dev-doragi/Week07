using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitCardUI : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _costText;
    [SerializeField] private Button _button;

    private Action<UnitDataSO> _onSelect;

    public void Setup(UnitDataSO data, Action<UnitDataSO> onSelect)
    {
        Setup(data, onSelect, true);
    }

    public void Setup(UnitDataSO data, Action<UnitDataSO> onSelect, bool isUnlocked)
    {
        _onSelect = onSelect;

        if (_icon != null)
        {
            _icon.sprite = data.Icon;
            _icon.enabled = data.Icon != null;
            _icon.color = isUnlocked ? Color.white : Color.black;
        }

        if (_nameText != null)
            _nameText.text = isUnlocked ? data.UnitName : string.Empty;

        if (_costText != null)
            _costText.text = isUnlocked ? data.Cost.ToString() : string.Empty;

        if (_button == null)
            return;

        _button.onClick.RemoveAllListeners();
        _button.interactable = isUnlocked;
        if (isUnlocked)
            _button.onClick.AddListener(() => _onSelect?.Invoke(data));
    }
}
