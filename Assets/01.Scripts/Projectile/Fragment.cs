using System.Collections;
using UnityEngine;

/// <summary>
/// 투사체 피격 후 생성되는 시각적 잔해(탄환/탄피) 효과입니다.
/// </summary>
public class Fragment : MonoBehaviour
{
    [SerializeField] private Rigidbody2D _rigidBody;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Sprite _aliveSprite;
    [SerializeField] private Sprite _deadSprite;
    [SerializeField] private float _knockBackPower = 2f;
    [SerializeField] private float _fadeDuration = 1f;

    private bool _isDead;

    private void OnEnable()
    {
        _isDead = false;            
        _rigidBody.bodyType = RigidbodyType2D.Dynamic;
        _spriteRenderer.sprite = _aliveSprite;
        _spriteRenderer.color = Color.white;

        Vector2 explosionDir = new Vector2(Random.Range(-1f, -0.2f), Random.Range(0.5f, 1.5f));
        _rigidBody.linearVelocity = Vector2.zero;
        _rigidBody.AddForce(explosionDir * _knockBackPower, ForceMode2D.Impulse);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_isDead && collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            _isDead = true;
            _rigidBody.linearVelocity = Vector2.zero;
            _rigidBody.bodyType = RigidbodyType2D.Static;
            _spriteRenderer.sprite = _deadSprite;
            StartCoroutine(FadeOutAndDespawn());
        }
    }

    private IEnumerator FadeOutAndDespawn()
    {
        yield return new WaitForSeconds(1f);
        float elapsed = 0f;
        Color color = _spriteRenderer.color;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(1, 0, elapsed / _fadeDuration);
            _spriteRenderer.color = color;
            yield return null;
        }

        if (gameObject.activeInHierarchy)
            PoolManager.Instance.Despawn(gameObject);
    }
}
