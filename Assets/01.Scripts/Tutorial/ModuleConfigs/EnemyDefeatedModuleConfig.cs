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
}
