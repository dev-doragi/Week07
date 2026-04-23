using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

    [Header("Slide Settings")]
    [SerializeField] private float _slideDuration = 0.25f;
    [SerializeField] private float _panelOpenX = 80f;
    [SerializeField] private float _panelClosedX = -220f;

    private E_UnitCategory _currentCategory = E_UnitCategory.None;
    private bool _isOpen = false;
    private Coroutine _slideCoroutine;

    private void Start()
    {
        if (_allUnits == null || _allUnits.Count == 0)
            _allUnits = new List<UnitDataSO>(Resources.LoadAll<UnitDataSO>("UnitSO"));

        _slidePanel.anchoredPosition = new Vector2(_panelClosedX, _slidePanel.anchoredPosition.y);

        _attackBtn.onClick.AddListener(() => OnCategoryClicked(E_UnitCategory.Attack));
        _defenseBtn.onClick.AddListener(() => OnCategoryClicked(E_UnitCategory.Defense));
        _supportBtn.onClick.AddListener(() => OnCategoryClicked(E_UnitCategory.Support));
    }

    private void OnCategoryClicked(E_UnitCategory category)
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

    private void PopulateCards(E_UnitCategory category)
    {
        foreach (Transform child in _cardContainer)
            Destroy(child.gameObject);

        foreach (var data in _allUnits)
        {
            if (data.Category != category) continue;
            var card = Instantiate(_cardPrefab, _cardContainer);
            card.Setup(data, OnUnitSelected);
        }
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
        _currentCategory = E_UnitCategory.None;
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
