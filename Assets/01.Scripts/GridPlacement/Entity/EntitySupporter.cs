using UnityEngine;
using System.Collections;

/// <summary>
/// 유닛 주변 아군에게 버프 및 지원 효과를 부여하는 모듈입니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - SupportModule의 Radius 내 아군 유닛 감지
/// - 제단(Altar) 유닛인 경우 활성화 여부 체크
/// - 대상의 역할군(공격/방어/전체)에 따른 버프 필터링 및 전달
/// </remarks>
public class EntitySupporter : MonoBehaviour
{
    private Unit _owner;
    private SupportModule _data;
    private AltarConnector _altar; // 제단 연동용 참조
    private float _scanInterval = 0.5f; // 0.5초마다 아군 탐색

    public void Setup(Unit owner, SupportModule data)
    {
        _owner = owner;
        _data = data;

        _altar = GetComponent<AltarConnector>();

        StartCoroutine(SupportRoutine());
    }

    private IEnumerator SupportRoutine()
    {
        while (!_owner.IsDead)
        {
            ScanAndApplyBuffs();
            yield return new WaitForSeconds(_scanInterval);
        }
    }

    private void ScanAndApplyBuffs()
    {
        if (_owner == null || _data == null) return;

        if (_altar != null && !_altar.IsAltarActive) return;

        int allyLayer = (_owner.Team == E_TeamType.Player) ? LayerMask.GetMask("Ally") : LayerMask.GetMask("Enemy");

        // 반경 내 아군 콜라이더 탐색
        Collider2D[] allies = Physics2D.OverlapCircleAll(transform.position, _data.Radius, allyLayer);

        foreach (var col in allies)
        {
            if (col.TryGetComponent(out Unit ally) && !ally.IsDead && ally != _owner)
            {
                foreach (var effect in _data.Effects)
                {
                    if (IsTargetRoleMatch(ally, effect.TargetRoleType))
                    {
                        // 최종 스탯 리시버에 버프 데이터 전달
                        ally.StatReceiver.ApplyModifier(effect);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 대상 유닛의 역할군이 버프 적용 대상인지 판별합니다.
    /// </summary>
    private bool IsTargetRoleMatch(Unit ally, E_SupportTargetRoleType targetCategory)
    {
        switch (targetCategory)
        {
            case E_SupportTargetRoleType.All:
                return true;
            case E_SupportTargetRoleType.Attack:
                return ally.Data.CanAttack;
            case E_SupportTargetRoleType.Defense:
                return ally.Data.CanCollide;
            default:
                return false;
        }
    }
}