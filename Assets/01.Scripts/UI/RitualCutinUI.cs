using DG.Tweening;
using UnityEngine;

public class RitualCutinUI : MonoBehaviour
{
    private enum ReplayPolicy
    {
        Restart = 0,
        Ignore = 1
    }

    [System.Serializable]
    private class SkillCutinEntry
    {
        public int skillIndex;
        public RectTransform cutinImage;
    }

    [Header("Skill Cutin Images")]
    [SerializeField] private SkillCutinEntry[] _entries = new SkillCutinEntry[3];

    [Header("Motion")]
    [SerializeField, Min(0.05f)] private float _duration = 0.9f;
    [SerializeField, Min(0f)] private float _holdDuration = 0.35f;
    [SerializeField, Min(0.05f)] private float _exitDuration = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _exitTargetAlpha = 0f;
    [SerializeField, Min(0f)] private float _offscreenPadding = 48f;
    [SerializeField, Range(0f, 1f)] private float _targetViewportX = 0.33f;
    [SerializeField] private AnimationCurve _moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve _exitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private ReplayPolicy _replayPolicy = ReplayPolicy.Restart;

    private Sequence _playSequence;
    private RectTransform _currentImage;

    public void Play(int skillIndex)
    {
        RectTransform image = ResolveImage(skillIndex);
        if (image == null)
        {
            Debug.LogWarning($"[RitualCutinUI] 컷인 이미지 미지정 | skillIndex: {skillIndex}");
            return;
        }

        if (_playSequence != null)
        {
            if (_replayPolicy == ReplayPolicy.Ignore)
            {
                Debug.Log($"[RitualCutinUI] 재생 무시 | 정책: Ignore | skillIndex: {skillIndex}");
                return;
            }

            StopCurrentPlayback(true);
        }

        PlaySequence(image, skillIndex);
    }

    private void PlaySequence(RectTransform image, int skillIndex)
    {
        _currentImage = image;
        image.gameObject.SetActive(true);

        Vector2 startPos = GetStartPosition(image);
        Vector2 endPos = GetEndPosition(image);
        image.anchoredPosition = startPos;
        CanvasGroup canvasGroup = EnsureCanvasGroup(image);
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        Debug.Log($"[RitualCutinUI] 컷인 시작 | skillIndex: {skillIndex}");

        float duration = Mathf.Max(0.05f, _duration);
        float exitDuration = Mathf.Max(0.05f, _exitDuration);

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(UpdateType.Normal, true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        Tween moveInTween = image.DOAnchorPos(endPos, duration);
        ApplyCurveEase(moveInTween, _moveCurve, Ease.OutCubic);
        sequence.Append(moveInTween);

        if (_holdDuration > 0f)
            sequence.AppendInterval(_holdDuration);

        Tween moveOutTween = image.DOAnchorPos(startPos, exitDuration);
        ApplyCurveEase(moveOutTween, _exitCurve, Ease.InCubic);
        sequence.Append(moveOutTween);

        if (canvasGroup != null)
        {
            Tween fadeOutTween = canvasGroup.DOFade(_exitTargetAlpha, exitDuration);
            ApplyCurveEase(fadeOutTween, _exitCurve, Ease.InCubic);
            sequence.Join(fadeOutTween);
        }

        _playSequence = sequence;

        sequence.OnComplete(() =>
        {
            image.anchoredPosition = startPos;
            if (canvasGroup != null)
                canvasGroup.alpha = _exitTargetAlpha;
            image.gameObject.SetActive(false);
            if (_currentImage == image)
            {
                _currentImage = null;
            }

            if (_playSequence == sequence)
                _playSequence = null;

            Debug.Log($"[RitualCutinUI] 컷인 종료 | skillIndex: {skillIndex}");
        });

        sequence.OnKill(() =>
        {
            if (_playSequence == sequence)
                _playSequence = null;
        });
    }

    private void StopCurrentPlayback(bool hideCurrentImage)
    {
        _playSequence?.Kill();

        if (hideCurrentImage && _currentImage != null)
            _currentImage.gameObject.SetActive(false);

        _currentImage = null;
    }

    private static void ApplyCurveEase(Tween tween, AnimationCurve curve, Ease fallbackEase)
    {
        if (tween == null)
            return;

        if (curve != null)
            tween.SetEase(curve);
        else
            tween.SetEase(fallbackEase);
    }

    private RectTransform ResolveImage(int skillIndex)
    {
        if (_entries == null)
            return null;

        for (int i = 0; i < _entries.Length; i++)
        {
            SkillCutinEntry entry = _entries[i];
            if (entry == null || entry.skillIndex != skillIndex)
                continue;

            return entry.cutinImage;
        }

        return null;
    }

    private Vector2 GetStartPosition(RectTransform image)
    {
        RectTransform parent = image.parent as RectTransform;
        if (parent == null)
            return image.anchoredPosition;

        float parentMinX = -parent.rect.width * parent.pivot.x;
        float halfImageWidth = image.rect.width * image.pivot.x;
        float x = parentMinX - halfImageWidth - _offscreenPadding;
        return new Vector2(x, image.anchoredPosition.y);
    }

    private Vector2 GetEndPosition(RectTransform image)
    {
        RectTransform parent = image.parent as RectTransform;
        if (parent == null)
            return image.anchoredPosition;

        float parentMinX = -parent.rect.width * parent.pivot.x;
        float parentMaxX = parent.rect.width * (1f - parent.pivot.x);
        float viewportTargetX = Mathf.Clamp01(_targetViewportX);
        float x = Mathf.Lerp(parentMinX, parentMaxX, viewportTargetX);
        return new Vector2(x, image.anchoredPosition.y);
    }

    private static CanvasGroup EnsureCanvasGroup(RectTransform image)
    {
        if (image == null)
            return null;

        CanvasGroup group = image.GetComponent<CanvasGroup>();
        if (group == null)
            group = image.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    private void OnDisable()
    {
        StopCurrentPlayback(true);
    }
}
