using UnityEngine;

/// <summary>
/// 적 소환 모듈 설정
/// </summary>
[System.Serializable]
public class EnemySpawnModuleConfig
{
    [Header("Tutorial Stage")]
    [Tooltip("튜토리얼에서 사용할 스테이지 데이터. StageManager의 기존 로직으로 로드됩니다.")]
    public StageDataSO TutorialStageData;

    [SerializeField]
    [Min(0)]
    [Tooltip("튜토리얼 스테이지 내에서 시작할 웨이브 인덱스")]
    public int TutorialWaveIndex = 0;

    [Header("Spawn Option")]
    [Tooltip("true: 자동 스폰, false: 버튼 등으로 수동 스폰")]
    public bool AutoSpawn = true;
}
