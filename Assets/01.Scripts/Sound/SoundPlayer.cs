using UnityEngine;
using System.Collections;

public class SoundPlayer : MonoBehaviour
{
    [Header("SFX Volume")]
    [SerializeField, Range(0f, 1f)] private float _masterSfxVolume = 0.5f;

    private AudioSource _source;
    private Coroutine _returnRoutine;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();

        if (_source == null)
        {
            _source = gameObject.AddComponent<AudioSource>();
            Debug.LogWarning("[SoundPlayer] AudioSource가 없어서 추가했습니다.", gameObject);
        }

        _source.spatialBlend = 0f;  // 2D 오디오
        _source.loop = false;        // 루프 안 함
        _source.playOnAwake = false; // 자동 재생 안 함
    }



    public void Play(AudioClip clip, float volume = 1f)
    {
        if (_source == null) return;
        if (clip == null) return;

        if (_returnRoutine != null)
            StopCoroutine(_returnRoutine);

        _source.clip = clip;
        _source.volume = Mathf.Clamp01(volume) * Mathf.Clamp01(_masterSfxVolume);
        _source.Play();
        _returnRoutine = StartCoroutine(ReturnToPoolRoutine(clip.length));
    }

    private IEnumerator ReturnToPoolRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (_source != null && _source.isPlaying)
            _source.Stop();

        _returnRoutine = null;

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Despawn(gameObject);
            Debug.Log("[SoundPlayer] 풀로 반환됨");
        }
        else
        {
            Debug.LogError("[SoundPlayer] PoolManager 없음!");
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        if (_returnRoutine != null)
        {
            StopCoroutine(_returnRoutine);
            _returnRoutine = null;
        }

        if (_source != null)
            _source.Stop();
    }
}
