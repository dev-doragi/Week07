using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RitualSkillCostFlyVisual : MonoBehaviour
{
    [System.Serializable]
    private class SkillVisualTarget
    {
        [Min(1)] public int SkillIndex = 1;
        public Transform TargetPoint;
        public Sprite Sprite;
    }

    [Header("Shared Spawn Point (A)")]
    [SerializeField] private Transform _spawnPoint;

    [Header("Per Skill Target Point (B) + Sprite")]
    [SerializeField] private List<SkillVisualTarget> _skillTargets = new List<SkillVisualTarget>();

    [Header("UI Root")]
    [SerializeField] private Canvas _rootCanvas;
    [SerializeField] private RectTransform _effectLayer;

    [Header("Motion")]
    [SerializeField] private float _durationMin = 0.45f;
    [SerializeField] private float _durationMax = 0.8f;
    [SerializeField] private float _spawnRadius = 24f;
    [SerializeField] private float _spawnInterval = 0.04f;
    [SerializeField] private AnimationCurve _speedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Render")]
    [SerializeField] private Vector2 _spriteSize = new Vector2(36f, 36f);

    private readonly Dictionary<int, SkillVisualTarget> _targetBySkill = new Dictionary<int, SkillVisualTarget>();

    private void Awake()
    {
        RebuildCache();
    }

    private void OnValidate()
    {
        if (_durationMax < _durationMin)
            _durationMax = _durationMin;
    }

    public void Play(int skillIndex, int skillCost)
    {
        if (_spawnPoint == null || !TryResolveCanvasRoot(out RectTransform canvasRoot))
            return;

        int spawnCount = Mathf.FloorToInt(skillCost / 10f);
        if (spawnCount <= 0)
            return;

        if (!_targetBySkill.TryGetValue(skillIndex, out SkillVisualTarget target) || target == null || target.TargetPoint == null || target.Sprite == null)
            return;

        RectTransform parentRect = _effectLayer != null ? _effectLayer : canvasRoot;
        StartCoroutine(SpawnRoutine(spawnCount, target, parentRect));
    }

    private IEnumerator SpawnRoutine(int spawnCount, SkillVisualTarget target, RectTransform parentRect)
    {
        for (int i = 0; i < spawnCount; i++)
        {
            SpawnOne(target, parentRect);
            if (_spawnInterval > 0f)
                yield return new WaitForSeconds(_spawnInterval);
            else
                yield return null;
        }
    }

    private void SpawnOne(SkillVisualTarget target, RectTransform parentRect)
    {
        Vector2 offset = Random.insideUnitCircle * _spawnRadius;
        Vector2 startPos = GetCanvasLocalPosition(parentRect, _spawnPoint.position) + offset;

        GameObject go = new GameObject("RitualCostVisual");
        go.transform.SetParent(parentRect, false);
        go.transform.SetAsLastSibling();

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = _spriteSize;
        rect.anchoredPosition = startPos;

        Image image = go.AddComponent<Image>();
        image.sprite = target.Sprite;
        image.raycastTarget = false;

        StartCoroutine(FlyRoutine(rect, target.TargetPoint, parentRect));
    }

    private IEnumerator FlyRoutine(RectTransform visual, Transform targetPoint, RectTransform parentRect)
    {
        float duration = Random.Range(_durationMin, _durationMax);
        Vector2 start = visual.anchoredPosition;

        float elapsed = 0f;
        while (elapsed < duration && visual != null && targetPoint != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = _speedCurve.Evaluate(t);
            Vector2 target = GetCanvasLocalPosition(parentRect, targetPoint.position);
            visual.anchoredPosition = Vector2.LerpUnclamped(start, target, curvedT);
            yield return null;
        }

        if (visual != null)
            Destroy(visual.gameObject);
    }

    private void RebuildCache()
    {
        _targetBySkill.Clear();
        for (int i = 0; i < _skillTargets.Count; i++)
        {
            SkillVisualTarget entry = _skillTargets[i];
            if (entry == null || entry.SkillIndex <= 0)
                continue;

            _targetBySkill[entry.SkillIndex] = entry;
        }
    }

    private bool TryResolveCanvasRoot(out RectTransform canvasRoot)
    {
        canvasRoot = null;

        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();

        if (_rootCanvas == null)
            return false;

        canvasRoot = _rootCanvas.transform as RectTransform;
        return canvasRoot != null;
    }

    private Vector2 GetCanvasLocalPosition(RectTransform referenceRect, Vector3 worldPosition)
    {
        Camera cam = _rootCanvas != null && _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _rootCanvas != null ? _rootCanvas.worldCamera : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(referenceRect, screenPoint, cam, out Vector2 localPoint);
        return localPoint;
    }
}
