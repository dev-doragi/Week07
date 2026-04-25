using System.Collections;
using UnityEngine;

// 스크립트 기반 유닛 애니메이션(Animator Controller 필요없음)
public class UnitAnimator : MonoBehaviour
{
    [Header("Idle")]
    [Tooltip("Idle 스프라이트 프레임 (1장이면 정지 이미지)")]
    [SerializeField] private Sprite[] _idleFrames;
    [SerializeField] private float _idleDuration = 1f;

    [Header("Attack")]
    [Tooltip("Attack 스프라이트 프레임 (비워두면 공격 애니 없음)")]
    [SerializeField] private Sprite[] _attackFrames;
    
    [Tooltip("Attack 애니메이션 총 재생 시간 (초). 0이면 SO 공격 속도에 맞춤")]
    [SerializeField] private float _attackDuration = 0f;

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

    //외부 API
    public void PlayAttack(float duration = 0f)
    {
        if(_attackFrames == null || _attackFrames.Length == 0) return;
        StopCurrent();

        float d = duration > 0f ? duration : _attackDuration;
        if(d <= 0f) d = 0.5f;   // 안전 기본값

        _currentAnim = StartCoroutine(PlayOnce(_attackFrames, d, PlayIdle));
    }

    public void PlayIdle()
    {
        StopCurrent();
        if(_idleFrames == null || _idleFrames.Length == 0) return;

        if(_idleFrames.Length == 1)
        {
            _sr.sprite = _idleFrames[0];
            return;
        }
        _currentAnim = StartCoroutine(PlayLoop(_idleFrames, _idleDuration));
    }

    // 내부 코루틴
    private IEnumerator PlayLoop(Sprite[] frames, float totalDuration)
    {
        float interval = totalDuration / frames.Length;
        int index = 0;
        while(true)
        {
            _sr.sprite = frames[index];
            index = (index + 1) % frames.Length;
            yield return new WaitForSeconds(interval);
        }
    }

    private IEnumerator PlayOnce(Sprite[] frames, float totalDuration, System.Action onComplete)
    {
        float interval = totalDuration / frames.Length;
        for(int i = 0; i < frames.Length; i++)
        {
            _sr.sprite = frames[i];
            yield return new WaitForSeconds(interval);
        }
        onComplete?.Invoke();
    }

    private void StopCurrent()
    {
        if(_currentAnim != null)
        {
            StopCoroutine(_currentAnim);
            _currentAnim = null;
        }
    }

    //디버그용
    [ContextMenu("Test Attack")]
    private void TestAttack() => PlayAttack();
}
