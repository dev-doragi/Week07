using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TutorialSkipButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Settings")]
    [SerializeField] private float _holdDuration = 1.5f; // 1.5초 정도가 적당합니다.
    [SerializeField] private Image _fillImage;           // 게이지용 UI 이미지

    private float _timer = 0f;
    private bool _isHolding = false;

    private void Update()
    {
        if (_isHolding)
        {
            // 튜토리얼 중 시간이 멈춰있을 수 있으므로 unscaledDeltaTime 사용
            _timer += Time.unscaledDeltaTime;

            if (_fillImage != null)
                _fillImage.fillAmount = _timer / _holdDuration;

            if (_timer >= _holdDuration)
            {
                OnSkipComplete();
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isHolding = true;
        _timer = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ResetButton();
    }

    private void ResetButton()
    {
        _isHolding = false;
        _timer = 0f;
        if (_fillImage != null)
            _fillImage.fillAmount = 0f;
    }

    private void OnSkipComplete()
    {
        ResetButton();
        Debug.Log("[Tutorial] Skip Hold Complete!");

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.SkipTutorial();
        }
        else
        {
            Time.timeScale = 1f;
        }

        // IF: 그냥 튜토리얼 클리어 패널 말고 아예 스테이지로 이동하게 할까?
    }
}