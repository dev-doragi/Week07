using DG.Tweening;
using UnityEngine;

public class CrashMovement : MonoBehaviour
{
    private Sequence _sequence;
    private Vector3 _startPosition;

    private void Start()
    {
        _startPosition = transform.position;
        PlayCrashSequence();
    }

    [ContextMenu("Play Crash Sequence")]
    public void PlayCrashSequence()
    {
        _sequence?.Kill();
        // 왼쪽 당길 상대 위치
        Vector3 leftTarget = _startPosition + new Vector3(-1f, 0f, 0f);
        // 충돌하러 갈 위치
        Vector3 rightTarget = _startPosition + new Vector3(10f, 0f, 0f);

        _sequence = DOTween.Sequence();
        // 돌진 전 사전 모션
        _sequence.Append(transform.DOMove(leftTarget, 0.8f).SetEase(Ease.OutCubic));
        // 돌진 과정
        _sequence.Append(transform.DOMove(rightTarget, 1f).SetEase(Ease.InExpo));
        // 충돌로 인한 떨림| 흔들림과 관련된 물리 버그 가능성 있음. 없어도 괜찮음)
        _sequence.Append(transform.DOPunchScale(new Vector3(1f, 1f, 1f), 1f, 10, 1f).SetEase(Ease.OutQuad));
        // 돌진 후 복귀
        _sequence.Append(transform.DOMove(_startPosition, 2.5f).SetEase(Ease.InQuad));
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
    }
}
