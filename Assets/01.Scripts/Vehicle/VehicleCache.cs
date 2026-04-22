using System.Collections.Generic;
using UnityEngine;

// 파츠 1개의 정보를 기억할 구조체
public struct PlacedPartSaveData
{
    public int PartKey;           // PartData의 고유 Key (Dictionary 검색용)
    public Vector2Int Origin;     // 기준 좌표
    public int Rotation;          // 회전값
}

// RatStatRuntime의 HP 정보를 기억할 구조체
public struct RatStatSaveData
{
    public Vector2Int Origin;     // PlacedPartSaveData.Origin과 대응
    public float CurrentHp;       // 저장 시점의 현재 HP
}

// 씬 전환/스테이지 스왑 간 데이터를 쥐고 있을 정적 컨테이너
public static class VehicleCache
{
    public static bool HasSavedData = false;
    public static List<PlacedPartSaveData> SavedParts = new List<PlacedPartSaveData>();

    // Origin → 현재 HP 매핑
    public static Dictionary<Vector2Int, RatStatSaveData> SavedRatStats = new Dictionary<Vector2Int, RatStatSaveData>();

    public static void Clear()
    {
        SavedParts.Clear();
        SavedRatStats.Clear();
        HasSavedData = false;
    }

    /// <summary>
    /// Origin에 해당하는 RatStatSaveData가 있으면 반환합니다.
    /// </summary>
    public static bool TryGetRatStat(Vector2Int origin, out RatStatSaveData statData)
    {
        return SavedRatStats.TryGetValue(origin, out statData);
    }
}