using System.Collections;
using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    public float speed;
    public int startIndex;
    public int endIndex;
    public Transform[] sprites;


    private float viewHeight;
    private float originalSpeed;
    private Coroutine speedLerpRoutine;

    private void Awake()
    {
        originalSpeed = speed;
        viewHeight = Camera.main.orthographicSize * 4;
 
        originalSpeed = speed;
    }


    public void StopBackground()
    {
        StartSmoothSpeedChange(0,2f);
    }

    public void RestartBackground()
    {
        StartSmoothSpeedChange(originalSpeed,2f);
    }

    private void StartSmoothSpeedChange(float targetSpeed, float duration)
    {
        if (speedLerpRoutine != null)
        {
            StopCoroutine(speedLerpRoutine);
        }

        speedLerpRoutine = StartCoroutine(SmoothSpeedChange(targetSpeed, duration));
    }

    private IEnumerator SmoothSpeedChange(float targetSpeed, float duration)
    {
        if (duration <= 0f)
        {
            speed = targetSpeed;
            speedLerpRoutine = null;
            yield break;
        }

        float startSpeed = speed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            speed = Mathf.Lerp(startSpeed, targetSpeed, t);
            yield return null;
        }

        speed = targetSpeed;
        speedLerpRoutine = null;
    }

    private void Update()
    {
        RecycleBG();
    }

    private void LateUpdate()
    {
        Vector3 curPos = transform.position;
        Vector3 nextPos = Vector3.left * speed * Time.deltaTime;
        transform.position = curPos + nextPos;
    }

    private void RecycleBG()
    {
        if (sprites[endIndex].position.x < viewHeight * (-1))
        {
            Vector3 backSpritesPos = sprites[startIndex].position;
            sprites[endIndex].transform.position = backSpritesPos + Vector3.right * viewHeight;

            int startIndexSave = startIndex;
            startIndex = endIndex;
            endIndex = (startIndexSave - 1 == -1) ? sprites.Length - 1 : startIndexSave - 1;
        }
    }
}
