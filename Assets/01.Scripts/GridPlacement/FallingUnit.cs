using System.Collections;
using UnityEngine;

public class FallingUnit : MonoBehaviour
{
    // 연출 파라미터
    private const float LIFETIME = 1.2f;        //전체 연출 시간
    private const float JUMP_VELOCITY = 5f;     //초기 상승 속도
    private const float HORIZONTAL_SPEED = 2.5f;    // 좌 / 우 수평 속도
    private const float GRAVITY = 15f;              //중력 가속도
    private const float SPIN_SPEED = 180f;          // 회전 속도 (deg/sec)

    //외부에서 호출 : 연출 시작
    public void Begin()
    {
        //이 프로젝트에 남아있는 비디오/콜라이더 등은 GridManager가 이미 정리함
        StartCoroutine(FallRoutine());   
    }

    private IEnumerator FallRoutine()
    {
        //랜덤 방향 결정 (-1 = 왼쪽, +1 = 오른쪽)
        float direction = Random.value < 0.5f ? -1f : 1f;
        float vSpeed = JUMP_VELOCITY;
        float elapsed = 0f;

        var sr = GetComponent<SpriteRenderer>();
        Color baseColor = sr != null ? sr.color : Color.white;

        while(elapsed < LIFETIME)
        {
            float dt = Time.deltaTime;

            //수직 : 중력 적용
            vSpeed -= GRAVITY * dt;
            transform.position += new Vector3(
                HORIZONTAL_SPEED * direction * dt,
                vSpeed * dt, 0f);

            //회전 (튀는 느낌 강조)
            transform.Rotate(0f, 0f, SPIN_SPEED * direction * dt);

            //페이드 아웃 (마지막 30% 구간에서)
            if(sr != null)
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
