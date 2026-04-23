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
        _onSelect = onSelect;

        Debug.Log($"[UnitCardUI] Setup 호출 | 유닛={data.UnitName} | _icon null={_icon == null} | data.Icon null={data.Icon == null}");

        if (_icon != null)
        {
            _icon.sprite = data.Icon;
            _icon.enabled = data.Icon != null;
            Debug.Log($"[UnitCardUI] 스프라이트 설정 완료 | sprite={_icon.sprite} | enabled={_icon.enabled} | size={_icon.rectTransform.sizeDelta}");
        }
        if (_nameText != null) _nameText.text = data.UnitName;
        if (_costText != null) _costText.text = data.Cost.ToString();

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onSelect?.Invoke(data));
    }
}
