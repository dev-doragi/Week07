using UnityEngine;

[System.Serializable]
public class UIStateModuleConfig
{
    [Header("UI State Override")]
    [Tooltip("체크 시 이 스텝에 진입할 때 아래 UI 상태를 강제로 적용합니다.")]
    public bool OverrideUIState = false;

    [Header("UI Visibility")]
    public bool ShowInGamePanel = true;
    public bool ShowBlockPanel = false;
    public bool ShowShopButtonGroup = true;
    public bool ShowProductionGrid = true;
    public bool ShowWaveStartButton = true;
    public bool ShowChargeButton = true;
}