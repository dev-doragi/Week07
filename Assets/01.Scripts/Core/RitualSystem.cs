using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어가 사용하는 의식 스킬 3종을 관리합니다.
/// 버튼 OnClick → UseSkill1/2/3 연결.
/// </summary>
public class RitualSystem : MonoBehaviour
{
    [Header("Skill Costs")]
    [SerializeField] private int _skill1Cost = 30;
    [SerializeField] private int _skill2Cost = 50;
    [SerializeField] private int _skill3Cost = 80;

    [Header("Skill Cooldowns (seconds)")]
    [SerializeField] private float _skill1Cooldown = 5f;
    [SerializeField] private float _skill2Cooldown = 5f;
    [SerializeField] private float _skill3Cooldown = 5f;

    [Header("Skill 1 - Wall")]
    [Tooltip("물리적으로 켜고 끌 벽 오브젝트. 아군 투사체는 통과, 적 투사체는 차단.")]
    [SerializeField] private GameObject _wallObject;
    [SerializeField] private float _skill1Duration = 5f;

    private Coroutine _wallRoutine;

    private float _skill1CooldownTimer = 0f;
    private float _skill2CooldownTimer = 0f;
    private float _skill3CooldownTimer = 0f;

    public float Skill1CooldownRemaining => _skill1CooldownTimer;
    public float Skill2CooldownRemaining => _skill2CooldownTimer;
    public float Skill3CooldownRemaining => _skill3CooldownTimer;

    private void Update()
    {
        if (_skill1CooldownTimer > 0f) _skill1CooldownTimer -= Time.deltaTime;
        if (_skill2CooldownTimer > 0f) _skill2CooldownTimer -= Time.deltaTime;
        if (_skill3CooldownTimer > 0f) _skill3CooldownTimer -= Time.deltaTime;
    }

    // ──────────────────────────────────────────────
    // 외부(버튼) 진입점
    // ──────────────────────────────────────────────

    public void UseSkill1()
    {
        if (!IsReady(1, _skill1CooldownTimer)) return;
        if (!TryConsumeResource(_skill1Cost)) return;
        _skill1CooldownTimer = _skill1Cooldown;
        ActivateWall();
    }

    public void UseSkill2()
    {
        if (!IsReady(2, _skill2CooldownTimer)) return;
        if (!TryConsumeResource(_skill2Cost)) return;
        _skill2CooldownTimer = _skill2Cooldown;
        OnSkill2();
    }

    public void UseSkill3()
    {
        if (!IsReady(3, _skill3CooldownTimer)) return;
        if (!TryConsumeResource(_skill3Cost)) return;
        _skill3CooldownTimer = _skill3Cooldown;
        OnSkill3();
    }

    // ──────────────────────────────────────────────
    // 스킬 구현
    // ──────────────────────────────────────────────

    /// <summary>
    /// 스킬 1: 벽 토글.
    /// 벽은 적 투사체 레이어와만 충돌하도록
    /// Physics 2D 충돌 매트릭스에서 설정하세요.
    /// (Wall 레이어 ↔ EnemyProjectile 충돌 ON, AllyProjectile 충돌 OFF)
    /// </summary>
    private void ActivateWall()
    {
        if (_wallObject == null)
        {
            Debug.LogWarning("[RitualSystem] 벽 오브젝트가 연결되지 않았습니다.");
            return;
        }

        if (_wallRoutine != null) StopCoroutine(_wallRoutine);
        _wallRoutine = StartCoroutine(WallRoutine());
    }

    private IEnumerator WallRoutine()
    {
        _wallObject.SetActive(true);
        Debug.Log($"[RitualSystem] 스킬 1 - 벽 활성화 ({_skill1Duration}초)");

        yield return new WaitForSeconds(_skill1Duration);

        _wallObject.SetActive(false);
        _wallRoutine = null;
        Debug.Log("[RitualSystem] 스킬 1 - 벽 비활성화");
    }

    private void OnSkill2()
    {
        Debug.Log("[RitualSystem] 스킬 2 활성화");
    }

    private void OnSkill3()
    {
        Debug.Log("[RitualSystem] 스킬 3 활성화");
    }

    // ──────────────────────────────────────────────
    // 공통 유틸
    // ──────────────────────────────────────────────

    private bool IsReady(int skillIndex, float timer)
    {
        if (timer > 0f)
        {
            Debug.Log($"[RitualSystem] 스킬 {skillIndex} 쿨타임 중 | 남은 시간: {timer:F1}초");
            return false;
        }
        return true;
    }

    private bool TryConsumeResource(int cost)
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError("[RitualSystem] ResourceManager를 찾을 수 없습니다.");
            return false;
        }

        if (!ResourceManager.Instance.SubtractMouseCount(cost))
        {
            Debug.Log($"[RitualSystem] 자원 부족 | 필요: {cost} / 보유: {ResourceManager.Instance.CurrentMouse}");
            return false;
        }

        return true;
    }
}
