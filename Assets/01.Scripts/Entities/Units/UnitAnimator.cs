using System.Collections;
using UnityEngine;

// 스크립트 기반 유닛 애니메이션(Animator Controller 필요없음)
public class UnitAnimator : MonoBehaviour
{
    private const float FPS = 15f;
    private static readonly WaitForSeconds WaitPerFrame = new WaitForSeconds(1f / FPS);

    [Header("Idle")]
    [Tooltip("Idle 스프라이트 프레임 (1장이면 정지 이미지)")]
    [SerializeField] private Sprite[] _idleFrames;

    [Header("Attack")]
    [Tooltip("Attack 스프라이트 프레임 (비워두면 공격 애니 없음)")]
    [SerializeField] private Sprite[] _attackFrames;

    private SpriteRenderer _sr;
    private Coroutine _currentAnim;

    private void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()
    {
        PlayIdle();
    }

    // 외부 API
    public void PlayAttack(float duration = 0f)
    {
        if (_attackFrames == null || _attackFrames.Length == 0) return;
        StopCurrent();
        _currentAnim = StartCoroutine(PlayOnce(_attackFrames, PlayIdle));
    }

    public void PlayIdle()
    {
        StopCurrent();
        if (_idleFrames == null || _idleFrames.Length == 0) return;

        if (_idleFrames.Length == 1)
        {
            _sr.sprite = _idleFrames[0];
            return;
        }
        _currentAnim = StartCoroutine(PlayLoop(_idleFrames));
    }

    // 내부 코루틴 — 30FPS 고정
    private IEnumerator PlayLoop(Sprite[] frames)
    {
        int index = 0;
        while (true)
        {
            _sr.sprite = frames[index];
            index = (index + 1) % frames.Length;
            yield return WaitPerFrame;
        }
    }

    private IEnumerator PlayOnce(Sprite[] frames, System.Action onComplete)
    {
        for (int i = 0; i < frames.Length; i++)
        {
            _sr.sprite = frames[i];
            yield return WaitPerFrame;
        }
        onComplete?.Invoke();
    }

    private void StopCurrent()
    {
        if (_currentAnim != null)
        {
            StopCoroutine(_currentAnim);
            _currentAnim = null;
        }
    }

    // 디버그용
    [ContextMenu("Test Attack")]
    private void TestAttack() => PlayAttack();
}
