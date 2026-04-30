using System.Collections.Generic;
using Newtonsoft.Json.Serialization;
using UnityEngine;

public class IncomeZoneBonusSystem : MonoBehaviour
{
    [Header("Referneces")]
    [SerializeField] private IncomeGridBoard _gridBoard;
    [SerializeField] private IncomeResourceProducer _producer;
    [SerializeField] private GridManager _playerGrid;
    [SerializeField] private IncomeInventory _inventory;

    [Header("좌상단 — 공격속도 (셀당 %)")]
    [SerializeField, Range(0f, 0.5f)] private float _attackSpeedBonusPerCell = 0.05f;

    [Header("우상단 — 최대체력 (셀당 flat)")]
    [SerializeField, Min(0)] private int _maxHpBonusPerCell = 10;

    [Header("좌하단 — 수용량 (셀당)")]
    [SerializeField, Min(0)] private int _capacityBonusPerCell = 1;

    [Header("우하단 — 자원생산력 (셀당)")]
    [SerializeField, Min(0)] private int _productionBonusPerCell = 1;

    // 이전 보너스 추적 (delta 계산으로 중복 방지)
    private float _appliedAttackSpeedBonus = 0f;
    private int   _appliedMaxHpBonus       = 0;
    private int   _appliedCapacityBonus    = 0;
    private int   _appliedProductionBonus  = 0;

    private void OnEnable()
    {
        if(_gridBoard != null)
            _gridBoard.OnBoardChanged += RecalculateBonuses;
        
        EventBus.Instance?.Subscribe<PlayerGridChangedEvent>(OnPlayerGridChanged);
        EventBus.Instance?.Subscribe<InGameStateChangedEvent>(OnInGameStateChanged);
    }

    private void OnDisable()
    {
        if(_gridBoard != null)
            _gridBoard.OnBoardChanged -= RecalculateBonuses;

        EventBus.Instance?.Unsubscribe<PlayerGridChangedEvent>(OnPlayerGridChanged);
        EventBus.Instance?.Unsubscribe<InGameStateChangedEvent>(OnInGameStateChanged);

        ResetAllBonuses();
    }

    private void OnPlayerGridChanged(PlayerGridChangedEvent evt)
    {
        //새 유닛이 배치될 때 공격속도 보너스를 해당 유닛에도 적용
        ApplyAttackSpeedToAllUnits(_appliedAttackSpeedBonus);
    }

    private void OnInGameStateChanged(InGameStateChangedEvent evt)
    {
        bool shouldLock = evt.NewState == InGameState.WavePlaying;
        SetBlocksInteractionLocked(shouldLock);
    }

    private void SetBlocksInteractionLocked(bool locked)
    {
        _inventory?.SetAllPiecesInteractionLocked(locked);
    }

    private void RecalculateBonuses()
    {
        if(_gridBoard == null) return;
        int topLeft     = _gridBoard.GetZoneOccupiedCount(0); // 좌상단 → 공격속도
        int topRight    = _gridBoard.GetZoneOccupiedCount(1); // 우상단 → 최대체력
        int bottomLeft  = _gridBoard.GetZoneOccupiedCount(2); // 좌하단 → 수용량
        int bottomRight = _gridBoard.GetZoneOccupiedCount(3); // 우하단 → 자원생산력

        ApplyAttackSpeedBonus(topLeft * _attackSpeedBonusPerCell);
        ApplyMaxHpBonus(topRight * _maxHpBonusPerCell);
        ApplyCapacityBonus(bottomLeft * _capacityBonusPerCell);
        ApplyProductionBonus(bottomRight * _productionBonusPerCell);

        Debug.Log($"[ZoneBonus] 공격속도+{_appliedAttackSpeedBonus*100:F0}% | 최대HP+{_appliedMaxHpBonus} | 수용량+{_appliedCapacityBonus} | 자원+{_appliedProductionBonus}");
    }

    // 유닛 공격 속도 증가 보너스
    private void ApplyAttackSpeedBonus(float newBonus)
    {
        if(Mathf.Approximately(_appliedAttackSpeedBonus, newBonus)) return;
        _appliedAttackSpeedBonus = newBonus;
        ApplyAttackSpeedToAllUnits(newBonus);
        Debug.Log($"[ZoneBonus][좌상단] 공격속도 보너스 변경 → +{newBonus * 100f:F0}% (셀 {_gridBoard.GetZoneOccupiedCount(0)}칸)");
    }

    private void ApplyAttackSpeedToAllUnits(float bonus)
    {
        if(_playerGrid == null) return;

        List<Unit> units = _playerGrid.GetAllLivingUnits();
        var effect = new PartSupportEffectData(
            SupportTargetRoleType.All,
            SupportStatType.AttackSpeed,
            ModifierType.Percent,
            bonus);

        foreach(Unit unit in units)
        {
            if(unit == null || unit.IsDead) continue;
            unit.StatReceiver.SetModifier(this, effect);
        }
    }

    // 최대 체력 증가 보너스
    private void ApplyMaxHpBonus(int newBonus)
    {
        if(_appliedMaxHpBonus == newBonus) return;
        _appliedMaxHpBonus = newBonus;
        _playerGrid?.SetMaxHpBonus(newBonus);
        Debug.Log($"[ZoneBonus][우상단] 최대체력 보너스 변경 → +{newBonus} HP (셀 {_gridBoard.GetZoneOccupiedCount(1)}칸)");
    }

    // 최대 수용량 증가 보너스
    private void ApplyCapacityBonus(int newBonus)
    {
        if(_appliedCapacityBonus == newBonus) return;
        _appliedCapacityBonus = newBonus;
        _playerGrid?.SetCapacityBonus(newBonus);
        Debug.Log($"[ZoneBonus][좌하단] 수용량 보너스 변경 → +{newBonus} (셀 {_gridBoard.GetZoneOccupiedCount(2)}칸)");
    }

    // 최대 자원 생산량 증가 보너스
    private void ApplyProductionBonus(int newBonus)
    {
        if(_appliedProductionBonus == newBonus) return;
        _appliedProductionBonus = newBonus;
        _producer?.SetProductionBonus(newBonus);
        Debug.Log($"[ZoneBonus][우하단] 자원생산력 보너스 변경 → +{newBonus} (셀 {_gridBoard.GetZoneOccupiedCount(3)}칸)");
    }

    private void ResetAllBonuses()
    {
        ApplyAttackSpeedBonus(0f);
        ApplyMaxHpBonus(0);
        ApplyCapacityBonus(0);
        ApplyProductionBonus(0);
    }

}
