using UnityEngine;

/// <summary>
/// 적 유닛 사망 시 드랍되는 보상 재화 오브젝트입니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - 스폰 시 넉백(폭발) 연출을 발생시킵니다.
/// - 바닥에 착지하면 주행 상태로 전환되어 이동합니다.
/// - 플레이어 또는 수집 영역과 충돌 시 자원을 지급하고 풀로 반환합니다.
///
/// [연동]
/// - 보상 지급: `ResourceManager`
/// - 풀링: `PoolManager`
/// - 플레이어 판정: `Unit`(팀 체크) 또는 `PlayerCollector` 태그 / `GridManager` 폴백
/// </remarks>
public class DropRat : MonoBehaviour
{
    #region Inspector
    [Header("Components")]
    [SerializeField] private Rigidbody2D _rigid;
    [SerializeField] private SpriteRenderer _sr;

    [Header("Sprites / Anim")]
    [SerializeField] private Sprite _aliveSprite;
    [SerializeField] private Sprite[] _runFrames;
    [SerializeField] private float _runFrameRate = 8f;

    [Header("Physics")]
    [SerializeField] private float _knockBackPower = 4f;
    [SerializeField] private float _moveSpeed = 1f;
    [SerializeField] private LayerMask _groundLayerMask;

    [Header("Reward")]
    [SerializeField] private int _rewardAmount = 2;
    [SerializeField] private string _collectorTag = "PlayerCollector";
    #endregion

    private bool _isMoving;
    private bool _isCollected;
    private Coroutine _animCoroutine;

    /// <summary>
    /// 오브젝트가 활성화될 때 초기화 및 스폰 넉백을 적용합니다.
    /// </summary>
    private void OnEnable()
    {
        _isMoving = false;
        _isCollected = false;

        if(_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = null;

        if (_rigid == null)
        {
            Debug.LogError($"[{name}] Rigidbody2D 참조가 누락되었습니다.");
            return;
        }

        _rigid.linearVelocity = Vector2.zero;
        _rigid.angularVelocity = 0f;

        Vector2 explosionDir = new Vector2(Random.Range(-1f, -0.2f), Random.Range(0.5f, 1.5f));
        _rigid.AddForce(explosionDir.normalized * _knockBackPower, ForceMode2D.Impulse);

        if (_sr != null && _aliveSprite != null) _sr.sprite = _aliveSprite;

    }

    /// <summary>
    /// 오브젝트가 비활성화될 때 상태를 초기화합니다 (풀링 안전성).
    /// </summary>
    private void OnDisable()
    {
        _isMoving = false;
        _isCollected = false;
        if(_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = null;
    }

    /// <summary>
    /// 충돌 처리:
    /// - 지정된 바닥 레이어와 충돌하면 주행 애니메이션으로 전환
    /// - 플레이어/수집 대상과 충돌하면 수집 처리
    /// </summary>
    /// <param name="collision">충돌 정보</param>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null) return;

        if (!_isMoving && CheckLayerInMask(collision.gameObject.layer, _groundLayerMask))
        {
            _isMoving = true;

            if(_runFrames != null && _runFrames.Length > 0)
            {
                _animCoroutine = StartCoroutine(RunAnimLoop());
            }
        }

        if (_isCollected) return;

        GameObject other = collision.gameObject;

        if (other.CompareTag(_collectorTag))
        {
            Collect();
            return;
        }

        if (other.TryGetComponent(out Unit unit))
        {
            if (unit.Team == TeamType.Player)
            {
                Collect();
            }
        }
        else if (other.GetComponent<GridManager>() != null)
        {
            Collect();
        }
    }

    private System.Collections.IEnumerator RunAnimLoop()
    {
        float interval = 1f / _runFrameRate;
        int index = 0;
        while(true)
        {
            _sr.sprite = _runFrames[index];
            index = (index + 1) % _runFrames.Length;
            yield return new WaitForSeconds(interval);
        }
    }


    /// <summary>
    /// 매 프레임 이동 처리 (주행 중일 때 수평 속도 적용).
    /// </summary>
    private void Update()
    {
        if (_isMoving && _rigid != null)
        {
            _rigid.linearVelocity = new Vector2(Vector2.left.x * _moveSpeed, _rigid.linearVelocity.y);
        }
    }

    /// <summary>
    /// 수집 처리:
    /// - 중복 수집 방지
    /// - `ResourceManager`에 보상 지급
    /// - `PoolManager`로 반환(없으면 파괴)
    /// </summary>
    public void Collect()
    {
        if (_isCollected) return;
        _isCollected = true;

        if (ResourceManager.Instance == null)
        {
            Debug.LogError("[DropRat] ResourceManager.Instance를 찾을 수 없습니다.");
        }
        else
        {
            ResourceManager.Instance.AddMouseCount(_rewardAmount);
        }

        if (PoolManager.Instance == null)
        {
            Debug.LogWarning("[DropRat] PoolManager.Instance가 존재하지 않아 오브젝트를 파괴합니다.");
            Destroy(gameObject);
        }
        else
        {
            PoolManager.Instance.Despawn(gameObject);
        }
    }

    /// <summary>
    /// 지정 레이어가 마스크에 포함되는지 검사합니다.
    /// </summary>
    private bool CheckLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}