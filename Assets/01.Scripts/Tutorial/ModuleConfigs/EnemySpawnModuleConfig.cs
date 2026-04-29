using UnityEngine;

/// <summary>
/// 적 소환 모듈 설정
/// </summary>
[System.Serializable]
public class EnemySpawnModuleConfig
{
    [Header("Enemy Spawn")]
    public int EnemySpawnCycles = 1;

    [SerializeField]
    public GameObject EnemySiegePrefab;

    [SerializeField]
    [Min(1f)]
    public float NextWaveInterval = 1f;
}
