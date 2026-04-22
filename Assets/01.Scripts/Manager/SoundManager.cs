using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-96)]
/// <summary>
/// 전역 배경음(BGM)과 효과음(SFX) 재생을 담당하는 오디오 관리자입니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - 게임 상태 및 인게임 흐름 상태에 따른 적절한 BGM 자동 전환
/// - PoolManager를 활용한 SFX 재생
///
/// [이벤트 흐름]
/// - Subscribe: GameStateChangedEvent, InGameStateChangedEvent, PlaySFXEvent
/// </remarks>
public class SoundManager : Singleton<SoundManager>
{
    [Header("Pool Setup")]
    [SerializeField] private GameObject _soundPlayerPrefab;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSource;

    [Header("BGM Assets")]
    [SerializeField] private AudioClip _lobbyBGM;
    [SerializeField] private AudioClip _prepareBGM;
    [SerializeField] private AudioClip _waveBGM;
    [SerializeField] private AudioClip _winBGM;

    protected override void Init()
    {
        if (_bgmSource == null)
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
        }
    }

    private void OnEnable()
    {
        // EventBus를 통한 상태 변경 수신
        EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        EventBus.Instance.Subscribe<InGameStateChangedEvent>(OnInGameStateChanged);
        EventBus.Instance.Subscribe<PlaySFXEvent>(OnPlaySFX);
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Instance.Unsubscribe<InGameStateChangedEvent>(OnInGameStateChanged);
            EventBus.Instance.Unsubscribe<PlaySFXEvent>(OnPlaySFX);
        }
    }

    // 전역 게임 상태 변경 시 BGM 처리
    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        if (evt.NewState == GameState.Ready)
            PlayBGM(_lobbyBGM);
    }

    // 인게임 세부 상태 변경 시 BGM 처리
    private void OnInGameStateChanged(InGameStateChangedEvent evt)
    {
        switch (evt.NewState)
        {
            case InGameState.Prepare:
                PlayBGM(_prepareBGM);
                break;
            case InGameState.WavePlaying:
                PlayBGM(_waveBGM);
                break;
            case InGameState.StageCleared:
                PlayBGM(_winBGM);
                break;
        }
    }

    private void OnPlaySFX(PlaySFXEvent evt)
    {
        PlaySFX(evt.Clip, Vector3.zero, evt.Volume);
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || _bgmSource.clip == clip) return;
        _bgmSource.clip = clip;
        _bgmSource.Play();
    }

    /// <summary>
    /// 효과음 재생 (풀링 기반)
    /// </summary>
    public void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        // PoolManager에서 사운드 플레이어 객체 생성
        GameObject obj = PoolManager.Instance.Spawn(_soundPlayerPrefab.name, position, Quaternion.identity);
        if (obj.TryGetComponent(out SoundPlayer player))
        {
            player.Play(clip, volume);
        }
    }
}