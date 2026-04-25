using UnityEngine;

/// <summary>
/// 적 유닛 사망 시 드랍되는 보상 재화 오브젝트입니다.
/// 아군 유닛에 닿으면 자동 수집됩니다.
/// </summary>
public class DropRat : MonoBehaviour
{
    [SerializeField] private Rigidbody2D _rigid;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Animator _animator;
    [SerializeField] private Sprite _aliveSprite;
    [SerializeField] private Sprite _runningSprite;
    [SerializeField] private float _knockBackPower = 4f;
    [SerializeField] private float _moveSpeed = 1f;
    [SerializeField] private int _rewardAmount = 2;
    [SerializeField] private LayerMask _groundLayerMask;

    private bool _isMoving;
    private bool _isCollected;

    private void OnEnable()
    {
        _isMoving = false;
        _isCollected = false;

        _rigid.linearVelocity = Vector2.zero;
        _rigid.angularVelocity = 0f;

        Vector2 explosionDir = new Vector2(Random.Range(-1f, -0.2f), Random.Range(0.5f, 1.5f));
        _rigid.AddForce(explosionDir * _knockBackPower, ForceMode2D.Impulse);

        if (_spriteRenderer != null) _spriteRenderer.sprite = _aliveSprite;
        if (_animator != null) _animator.Rebind();
    }

    private void OnDisable()
    {
        _isMoving = false;
        _isCollected = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null) return;

        if (!_isMoving && CheckLayerInMask(collision.gameObject.layer, _groundLayerMask))
        {
            _isMoving = true;
            if (_animator != null) _animator.SetTrigger("Run");
            if (_spriteRenderer != null) _spriteRenderer.sprite = _runningSprite;
            return;
        }

        if (_isCollected) return;

        if (collision.gameObject.TryGetComponent(out Unit unit) && unit.Team == TeamType.Player)
        {
            Collect();
        }
    }

    private void Update()
    {
        if (_isMoving && _rigid != null)
        {
            _rigid.linearVelocity = new Vector2(-_moveSpeed, _rigid.linearVelocity.y);
        }
    }

    private void Collect()
    {
        if (_isCollected) return;
        _isCollected = true;

        ResourceManager.Instance?.AddMouseCount(_rewardAmount);
        PoolManager.Instance.Despawn(gameObject);
    }

    private bool CheckLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}