using System.Collections;
using UnityEngine;

public class HighlightModule : ITutorialModule
{
    private HighlightModuleConfig _config;
    private readonly MonoBehaviour _coroutineHost;
    private readonly RectTransform _highlighter;
    private Vector2 _baseSize;
    private Coroutine _pulseCoroutine;

    public HighlightModule(MonoBehaviour coroutineHost, RectTransform highlighter)
    {
        _coroutineHost = coroutineHost;
        _highlighter = highlighter;
    }

    public void Initialize(TutorialStep step)
    {
        _config = step?.HighlightConfig;
    }

    public IEnumerator Execute()
    {
        if (_config == null || _config.TargetUI == null || _highlighter == null)
            yield break;

        _highlighter.gameObject.SetActive(true);
        _highlighter.position = _config.TargetUI.position;
        _baseSize = _config.TargetUI.sizeDelta;
        _highlighter.sizeDelta = _baseSize;

        if (_pulseCoroutine != null && _coroutineHost != null)
        {
            _coroutineHost.StopCoroutine(_pulseCoroutine);
        }

        if (_coroutineHost != null)
        {
            _pulseCoroutine = _coroutineHost.StartCoroutine(HighlighterPulseRoutine());
        }
        yield break;
    }

    public void Cleanup()
    {
        if (_pulseCoroutine != null)
        {
            _coroutineHost?.StopCoroutine(_pulseCoroutine);

            _pulseCoroutine = null;
        }

        if (_highlighter != null)
        {
            _highlighter.gameObject.SetActive(false);
        }
    }

    private IEnumerator HighlighterPulseRoutine()
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * _config.PulseSpeed) + 1f) / 2f;
            _highlighter.sizeDelta = Vector2.Lerp(_baseSize, _baseSize * _config.PulseScale, t);

            yield return null;
        }
    }
}