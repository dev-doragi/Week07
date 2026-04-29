using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 튜토리얼 자동 진행 모듈
/// - 일정 시간 후 자동 진행 (delay > 0)
/// - 사용자 클릭 대기 (delay <= 0)
/// - 플레이테스트 모드에서는 조건 무시
/// </summary>
public class AutoAdvanceModule : ITutorialModule
{
    private AutoAdvanceModuleConfig _config;
    private Func<bool> _nextClickedChecker;
    private bool _ignoreConditions;

    public AutoAdvanceModule(float delay, Func<bool> nextClickedChecker, bool ignoreConditions = false)
    {
        _config = new AutoAdvanceModuleConfig { AutoAdvanceDelay = delay };
        _nextClickedChecker = nextClickedChecker;
        _ignoreConditions = ignoreConditions;
    }

    public void Initialize(TutorialStep step)
    {
        if (_config == null)
        {
            _config = step.AutoAdvanceConfig;
        }
    }

    public IEnumerator Execute()
    {
        if (_config == null)
        {
            yield break;
        }

        if (_config.AutoAdvanceDelay > 0f)
        {
            // 자동 진행: 딜레이 또는 클릭 중 먼저 발생하는 것
            float elapsed = 0f;
            while (!_nextClickedChecker?.Invoke() ?? false && elapsed < _config.AutoAdvanceDelay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        else
        {
            // 클릭 대기
            while (!_nextClickedChecker?.Invoke() ?? false)
            {
                yield return null;
            }
        }
    }

    public void Cleanup()
    {
        // 정리할 것 없음
    }
}
