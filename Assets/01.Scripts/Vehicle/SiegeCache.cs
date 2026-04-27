using System.Collections.Generic;
using UnityEngine;

// 씬 전환 간 유닛 배치 정보를 보관하는 정적 컨테이너
public static class SiegeCache
{
    // UnitDataSO 레퍼런스와 Origin을 함께 저장 (Key 검색 불필요)
    public struct Entry
    {
        public UnitDataSO Data;
        public Vector2Int  Origin;
    }

    public static bool        HasSavedData { get; private set; }
    public static List<Entry> SavedUnits   { get; } = new List<Entry>();

    // -------------------------------------------------------
    // 저장
    // -------------------------------------------------------
    public static void Save(List<PlacedUnit> snapshot)
    {
        SavedUnits.Clear();

        foreach (var placed in snapshot)
        {
            if (placed?.Data == null) continue;

            SavedUnits.Add(new Entry
            {
                Data   = placed.Data,
                Origin = placed.OriginCell,
            });
        }

        HasSavedData = SavedUnits.Count > 0;
        Debug.Log($"[SiegeCache] {SavedUnits.Count}개 유닛 저장 완료");
    }

    // -------------------------------------------------------
    // 복원 후 캐시 비우기
    // -------------------------------------------------------
    public static List<Entry> ConsumeAndClear()
    {
        var copy = new List<Entry>(SavedUnits);
        Clear();
        return copy;
    }

    public static void Clear()
    {
        SavedUnits.Clear();
        HasSavedData = false;
    }
}