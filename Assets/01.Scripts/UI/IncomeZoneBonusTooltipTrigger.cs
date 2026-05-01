using UnityEngine;
using UnityEngine.EventSystems;

public class IncomeZoneBonusTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private IncomeZoneBonusTooltipUI _tooltip;
    [SerializeField] private IncomeZoneBonusSystem _bonusSystem;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(_tooltip == null || _bonusSystem == null) return;
        _tooltip?.Show(
            _bonusSystem.CurrentAttackSpeedBonus,
            _bonusSystem.CurrentMaxHpBonus,
            _bonusSystem.CurrentCapacityBonus,
            _bonusSystem.CurrentProductionBonus,
            GetComponent<RectTransform>());
    }

    public void OnPointerExit(PointerEventData eventData) => _tooltip?.Hide();
}
