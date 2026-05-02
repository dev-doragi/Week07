using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UnitShopUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridController _gridController;
    [SerializeField] private RectTransform _slidePanel;
    [SerializeField] private Transform _cardContainer;
    [SerializeField] private UnitCardUI _cardPrefab;

    [Header("Category Buttons")]
    [SerializeField] private Button _attackBtn;
    [SerializeField] private Button _defenseBtn;
    [SerializeField] private Button _supportBtn;

    [Header("Unit Data")]
    [Tooltip("비워두면 Resources/UnitSO 폴더에서 자동 로드")]
    [SerializeField] private List<UnitDataSO> _allUnits;
    [SerializeField] private TextMeshProUGUI _capacityText;     //수용량 텍스트 UI

    [Header("Capacity Text")]
    [SerializeField] private Color _capacityNormalColor = Color.white;
    [SerializeField] private Color _capacityFullColor = Color.red;

    [Header("Tooltip")]
    [SerializeField] private UnitTooltipUI _tooltip;

    [Header("Slide Settings")]
    [SerializeField] private float _panelOpenX = 80f;

    private UnitCategory _currentCategory = UnitCategory.None;
    private UnitCardUI _selectedCard;

    private void OnEnable()
    {
        if(GridManager.Instance != null)
            GridManager.Instance.OnCapacityChanged += UpdateCapacityText;

        UnlockManager unlockManager = FindFirstObjectByType<UnlockManager>();
        if (unlockManager != null)
            unlockManager.UnitUnlocked += OnUnitUnlocked;
    }

    private void OnDisable()
    {
        if(GridManager.Instance != null)
            GridManager.Instance.OnCapacityChanged -= UpdateCapacityText;

        UnlockManager unlockManager = FindFirstObjectByType<UnlockManager>();
        if (unlockManager != null)
            unlockManager.UnitUnlocked -= OnUnitUnlocked;
    }


    private void Start()
    {
        if (_allUnits == null || _allUnits.Count == 0)
            _allUnits = new List<UnitDataSO>(Resources.LoadAll<UnitDataSO>("UnitSO"));

        _slidePanel.anchoredPosition = new Vector2(_panelOpenX, _slidePanel.anchoredPosition.y);

        _attackBtn.onClick.AddListener(() => OnCategoryClicked(UnitCategory.Attack));
        _defenseBtn.onClick.AddListener(() => OnCategoryClicked(UnitCategory.Defense));
        _supportBtn.onClick.AddListener(() => OnCategoryClicked(UnitCategory.Support));

        UpdateCapacityText();
    }

    private void UpdateCapacityText()
    {
        if(_capacityText == null || GridManager.Instance == null) return;

        int current = GridManager.Instance.CurrentUnitCount;
        int max = GridManager.Instance.MaxCapacity;
        _capacityText.text = $"{current} / {max}";
        _capacityText.color = current >= max ? _capacityFullColor : _capacityNormalColor;
    }

    private void OnCategoryClicked(UnitCategory category)
    {
        GameCsvLogger.Instance.LogEvent(
            GameLogEventType.ButtonClicked,
            actor: gameObject,
            metadata: new Dictionary<string, object> { { "button", category + "Tab" } });

        _currentCategory = category;
        PopulateCards(category);


    }

    private void PopulateCards(UnitCategory category)
    {
        _selectedCard?.SetSelected(false);
        _selectedCard = null;

        foreach (Transform child in _cardContainer)
            Destroy(child.gameObject);

        //지원 탭일 때 설치 가능한 바퀴를 최상단에 먼저 추가
        if(category == UnitCategory.Support)
        {
            foreach(var data in _allUnits)
            {
                if(data.Category != UnitCategory.Wheel) continue;
                if(data.PlacementRule == PlacementRule.InitialOnly) continue;
                var card = Instantiate(_cardPrefab, _cardContainer);
                var capturedCard = card;
                card.Setup(data, (d) =>
                {
                    SelectCard(capturedCard);
                    OnUnitSelected(d);
                }, _tooltip);
            }
        }

        foreach (var data in _allUnits)
        {
            if (data.Category != category) continue;
            var card = Instantiate(_cardPrefab, _cardContainer);
            var capturedCard = card;
            card.Setup(data, (d) =>
            {
                SelectCard(capturedCard);
                OnUnitSelected(d);
            }, _tooltip, IsUnlocked(data));
        }
    }

    private void SelectCard(UnitCardUI card)
    {
        _selectedCard?.SetSelected(false);
        _selectedCard = card;
        _selectedCard?.SetSelected(true);
    }

    private bool IsUnlocked(UnitDataSO data)
    {
        UnlockManager unlockManager = FindFirstObjectByType<UnlockManager>();
        return unlockManager == null || unlockManager.IsUnitUnlocked(data);
    }

    private void OnUnitUnlocked(UnitDataSO unit)
    {
        if (_currentCategory != UnitCategory.None)
            PopulateCards(_currentCategory);
    }

    private void OnUnitSelected(UnitDataSO data)
    {
        GameCsvLogger.Instance.LogEvent(
            GameLogEventType.ButtonClicked,
            actor: gameObject,
            value: data != null ? data.Cost : 0f,
            metadata: new Dictionary<string, object>
            {
                { "button", "UnitCard" },
                { "unitKey", data != null ? data.Key : -1 },
                { "unitName", data != null ? data.UnitName : string.Empty },
                { "category", data != null ? data.Category.ToString() : string.Empty }
            });
        _gridController.SelectByData(data);
    }

}
