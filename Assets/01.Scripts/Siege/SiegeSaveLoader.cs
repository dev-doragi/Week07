using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 클리어 시 플레이어 그리드를 저장하고,
/// 씬 재로드 후 StageGenerateCompleteEvent를 받아 복원합니다.
/// </summary>
/// <remarks>
/// [저장 시점]
/// - StageClearedEvent: 스테이지 클리어 시 (GameFlowManager에서 발행)
///   → 모든 웨이브 완료 후 현재 배치된 유닛 상태를 저장
///
/// [복원 시점]
/// - StageGenerateCompleteEvent 수신 시 자동 복원
/// - 게임 오버 후 Retry: 동일 스테이지 재로드 → 저장된 배치 복원
/// - 다음 스테이지: 새 스테이지 로드 → 이전 배치 복원 후 이어서 도전
///
/// [캐시 정리]
/// - 로비 이동 또는 스테이지 선택으로 복귀할 때 명시적으로 Clear() 호출
/// </remarks>
[DefaultExecutionOrder(-120)]
public class SiegeSaveLoader : Singleton<SiegeSaveLoader>
{
    protected override void OnBootstrap()
    {
        Debug.Log("[SiegeSaveLoader] OnBootstrap - 이벤트 구독 시작");

        if (EventBus.Instance == null)
        {
            Debug.LogError("[SiegeSaveLoader] EventBus.Instance가 null입니다!");
            return;
        }

        EventBus.Instance.Subscribe<StageClearedEvent>(OnStageCleared);
        EventBus.Instance.Subscribe<StageGenerateCompleteEvent>(OnStageGenerateComplete);

        Debug.Log("[SiegeSaveLoader] 이벤트 구독 완료");
    }

    private void OnDisable()
    {
        Debug.Log("[SiegeSaveLoader] OnDisable - 이벤트 구독 해제");

        if (EventBus.Instance == null) return;

        EventBus.Instance.Unsubscribe<StageClearedEvent>(OnStageCleared);
        EventBus.Instance.Unsubscribe<StageGenerateCompleteEvent>(OnStageGenerateComplete);
    }

    // -------------------------------------------------------
    // 스테이지 클리어 → 현재 그리드 저장
    // -------------------------------------------------------
    private void OnStageCleared(StageClearedEvent evt)
    {
        Debug.Log($"[SiegeSaveLoader] OnStageCleared 호출 - Stage {evt.StageIndex}");

        if (GridManager.Instance == null)
        {
            Debug.LogWarning("[SiegeSaveLoader] GridManager가 없어 저장을 건너뜁니다.");
            return;
        }

        // Wheel·Core 제외한 배치된 유닛만 스냅샷
        var snapshot = GridManager.Instance.GetPlacedUnitsSnapshot(includeInitialUnits: false);
        Debug.Log($"[SiegeSaveLoader] 스냅샷 개수: {snapshot.Count}");

        SiegeCache.Save(snapshot);
        Debug.Log($"[SiegeSaveLoader] 저장 완료 - HasSavedData: {SiegeCache.HasSavedData}, Count: {SiegeCache.SavedUnits.Count}");
    }

    // -------------------------------------------------------
    // 새 스테이지 생성 완료 → 캐시가 있으면 복원
    // -------------------------------------------------------
    private void OnStageGenerateComplete(StageGenerateCompleteEvent evt)
    {
        Debug.Log($"[SiegeSaveLoader] OnStageGenerateComplete 호출 - HasSavedData: {SiegeCache.HasSavedData}");

        if (!SiegeCache.HasSavedData)
        {
            Debug.Log("[SiegeSaveLoader] 저장된 데이터가 없어 복원을 건너뜁니다.");
            return;
        }

        if (GridManager.Instance == null)
        {
            Debug.LogWarning("[SiegeSaveLoader] GridManager가 없어 복원을 건너뜁니다.");
            return;
        }

        List<SiegeCache.Entry> entries = new List<SiegeCache.Entry>(SiegeCache.SavedUnits);
        Debug.Log($"[SiegeSaveLoader] 복원할 엔트리 개수: {entries.Count}");

        int restoredCount = 0;
        foreach (var entry in entries)
        {
            if (entry.Data == null)
            {
                Debug.LogWarning("[SiegeSaveLoader] 엔트리의 Data가 null입니다.");
                continue;
            }

            // PlaceInitial: 비용/규칙 검증 없이 강제 배치
            GridManager.Instance.PlaceInitial(entry.Data, entry.Origin);
            restoredCount++;
        }

        Debug.Log($"[SiegeSaveLoader] {restoredCount}개 유닛 복원 완료");
    }
}