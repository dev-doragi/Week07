using System.Collections;
using UnityEngine;

/// <summary>
/// 튜토리얼 일시정지 모듈
/// - 게임을 일시정지 (timeScale 조절)
/// - 입력 제어 (선택사항)
/// - 모듈 종료 시 자동 재개
/// </summary>
public class PauseModule : ITutorialModule
{
    private PauseModuleConfig _config;
    private bool _inputBlocked = false;
    private bool _gamePaused = false;

    public void Initialize(TutorialStep step)
    {
        _config = step.PauseConfig;
    }

    public IEnumerator Execute()
    {
        if (_config == null)
        {
            yield break;
        }

        // 게임 일시정지
        if (_config.ShouldPause)
        {
            Time.timeScale = 0f;
            _gamePaused = true;
        }

        // 입력 차단
        if (_config.BlockInput && InputReader.Instance != null)
        {
            InputReader.Instance.SetInputBlocked(true);
            _inputBlocked = true;
        }

        // 모듈은 즉시 완료 (다른 모듈과 함께 사용)
        yield return null;
    }

    public void Cleanup()
    {
        // 게임 재개
        if (_gamePaused)
        {
            Time.timeScale = 1f;
            _gamePaused = false;
        }

        // 입력 복구
        if (_inputBlocked && InputReader.Instance != null)
        {
            InputReader.Instance.SetInputBlocked(false);
            _inputBlocked = false;
        }
    }
}
