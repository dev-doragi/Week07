using UnityEngine;
using UnityEngine.Events;

// ================================================================
// 돌진 게이지 충전 시스템
// - 1초에 (1 × ChargeGaugePower)만큼 게이지 증가
// - 최대 게이지 도달 시 OnGaugeFull 이벤트 발동 (UI 버튼 활성화용)
// - 외부에서 Consume() 호출 시 0으로 리셋
// ================================================================
public class GridChargeSystem : MonoBehaviour
{
    [Header("Gauge Settings")]
    [SerializeField, Min(1f)] private float _maxGauge = 100f;
    [SerializeField, Min(0f)] private float _chargeGaugePower = 1f;

    [Header("Events")]
    [Tooltip("게이지가 가득 찬 순간 1회 호출 (UI 버튼 활성화)")]
    public UnityEvent OnGaugeFull;

    [Tooltip("게이지 값이 변할 때마다 호출 (0 ~ 1 정규화 값, UI 프로그레스 비용)")]
    public UnityEvent<float> OnGaugeChanged;

    //런타임 상태
    private float _current;
    private bool _isFull;

    //외부에서 조회용 UI 텍스트 등..
    public float Current => _current;
    public float Max => _maxGauge;
    public float Normalized => _current / _maxGauge;
    public bool IsFull => _isFull;

    // 인스펙터 런타임 조정 허용
    public float ChargeGaugePower
    {
        get => _chargeGaugePower;
        set => _chargeGaugePower = Mathf.Max(0f, value);
    }

    // Unity 생명 주기
    private void Update()
    {
        if(_isFull) return;     //가득 찬 상태에선 충전 정지
        
        //매초 1 x power 씩 증가
        _current += _chargeGaugePower * Time.deltaTime;

        if(_current  >= _maxGauge)
        {
            _current = _maxGauge;
            _isFull = true;
            OnGaugeChanged?.Invoke(1f);
            OnGaugeFull?.Invoke();
        }

        OnGaugeChanged?.Invoke(Normalized);
    }

    //외부 API
    public void Consume()
    {
        _current = 0f;
        _isFull = false;
        OnGaugeChanged?.Invoke(0f);
        Debug.Log("[Charge] Consume - 게이지 0으로 리셋, 충전 재개");
    }

    // 디버그/치트용: 즉시 가득 채우기
    [ContextMenu("Fill Gauge")]
    public void FillImmediately()
    {
        _current = _maxGauge;
        _isFull = true;
        OnGaugeFull?.Invoke();
        OnGaugeChanged?.Invoke(1f);
    }

}
