using TMPro;
using UnityEngine;

public class IncomeZoneBonusTooltipUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _attackSpeedText;
    [SerializeField] private TextMeshProUGUI _maxHpText;
    [SerializeField] private TextMeshProUGUI _capacityText;
    [SerializeField] private TextMeshProUGUI _productionText;

    [Header("Text Colors")]
    [SerializeField] private Color _titleColor = Color.white;
    [SerializeField] private Color _attackSpeedColor = Color.white;
    [SerializeField] private Color _maxHpColor = Color.white;
    [SerializeField] private Color _capacityColor = Color.white;
    [SerializeField] private Color _productionColor = Color.white;

    private void Awake()
    {
        if(_panel == null) return;
        ApplyColors();
        _panel.SetActive(false);
    } 

    public void Show(float attackSpeed, int maxHp, int capacity, int production, RectTransform anchor)
    {
        _titleText.text = "생산시설 보너스 정보";
        _attackSpeedText.text = $"공격속도 증가 : +{attackSpeed * 100f:F0}%";
        _maxHpText.text = $"최대 체력 증가 : +{maxHp}";
        _capacityText.text = $"유닛 수용량 증가 : +{capacity}";
        _productionText.text = $"자원 생산력 증가 : +{production}";

        // 위치 설정 (앵커 기준 위쪽)
        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        _panel.transform.position = new Vector3(corners[1].x, corners[1].y + 25f, 0f);
        _panel.SetActive(true);
    }

    private void ApplyColors()
    {
        if(_titleText != null) _titleText.color = _titleColor;
        if (_attackSpeedText != null) _attackSpeedText.color = _attackSpeedColor;
        if (_maxHpText != null)       _maxHpText.color       = _maxHpColor;
        if (_capacityText != null)    _capacityText.color    = _capacityColor;
        if (_productionText != null)  _productionText.color  = _productionColor;
    }

    public void Hide() => _panel.SetActive(false);
}
