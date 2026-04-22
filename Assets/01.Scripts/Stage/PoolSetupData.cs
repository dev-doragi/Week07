using UnityEngine;

/// <summary>
/// PoolManager의 인스펙터 창에서 풀링할 오브젝트들을 
/// 보기 좋고 편하게 세팅하기 위해 만든 데이터 묶음(구조체)입니다.
/// </summary>

[System.Serializable]
public struct PoolSetupData
{
    public GameObject Prefab;
    [Tooltip("초기 생성 개수 (Prewarm)")]
    public int InitialSize;
    [Tooltip("최대 생성 허용 개수")]
    public int MaxSize;
}