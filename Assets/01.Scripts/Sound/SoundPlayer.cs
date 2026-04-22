using UnityEngine;
using System.Collections;

public class SoundPlayer : MonoBehaviour
{
    private AudioSource _source;

    private void Awake() => _source = GetComponent<AudioSource>();

    public void Play(AudioClip clip, float volume)
    {
        _source.clip = clip;
        _source.volume = volume;
        _source.Play();

        // 소리 재생이 끝나면 풀로 반환
        StartCoroutine(ReturnToPoolRoutine(clip.length));
    }

    private IEnumerator ReturnToPoolRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        PoolManager.Instance.Despawn(gameObject);
    }
}