using UnityEngine;

/// <summary>
/// 적 격파 모듈 설정
/// </summary>
[System.Serializable]
public class EnemyDefeatedModuleConfig
{
    [Header("Enemy Defeat Condition")]
    public int RequiredEnemyCount = 1;

    [SerializeField]
    public TutorialEnemyDefeatTarget Target = TutorialEnemyDefeatTarget.Any;

    [Header("Display")]
    [SerializeField]
    [Tooltip("적 처치 진행도 표시 라벨 (비워두면 기본값 사용)")]
    public string DefeatLabel = "";
}
