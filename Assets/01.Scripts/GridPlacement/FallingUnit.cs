using System.Collections;
using UnityEngine;

public class FallingUnit : MonoBehaviour
{
    // 연출 파라미터
    private const float LIFETIME = 1.2f;
    private const float JUMP_VELOCITY = 5f;
    private const float HORIZONTAL_SPEED = 2.5f;
    private const float GRAVITY = 15f;
    private const float SPIN_SPEED = 180f;
    
    [SerializeField] private int _orderInLayerBoost = 10; // 화면 앞으로 튀어나갈 레이어 오프셋

    public void Begin()
    {
        StartCoroutine(FallRoutine());
    }

    private IEnumerator FallRoutine()
    {
        float direction = Random.value < 0.5f ? -1f : 1f;
        float vSpeed = JUMP_VELOCITY;
        float elapsed = 0f;

        var sr = GetComponent<SpriteRenderer>();
        Color baseColor = sr != null ? sr.color : Color.white;

        // Order in Layer 증가
        if (sr != null)
            sr.sortingOrder += _orderInLayerBoost;

        while (elapsed < LIFETIME)
        {
            float dt = Time.deltaTime;

            // 수직: 중력 적용
            vSpeed -= GRAVITY * dt;
            transform.position += new Vector3(
                HORIZONTAL_SPEED * direction * dt,
                vSpeed * dt, 0f);

            // 회전 (튀는 느낌 강조)
            transform.Rotate(0f, 0f, SPIN_SPEED * direction * dt);

            // 페이드 아웃 (마지막 30% 구간에서)
            if (sr != null)
            {
                float fadeT = Mathf.InverseLerp(LIFETIME * 0.7f, LIFETIME, elapsed);
                sr.color = new Color(baseColor.r, baseColor.g, baseColor.b,
                    baseColor.a * (1f - fadeT));
            }

            elapsed += dt;
            yield return null;
        }
        Destroy(gameObject);
    }
}
