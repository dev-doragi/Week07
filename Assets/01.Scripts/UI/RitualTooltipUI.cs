using UnityEngine;
using TMPro;

public class RitualTooltipUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descText;
    [SerializeField] private TextMeshProUGUI _costText;
    [SerializeField] private TextMeshProUGUI _cooldownText;
    [SerializeField] private float _xOffset = 10f;

    private void Awake() => _panel.SetActive(false);

    public void Show(string skillName, string desc, int cost, float cooldown, RectTransform buttonRect)
    {
        if(_nameText) _nameText.text = skillName;
        if(_descText) _descText.text = desc;
        if(_costText) _costText.text = $"소모값: {cost}";
        if(_cooldownText) _cooldownText.text = $"쿨타임 : {cooldown:F1}";

        Vector3[] corners = new Vector3[4];
        buttonRect.GetWorldCorners(corners);
        float rightX = corners[2].x;
        float centerY = (corners[0].y + corners[1].y) * 0.5f;
        _panel.transform.position = new Vector3(rightX + _xOffset, centerY, 0f);
        _panel.SetActive(true);
    }

    public void Hide() => _panel.SetActive(false);
}
