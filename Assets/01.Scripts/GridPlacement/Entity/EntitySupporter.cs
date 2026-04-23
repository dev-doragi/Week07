using UnityEngine;
using System.Collections;

/// <summary>
/// 유닛 주변 아군에게 버프 및 지원 효과를 부여하는 모듈입니다.
/// </summary>
/// <remarks>
/// [주요 역할]
/// - SupportModule의 Radius 내 아군 유닛 감지
/// - PartSupportEffectData 리스트에 따른 능력치 가감치 전달
/// </remarks>

public class EntitySupporter : MonoBehaviour
{
    private Unit _owner;
    private SupportModule _data;
    private float _scanInterval = 0.5f; // 0.5초마다 아군 탐색

    public void Setup(Unit owner, SupportModule data)
    {
        _owner = owner;
        _data = data;
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
        // 내 팀에 따른 아군 레이어 마스크 결정
        int allyLayer = (_owner.Team == E_TeamType.Player) ? LayerMask.GetMask("Ally") : LayerMask.GetMask("Enemy");

        Collider2D[] allies = Physics2D.OverlapCircleAll(transform.position, _data.Radius, allyLayer);

        foreach (var col in allies)
        {
            Unit ally = col.GetComponent<Unit>();
            if (ally != null && !ally.IsDead)
            {
                // TODO: ally에게 _data.Effects(버프 리스트)를 전달하여 능력치 수정
                // 예: ally.StatReceiver.ApplyBuff(_data.Effects);
            }
        }
    }
}