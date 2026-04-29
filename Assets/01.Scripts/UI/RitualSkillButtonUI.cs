using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class RitualSkillButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("SKill Info")]
    [SerializeField] private int _skillIndex;   // 1, 2, 3
    [SerializeField] private string _skillName;
    [SerializeField] private string _skillDescription;

    [Header("References")]
    [SerializeField] private TextMeshProUGUI _buttonLabel;
    [SerializeField] private RitualSystem _ritualSystem;
    [SerializeField] private RitualTooltipUI _tooltip;

    [Header("Visual")]
    [SerializeField] private float _lockedAlpha = 0.4f;

    private CanvasGroup _canvasGroup;
    private bool _cachedUnlockedState;

    private void Start()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        Refresh();
        RefreshVisual();  
    } 

    private void Update()
    {
        if(_ritualSystem == null || _canvasGroup == null) return;
        bool current = _ritualSystem.IsSkillUnlocked(_skillIndex);
        if(current == _cachedUnlockedState) return;

        _cachedUnlockedState = current;
        _canvasGroup.alpha = current ? 1f : _lockedAlpha;
    }

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<RitualCostChangedEvnet>(OnCostChanged);   
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<RitualCostChangedEvnet>(OnCostChanged);
    }

    private void OnCostChanged(RitualCostChangedEvnet evt) => Refresh();


    private void RefreshVisual()
    {
        if (_canvasGroup == null || _ritualSystem == null) return;
        bool unlocked = _ritualSystem.IsSkillUnlocked(_skillIndex);
        _canvasGroup.alpha = unlocked ? 1f : _lockedAlpha;
    }
    public void Refresh()
    {
        if(_ritualSystem == null || _buttonLabel == null) return;
        int cost = _ritualSystem.GetSkillCost(_skillIndex);
        _buttonLabel.text = $"{_skillName}";
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(_tooltip == null || _ritualSystem == null) return;
        if(!_ritualSystem.IsSkillUnlocked(_skillIndex)) return;

        int cost = _ritualSystem.GetSkillCost(_skillIndex);
        float cooldown = _ritualSystem.GetSkillCooldownDuration(_skillIndex);
        _tooltip.Show(_skillName, _skillDescription, cost, cooldown, GetComponent<RectTransform>());
    }

    public void OnPointerExit(PointerEventData eventData) => _tooltip?.Hide();
}
