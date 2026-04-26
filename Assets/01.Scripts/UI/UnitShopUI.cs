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

    [Header("Slide Settings")]
    [SerializeField] private float _slideDuration = 0.25f;
    [SerializeField] private float _panelOpenX = 80f;
    [SerializeField] private float _panelClosedX = -220f;

    private UnitCategory _currentCategory = UnitCategory.None;
    private bool _isOpen = false;
    private Coroutine _slideCoroutine;

    private void Update()
    {
        if (!_isOpen) return;
        if (_gridController.IsPlacingUnit) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame &&
            !EventSystem.current.IsPointerOverGameObject())
        {
            ClosePanel();
        }
    }

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

        _slidePanel.anchoredPosition = new Vector2(_panelClosedX, _slidePanel.anchoredPosition.y);

        _attackBtn.onClick.AddListener(() => OnCategoryClicked(UnitCategory.Attack));
        _defenseBtn.onClick.AddListener(() => OnCategoryClicked(UnitCategory.Defense));
        _supportBtn.onClick.AddListener(() => OnCategoryClicked(UnitCategory.Support));
    }

    private void UpdateCapacityText()
    {
        if(_capacityText != null && GridManager.Instance != null)
            _capacityText.text = $"{GridManager.Instance.CurrentUnitCount} / {GridManager.Instance.MaxCapacity}";
    }

    private void OnCategoryClicked(UnitCategory category)
    {
        if (_isOpen && _currentCategory == category)
        {
            ClosePanel();
            return;
        }

        _currentCategory = category;
        PopulateCards(category);

        if (!_isOpen)
            OpenPanel();
    }

    private void PopulateCards(UnitCategory category)
    {
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
                card.Setup(data, OnUnitSelected, IsUnlocked(data));
            }
        }

        foreach (var data in _allUnits)
        {
            if (data.Category != category) continue;
            var card = Instantiate(_cardPrefab, _cardContainer);
            card.Setup(data, OnUnitSelected, IsUnlocked(data));
        }
    }

    private bool IsUnlocked(UnitDataSO data)
    {
        UnlockManager unlockManager = FindFirstObjectByType<UnlockManager>();
        return unlockManager == null || unlockManager.IsUnitUnlocked(data);
    }

    private void OnUnitUnlocked(UnitDataSO unit)
    {
        if (_isOpen && _currentCategory != UnitCategory.None)
            PopulateCards(_currentCategory);
    }

    private void OnUnitSelected(UnitDataSO data)
    {
        _gridController.SelectByData(data);
    }

    private void OpenPanel()
    {
        _isOpen = true;
        SlideToX(_panelOpenX);
    }

    private void ClosePanel()
    {
        _isOpen = false;
        _currentCategory = UnitCategory.None;
        SlideToX(_panelClosedX);
    }

    private void SlideToX(float targetX)
    {
        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(SlideRoutine(targetX));
    }

    private IEnumerator SlideRoutine(float targetX)
    {
        float startX = _slidePanel.anchoredPosition.x;
        float elapsed = 0f;

        while (elapsed < _slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _slideDuration));
            _slidePanel.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, t), _slidePanel.anchoredPosition.y);
            yield return null;
        }

        _slidePanel.anchoredPosition = new Vector2(targetX, _slidePanel.anchoredPosition.y);
        _slideCoroutine = null;
    }
}
