using UnityEngine;

[System.Serializable]
public class MouseUIMovement
{
    [SerializeField] private Vector2 moveSpeedRange = new Vector2(0.6f, 1.6f);
    [SerializeField] private Vector2 idleDelayRange = new Vector2(0.25f, 1.2f);
    [SerializeField] private float wiggleAmount = 0.09f;
    [SerializeField] private float wiggleSpeed = 20f;
    [SerializeField] private float bounceAmount = 0.08f;
    [SerializeField] private float bounceSpeed = 16f;
    [SerializeField] private float stretchAmount = 0.08f;
    [SerializeField] private float idleBreathAmount = 0.025f;
    [SerializeField] private float idleBreathSpeed = 3.2f;

    private Vector2 targetPosition;
    private float currentMoveSpeed;
    private float restEndTime;
    private float animationSeed;
    private bool hasAnimationSeed;
    private bool hasDestination;
    private bool isResting;

    // The UI container the mouse is allowed to roam inside
    private RectTransform _bounds;

    public void Init(RectTransform bounds)
    {
        _bounds = bounds;
    }

    public void UpdateMovement(RectTransform rectTransform)
    {
        if (rectTransform == null || _bounds == null) return;

        EnsureAnimationSeed();

        if (!hasDestination)
            ChooseNextDestination();

        if (isResting)
        {
            if (Time.time < restEndTime)
            {
                ApplyIdleAnimation(rectTransform);
                return;
            }

            ChooseNextDestination();
        }

        Vector2 moveDirection = targetPosition - rectTransform.anchoredPosition;

        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            BeginRest(rectTransform.anchoredPosition);
            ApplyIdleAnimation(rectTransform);
            return;
        }

        // Flip facing direction
        if (Mathf.Abs(moveDirection.x) > 0.01f)
        {
            float facingX = moveDirection.x >= 0f ? 1f : -1f;
            rectTransform.localScale = new Vector3(
                Mathf.Abs(rectTransform.localScale.x) * facingX,
                rectTransform.localScale.y,
                rectTransform.localScale.z
            );
        }

        rectTransform.anchoredPosition = Vector2.MoveTowards(
            rectTransform.anchoredPosition,
            targetPosition,
            currentMoveSpeed * Time.deltaTime * 100f // multiply since UI units are larger
        );

        Vector2 remaining = targetPosition - rectTransform.anchoredPosition;
        if (remaining.sqrMagnitude > 0.0001f)
            ApplyMovingAnimation(rectTransform);
        else
        {
            BeginRest(rectTransform.anchoredPosition);
            ApplyIdleAnimation(rectTransform);
        }
    }

    public void ResetMotion(RectTransform rectTransform)
    {
        if (rectTransform == null) return;

        targetPosition = rectTransform.anchoredPosition;
        currentMoveSpeed = Random.Range(moveSpeedRange.x, moveSpeedRange.y);
        restEndTime = 0f;
        hasDestination = false;
        isResting = false;
        EnsureAnimationSeed();
        ResetVisualWiggle(rectTransform);
    }

    public void ResetVisualWiggle(RectTransform rectTransform)
    {
        if (rectTransform == null) return;

        rectTransform.localRotation = Quaternion.identity;
        ApplyScale(rectTransform, 0f, 0f);
    }

    private void ChooseNextDestination()
    {
        if (_bounds == null) return;

        // Pick a random point within the bounds rect
        Rect rect = _bounds.rect;
        float x = Random.Range(rect.xMin, rect.xMax);
        float y = Random.Range(rect.yMin, rect.yMax);
        targetPosition = new Vector2(x, y);

        currentMoveSpeed = Random.Range(moveSpeedRange.x, moveSpeedRange.y);
        hasDestination = true;
        isResting = false;
    }

    private void BeginRest(Vector2 currentPosition)
    {
        targetPosition = currentPosition;
        restEndTime = Time.time + Random.Range(idleDelayRange.x, idleDelayRange.y);
        isResting = true;
        hasDestination = true;
    }

    private void ApplyMovingAnimation(RectTransform rectTransform)
    {
        float animationTime = Time.time * bounceSpeed + animationSeed;
        float bounce = Mathf.Sin(animationTime);
        float wiggle = Mathf.Sin(Time.time * wiggleSpeed + animationSeed * 0.5f) * wiggleAmount;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, wiggle * 30f);
        ApplyScale(rectTransform, bounce * stretchAmount, bounceAmount * Mathf.Abs(bounce));
    }

    private void ApplyIdleAnimation(RectTransform rectTransform)
    {
        float breath = Mathf.Sin(Time.time * idleBreathSpeed + animationSeed) * idleBreathAmount;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, breath * 8f);
        ApplyScale(rectTransform, breath * 0.5f, 0f);
    }

    private void ApplyScale(RectTransform rectTransform, float stretchOffset, float squashOffset)
    {
        float facingX = Mathf.Sign(rectTransform.localScale.x);
        if (Mathf.Approximately(facingX, 0f)) facingX = 1f;

        float widthScale = Mathf.Max(0.85f, 1f - stretchOffset + squashOffset);
        float heightScale = Mathf.Max(0.85f, 1f + stretchOffset - squashOffset);

        rectTransform.localScale = new Vector3(
            facingX * widthScale,
            heightScale,
            rectTransform.localScale.z
        );
    }

    private void EnsureAnimationSeed()
    {
        if (hasAnimationSeed) return;

        animationSeed = Random.Range(0f, Mathf.PI * 2f);
        hasAnimationSeed = true;
    }
}