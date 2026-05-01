using System.Collections;
using UnityEngine;

public class HighlightModule : ITutorialModule
{
    private HighlightModuleConfig _config;
    private readonly MonoBehaviour _coroutineHost;
    private readonly RectTransform _highlighter;

    private RectTransform _target;
    private RectTransform _highlighterParent;
    private Coroutine _routineCoroutine;
    private readonly Vector3[] _corners = new Vector3[4];

    // Static 모드에서 사용할 스냅샷
    private Vector3 _staticWorldPosition;
    private Vector2 _staticSize;

    public HighlightModule(MonoBehaviour coroutineHost, RectTransform highlighter)
    {
        _coroutineHost = coroutineHost;
        _highlighter = highlighter;
    }

    public void Initialize(TutorialStep step)
    {
        _config = step?.HighlightConfig;
        _target = _config?.TargetUI;
        _highlighterParent = _highlighter != null ? _highlighter.parent as RectTransform : null;
    }

    public IEnumerator Execute()
    {
        if (_config == null || _target == null || _highlighter == null)
            yield break;

        _highlighter.gameObject.SetActive(true);

        // 이전 코루틴 있으면 정리
        if (_routineCoroutine != null && _coroutineHost != null)
            _coroutineHost.StopCoroutine(_routineCoroutine);

        // 모드 분기: StaticMarker(스냅샷) / FollowTarget(추적)
        if (_config.TrackingMode == HighlightTrackingMode.StaticMarker)
        {
            // Static 모드는 비활성화된 가짜 마커의 위치/크기 값을 그대로 사용해야 함
            // 위치는 world position, 크기는 sizeDelta(가짜 마커에 설정된 값)로 스냅샷
            _staticWorldPosition = _target.position;
            _staticSize = _target.sizeDelta;

            if (_coroutineHost != null)
                _routineCoroutine = _coroutineHost.StartCoroutine(StaticPulseRoutine());
        }
        else // FollowTarget
        {
            // Follow 모드는 실제 UI가 활성화된 경우에만 정상 작동하도록 매 프레임 갱신
            if (_highlighterParent == null)
            {
                yield break;
            }

            if (_coroutineHost != null)
                _routineCoroutine = _coroutineHost.StartCoroutine(FollowAndPulseRoutine());
        }

        yield break;
    }

    public void Cleanup()
    {
        if (_routineCoroutine != null)
        {
            _coroutineHost?.StopCoroutine(_routineCoroutine);
            _routineCoroutine = null;
        }

        if (_highlighter != null)
            _highlighter.gameObject.SetActive(false);
    }

    private IEnumerator StaticPulseRoutine()
    {
        // Static은 가짜 마커 스냅샷을 사용하므로 active 여부 검사 없이 위치/크기 고정
        while (true)
        {
            // 대상이 아예 null이 되면 종료
            if (_target == null)
            {
                _highlighter.gameObject.SetActive(false);
                yield break;
            }

            _highlighter.gameObject.SetActive(true);

            float t = (Mathf.Sin(Time.unscaledTime * _config.PulseSpeed) + 1f) * 0.5f;
            float scale = Mathf.Lerp(1f, _config.PulseScale, t);

            // world position을 그대로 사용 (Canvas Overlay일 경우에도 position 값으로 충분히 동작)
            _highlighter.position = _staticWorldPosition;
            _highlighter.sizeDelta = _staticSize * scale;

            yield return null;
        }
    }

    private IEnumerator FollowAndPulseRoutine()
    {
        var canvas = _highlighterParent != null ? _highlighterParent.GetComponentInParent<Canvas>() : null;
        Camera eventCamera = GetEventCamera(canvas);

        while (true)
        {
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                // 실제 UI가 비활성화되면 하이라이터 숨김
                _highlighter.gameObject.SetActive(false);
                yield return null;
                continue;
            }

            _highlighter.gameObject.SetActive(true);

            _target.GetWorldCorners(_corners);

            Vector2 minScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, _corners[0]);
            Vector2 maxScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, _corners[2]);
            Vector2 centerScreen = (minScreen + maxScreen) * 0.5f;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_highlighterParent, centerScreen, eventCamera, out var localCenter);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_highlighterParent, minScreen, eventCamera, out var localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_highlighterParent, maxScreen, eventCamera, out var localMax);

            Vector2 localSize = new Vector2(Mathf.Abs(localMax.x - localMin.x), Mathf.Abs(localMax.y - localMin.y));

            float t = (Mathf.Sin(Time.unscaledTime * _config.PulseSpeed) + 1f) * 0.5f;
            float scale = Mathf.Lerp(1f, _config.PulseScale, t);

            _highlighter.anchoredPosition = localCenter;
            _highlighter.sizeDelta = localSize * scale;

            yield return null;
        }
    }

    private Camera GetEventCamera(Canvas canvas)
    {
        if (canvas == null) return null;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }
}