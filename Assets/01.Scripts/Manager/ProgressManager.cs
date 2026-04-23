using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 플레이어의 스테이지 클리어 진행도와 튜토리얼 완료 여부를 영구 저장(PlayerPrefs)하는 매니저입니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - 클리어한 스테이지 인덱스 추적 및 최고 클리어 기록 갱신
/// - 튜토리얼 스킵 및 보상 해금 논리 처리
///
/// [이벤트 흐름]
/// - Subscribe: StageClearedEvent, TutorialCompletedEvent
/// - Publish: StageProgressUpdatedEvent
/// </remarks>

[DefaultExecutionOrder(-190)]
public class ProgressManager : Singleton<ProgressManager>
{
    private const string PREF_KEY = "ClearedStages_v1";
    private const string PREF_TUTORIAL_KEY = "TutorialCompleted_v1";

    private readonly HashSet<int> _clearedStages = new HashSet<int>();
    public IReadOnlyCollection<int> ClearedStages => _clearedStages;
    public int HighestClearedStage { get; private set; } = -1;
    // 튜토리얼 완료 여부 (튜토리얼 보상으로 첫 스테이지만 해금할 때 사용)
    private bool _tutorialCompleted = false;

    protected override void OnBootstrap()
    {
        // 초기화하고 저장된 진행 정보를 로드합니다.
        _clearedStages.Clear();
        HighestClearedStage = -1;
        _tutorialCompleted = false;

        LoadFromPrefs();
    }

    public void SkipTutorial()
    {
        _tutorialCompleted = true;
        SaveToPrefs();
        EventBus.Instance?.Publish(new StageProgressUpdatedEvent { HighestCleared = HighestClearedStage });
    }

    private void OnEnable()
    {
        if (EventBus.Instance == null) return;
        EventBus.Instance.Subscribe<StageClearedEvent>(OnStageCleared);

        // 튜토리얼 완료 이벤트 수신 추가
        EventBus.Instance.Subscribe<TutorialCompletedEvent>(OnTutorialCompleted);
    }

    private void OnDisable()
    {
        if (EventBus.Instance == null) return;
        EventBus.Instance.Unsubscribe<StageClearedEvent>(OnStageCleared);
        EventBus.Instance.Unsubscribe<TutorialCompletedEvent>(OnTutorialCompleted);
    }

    private void OnStageCleared(StageClearedEvent evt)
    {
        // Do not treat tutorial runs as real stage clears.
        // If StageLoadContext indicates we're in tutorial mode, ignore these events.
        if (StageLoadContext.IsTutorial)
        {
            Debug.Log($"[ProgressManager] Ignoring StageClearedEvent for Stage {evt.StageIndex} during tutorial.");
            return;
        }

        MarkStageCleared(evt.StageIndex);
    }

    private void OnTutorialCompleted(TutorialCompletedEvent evt)
    {
        // 튜토리얼 완료 시에는 "튜토리얼 보상으로 첫 스테이지 해금"을
        // 클리어 처리와 구분하여 별도 플래그로 관리합니다.
        // 일반적인 경우 RewardStageIndex == 0 이며, 이때는 실제로 클리어로
        // 기록하지 않고 튜토리얼 플래그만 설정합니다.
        if (evt.RewardStageIndex == 0)
        {
            _tutorialCompleted = true;
            Debug.Log("[ProgressManager] Tutorial completed. Stage 0 unlocked.");
            SaveToPrefs();
            EventBus.Instance?.Publish(new StageProgressUpdatedEvent { HighestCleared = HighestClearedStage });
        }
        else if (evt.RewardStageIndex > 0)
        {
            // 만약 튜토리얼 보상이 0이 아닌 다른 스테이지라면 기존 동작대로 클리어 처리
            MarkStageCleared(evt.RewardStageIndex);
        }
    }

    /// <summary>
    /// 주어진 스테이지 인덱스를 클리어로 표시하고, 변경 시 저장합니다.
    /// </summary>
    public void MarkStageCleared(int stageIndex)
    {
        if (stageIndex < 0) return;

        if (_clearedStages.Add(stageIndex))
        {
            HighestClearedStage = Math.Max(HighestClearedStage, stageIndex);
            SaveToPrefs();
            EventBus.Instance?.Publish(new StageProgressUpdatedEvent { HighestCleared = HighestClearedStage });
        }
    }

    /// <summary>
    /// 지정된 스테이지가 해금되어 있는지 판정합니다.
    /// 규칙:
    /// - stage 0: 튜토리얼 완료(혹은 명시적으로 클리어 처리) 시 해금됩니다.
    /// - 그 외 스테이지 i: i <= HighestClearedStage + 1 이면 해금됩니다.
    /// </summary>
    public bool IsStageUnlocked(int stageIndex)
    {
        if (stageIndex < 0) return false;

        // Stage 0은 기본으로 해금되지 않음. TutorialCompletedEvent로 MarkStageCleared(0)가 호출되어야 해금된다.
        if (stageIndex == 0)
        {
            return _tutorialCompleted || _clearedStages.Contains(0);
        }

        return stageIndex <= (HighestClearedStage + 1);
    }

    private void LoadFromPrefs()
    {
        string raw = PlayerPrefs.GetString(PREF_KEY, string.Empty);
        _clearedStages.Clear();
        HighestClearedStage = -1;

        if (!string.IsNullOrWhiteSpace(raw))
        {
            var parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                if (int.TryParse(p.Trim(), out int idx))
                {
                    _clearedStages.Add(idx);
                }
            }

            if (_clearedStages.Count > 0)
            {
                HighestClearedStage = _clearedStages.Max();
            }
        }

        _tutorialCompleted = PlayerPrefs.GetInt(PREF_TUTORIAL_KEY, 0) == 1;

        // 로드가 끝났음을 알림
        EventBus.Instance?.Publish(new StageProgressUpdatedEvent { HighestCleared = HighestClearedStage });
    }

    private void SaveToPrefs()
    {
        if (_clearedStages.Count == 0)
        {
            PlayerPrefs.DeleteKey(PREF_KEY);
        }
        else
        {
            var ordered = _clearedStages.OrderBy(i => i).Select(i => i.ToString());
            PlayerPrefs.SetString(PREF_KEY, string.Join(",", ordered));
        }

        PlayerPrefs.SetInt(PREF_TUTORIAL_KEY, _tutorialCompleted ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 모든 진행을 지웁니다. (런타임 호출 가능)
    /// </summary>
    [ContextMenu("ResetClearedStages")]
    public void ClearAllProgress()
    {
        _clearedStages.Clear();
        HighestClearedStage = -1;
        _tutorialCompleted = false;
        PlayerPrefs.DeleteKey(PREF_KEY);
        PlayerPrefs.DeleteKey(PREF_TUTORIAL_KEY);
        PlayerPrefs.Save();
        EventBus.Instance?.Publish(new StageProgressUpdatedEvent { HighestCleared = HighestClearedStage });
    }
}
