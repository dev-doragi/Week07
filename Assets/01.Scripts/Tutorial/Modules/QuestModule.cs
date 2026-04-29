using System.Collections;
using UnityEngine;

/// <summary>
/// 튜토리얼 목표/UI 강조 모듈
/// ✅ 수정: Execute()는 즉시 반환 (비블로킹)
///    하이라이트는 Initialize()에서 시작, Cleanup()에서 제거
///    — 기존 HighlightUI(float.MaxValue) 무한 블로킹으로 인한 데드락 해결
/// </summary>
public class QuestModule : ITutorialModule
{
    private QuestModuleConfig _config;
    private TutorialDialoguePresenter _presenter;

    public QuestModule(TutorialDialoguePresenter presenter)
    {
        _presenter = presenter;
    }

    public void Initialize(TutorialStep step)
    {
        _config = step.QuestConfig;

        // ✅ 하이라이트를 Initialize에서 즉시 시작 (Execute 블로킹 제거)
        if (_presenter != null && _config?.TargetUI != null && _config.HighlightUI)
        {
            _presenter.StartUIHighlight(_config.TargetUI);
        }
    }

    public IEnumerator Execute()
    {
        // ✅ 즉시 반환 — 하이라이트는 백그라운드에서 독립 실행 중
        //    다음 모듈(AutoAdvanceModule 등)이 정상적으로 실행됨
        yield return null;
    }

    public void Cleanup()
    {
        if (_presenter != null)
        {
            _presenter.RemoveUIHighlight();
        }
    }
}
