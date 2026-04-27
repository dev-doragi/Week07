using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UnitTooltipUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descText;
    [SerializeField] private TextMeshProUGUI _costText;
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private TextMeshProUGUI _attackDmgText;
    [SerializeField] private TextMeshProUGUI _attackSpdText;
    [SerializeField] private TextMeshProUGUI _collisionPowerText;   //방어 유닛 전용
    [SerializeField] private TextMeshProUGUI _buffDescText;         //지원 유닛 전용
    [SerializeField] private float _xOffset = 10f;

    private void Awake()
    {
        _panel.SetActive(false);        
    }

    public void Show(UnitDataSO data, RectTransform cardRect)
    {
        if(data == null) return;

        if(_icon != null) {_icon.sprite = data.Icon; _icon.enabled = data.Icon != null; }
        if(_nameText != null) _nameText.text = data.UnitName;

        // 모든 선택적 필드 먼저 숨기기
        HideAll();

        // 카테고리별 표시
        switch (data.Category)
        {
            case UnitCategory.Attack:
                SetField(_costText, $"코스트: {data.Cost}");
                SetField(_hpText, $"HP: {data.MaxHp}");
                if (data.CanAttack)
                {
                    SetField(_attackDmgText, $"공격력: {data.Attack.Damage}");
                    SetField(_attackSpdText, $"공격속도: {data.Attack.Speed:F1}");
                }
                SetField(_descText, data.Description);
                break;

            case UnitCategory.Defense:
                SetField(_costText, $"코스트: {data.Cost}");
                SetField(_hpText, $"HP: {data.MaxHp}");
                if (data.CanCollide)
                    SetField(_collisionPowerText, $"충돌데미지: {data.Defense.CollisionPower}");
                SetField(_descText, data.Description);
                break;

            case UnitCategory.Support:
                SetField(_costText, $"코스트: {data.Cost}");
                SetField(_hpText, $"HP: {data.MaxHp}");
                SetField(_descText, data.Description);
                if (data.CanSupport && data.Support.Effects.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var effect in data.Support.Effects)
                        sb.AppendLine(effect.EffectDescription());
                    SetField(_buffDescText, sb.ToString().TrimEnd());
                }
                break;

            case UnitCategory.Wheel:
                SetField(_costText, $"코스트: {data.Cost}");   // 추가
                SetField(_descText, data.Description);
                break;
        }

        Vector3[] corners = new Vector3[4];
        cardRect.GetWorldCorners(corners);
        // corners[2] = 우상단, corners[3] = 우하단
        float rightX = corners[2].x;
        float centerY = (corners[0].y + corners[1].y) * 0.5f;

        _panel.transform.position = new Vector3(rightX + _xOffset, centerY, 0f);
        _panel.SetActive(true);
    }

    private void HideAll()
    {
        SetHide(_costText);
        SetHide(_hpText);
        SetHide(_descText);
        SetHide(_attackDmgText);
        SetHide(_attackSpdText);
        SetHide(_collisionPowerText);
        SetHide(_buffDescText);
    }

    private void SetField(TextMeshProUGUI field, string text)
    {
        if (field == null) return;
        field.text = text;
        field.gameObject.SetActive(true);
    }

    private void SetHide(TextMeshProUGUI field)
    {
        if (field != null) field.gameObject.SetActive(false);
    }

    public void Hide() => _panel.SetActive(false);

}


