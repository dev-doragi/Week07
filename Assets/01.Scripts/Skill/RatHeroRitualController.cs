using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class RatHeroRitualController : MonoBehaviour
{
    [Header("EventBus Listen")]
    [SerializeField] private bool _listenEventBus = true;

    [Header("RatHero Sprite Target")]
    [SerializeField] private SpriteRenderer _targetSpriteRenderer;
    [SerializeField] private bool _findSpriteRendererInChildren = true;

    [Header("Skill1 Sprite (n seconds)")]
    [SerializeField] private Sprite _skill1Sprite;
    [SerializeField, Min(0f)] private float _skill1SpriteDuration = 1.2f;

    [Header("Skill2 Sprite (n seconds)")]
    [SerializeField] private Sprite _skill2Sprite;
    [SerializeField, Min(0f)] private float _skill2SpriteDuration = 1.2f;

    [Header("Skill3 Sprite/Move (m seconds)")]
    [SerializeField] private Sprite _skill3SpecialSprite;
    [SerializeField, Min(0f)] private float _skill3SpecialSpriteDuration = 2f;
    [SerializeField] private Transform _skill3MoveTarget;
    [SerializeField] private bool _useSkill3MoveTarget = true;
    [SerializeField] private Vector3 _skill3MovePosition;

    [Header("Sprite Swap Motion")]
    [SerializeField] private bool _useSwapMotion = true;
    [SerializeField, Min(0.01f)] private float _swapMotionDuration = 0.2f;
    [SerializeField] private Vector3 _swapPunchScale = new Vector3(0.12f, 0.12f, 0f);
    [SerializeField, Min(1)] private int _swapPunchVibrato = 8;
    [SerializeField, Range(0f, 1f)] private float _swapPunchElasticity = 0.75f;

    private readonly Dictionary<int, int> _skillLevels = new Dictionary<int, int>
    {
        { 1, 0 },
        { 2, 0 },
        { 3, 0 }
    };

    private Sprite _defaultSprite;
    private Coroutine _spriteRoutine;
    private int _spriteChangeVersion;
    private Tween _swapMotionTween;
    private Vector3 _defaultVisualScale = Vector3.one;
    private bool _hasDefaultVisualScale;

    private void Awake()
    {
        ResolveSpriteRenderer();
        CacheDefaultSprite();
    }

    private void Start()
    {
        RitualSystem ritualSystem = FindAnyObjectByType<RitualSystem>();
        if (ritualSystem != null)
        {
            SyncSkillLevels(ritualSystem);
        }
    }

    private void OnEnable()
    {
        if (!_listenEventBus)
            return;

        EventBus.Instance?.Subscribe<RitualSkillCastEvent>(OnRitualSkillCast);
        EventBus.Instance?.Subscribe<RitualSkillLevelChangedEvent>(OnRitualSkillLevelChanged);
    }

    private void OnDisable()
    {
        if (_listenEventBus)
        {
            EventBus.Instance?.Unsubscribe<RitualSkillCastEvent>(OnRitualSkillCast);
            EventBus.Instance?.Unsubscribe<RitualSkillLevelChangedEvent>(OnRitualSkillLevelChanged);
        }

        if (_spriteRoutine != null)
        {
            StopCoroutine(_spriteRoutine);
            _spriteRoutine = null;
        }

        _swapMotionTween?.Kill();
        _swapMotionTween = null;
    }

    public void OnSkill1CastFromRitual(int skillLevel)
    {
        ApplySkillLevel(1, skillLevel);
        PlayTemporarySprite(_skill1Sprite, _skill1SpriteDuration, 1, skillLevel);
    }

    public void OnSkill2CastFromRitual(int skillLevel)
    {
        ApplySkillLevel(2, skillLevel);
        PlayTemporarySprite(_skill2Sprite, _skill2SpriteDuration, 2, skillLevel);
    }

    public void OnSkill3CastFromRitual(int skillLevel)
    {
        ApplySkillLevel(3, skillLevel);
        MoveToSkill3Position();
        PlayTemporarySprite(_skill3SpecialSprite, _skill3SpecialSpriteDuration, 3, skillLevel);
    }

    public void OnSkillLevelChangedFromRitual(int skillIndex, int skillLevel)
    {
        ApplySkillLevel(skillIndex, skillLevel);
    }

    public int GetSkillLevel(int skillIndex)
    {
        if (_skillLevels.TryGetValue(skillIndex, out int level))
            return level;

        return 0;
    }

    public void SyncSkillLevels(RitualSystem ritualSystem)
    {
        if (ritualSystem == null)
            return;

        ApplySkillLevel(1, ritualSystem.GetSkillLevel(1));
        ApplySkillLevel(2, ritualSystem.GetSkillLevel(2));
        ApplySkillLevel(3, ritualSystem.GetSkillLevel(3));
    }

    private void OnRitualSkillCast(RitualSkillCastEvent evt)
    {
        switch (evt.SkillIndex)
        {
            case 1:
                OnSkill1CastFromRitual(evt.SkillLevel);
                break;

            case 2:
                OnSkill2CastFromRitual(evt.SkillLevel);
                break;

            case 3:
                OnSkill3CastFromRitual(evt.SkillLevel);
                break;
        }
    }

    private void OnRitualSkillLevelChanged(RitualSkillLevelChangedEvent evt)
    {
        ApplySkillLevel(evt.SkillIndex, evt.SkillLevel);
    }

    private void ResolveSpriteRenderer()
    {
        if (_targetSpriteRenderer != null)
            return;

        if (_findSpriteRendererInChildren)
            _targetSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        else
            _targetSpriteRenderer = GetComponent<SpriteRenderer>();

        if (_targetSpriteRenderer != null)
        {
            if (_defaultSprite == null)
                _defaultSprite = _targetSpriteRenderer.sprite;

            if (!_hasDefaultVisualScale)
            {
                _defaultVisualScale = _targetSpriteRenderer.transform.localScale;
                _hasDefaultVisualScale = true;
            }
        }
    }

    private void CacheDefaultSprite()
    {
        if (_targetSpriteRenderer != null)
        {
            _defaultSprite = _targetSpriteRenderer.sprite;
            _defaultVisualScale = _targetSpriteRenderer.transform.localScale;
            _hasDefaultVisualScale = true;
        }
    }

    private void MoveToSkill3Position()
    {
        Vector3 targetPosition = _useSkill3MoveTarget && _skill3MoveTarget != null
            ? _skill3MoveTarget.position
            : _skill3MovePosition;

        transform.position = targetPosition;
        Debug.Log($"[RatHeroRitualController] Skill3 이동 적용 | 위치: {targetPosition}");
    }

    private void PlayTemporarySprite(Sprite sprite, float duration, int skillIndex, int skillLevel)
    {
        ResolveSpriteRenderer();
        if (_targetSpriteRenderer == null)
        {
            Debug.LogWarning("[RatHeroRitualController] SpriteRenderer가 없어 스프라이트 연출을 적용할 수 없습니다.");
            return;
        }

        if (sprite == null)
        {
            Debug.LogWarning($"[RatHeroRitualController] Skill{skillIndex} 스프라이트가 지정되지 않았습니다.");
            return;
        }

        _spriteChangeVersion++;
        int version = _spriteChangeVersion;

        if (_spriteRoutine != null)
        {
            StopCoroutine(_spriteRoutine);
            _spriteRoutine = null;
        }

        _targetSpriteRenderer.sprite = sprite;
        PlaySwapMotion();
        Debug.Log($"[RatHeroRitualController] Skill{skillIndex} 연출 시작 | 단계: {skillLevel} | 지속: {duration:0.##}초");

        if (duration <= 0f)
            return;

        _spriteRoutine = StartCoroutine(RestoreSpriteRoutine(version, duration, skillIndex));
    }

    private IEnumerator RestoreSpriteRoutine(int version, float duration, int skillIndex)
    {
        yield return new WaitForSeconds(duration);

        if (version != _spriteChangeVersion)
            yield break;

        if (_targetSpriteRenderer != null)
        {
            _targetSpriteRenderer.sprite = _defaultSprite;
            PlaySwapMotion();
        }

        _spriteRoutine = null;
        Debug.Log($"[RatHeroRitualController] Skill{skillIndex} 연출 종료");
    }

    private void ApplySkillLevel(int skillIndex, int skillLevel)
    {
        if (!_skillLevels.ContainsKey(skillIndex))
            return;

        _skillLevels[skillIndex] = Mathf.Max(0, skillLevel);
    }

    private void PlaySwapMotion()
    {
        if (!_useSwapMotion || _targetSpriteRenderer == null)
            return;

        Transform visual = _targetSpriteRenderer.transform;
        _swapMotionTween?.Kill();
        visual.localScale = _defaultVisualScale;

        _swapMotionTween = visual
            .DOPunchScale(_swapPunchScale, _swapMotionDuration, _swapPunchVibrato, _swapPunchElasticity)
            .SetUpdate(UpdateType.Normal)
            .OnKill(() =>
            {
                if (visual != null)
                    visual.localScale = _defaultVisualScale;
            });
    }
}
