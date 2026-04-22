using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class VehicleSaveLoader : MonoBehaviour
{
    [Header("References")]
    // 최소한의 필드만 남겨두어 에러를 방지합니다.
    [SerializeField] private Transform _placedPartsRoot;

    private void OnEnable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<StageLoadedEvent>(OnStageLoaded);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<StageLoadedEvent>(OnStageLoaded);
        }
    }

    /// <summary>
    /// [EventBus] 스테이지 로드 시 차량 복구 (더미)
    /// </summary>
    private void OnStageLoaded(StageLoadedEvent evt)
    {
        // 현재는 비히클 저장/복구 로직이 미정이므로 로드 동작을 더미로 처리합니다.
        LoadSavedVehicle(evt.StageIndex);
    }

    public void SaveCurrentVehicle(int stageIndex)
    {
        // 더미 저장: 실제 구현 전까지는 캐시를 초기화하고 HasSavedData=false로 유지합니다.
        Debug.Log($"[Vehicle] (DUMMY) SaveCurrentVehicle called for Stage {stageIndex} - saving disabled.");
        VehicleCache.Clear();
    }

    public void LoadSavedVehicle(int stageIndex)
    {
        // 더미 로드: 실제 복구 로직은 추후 구현합니다.
        if (!VehicleCache.HasSavedData || VehicleCache.SavedParts.Count == 0)
        {
            Debug.Log("[Vehicle] (DUMMY) No saved data to restore.");
            return;
        }

        Debug.Log($"[Vehicle] (DUMMY) LoadSavedVehicle called for Stage {stageIndex} - restore disabled.");
    }

    private void ClearCurrentField()
    {
        if (_placedPartsRoot == null) return;

        int childCount = _placedPartsRoot.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            Transform child = _placedPartsRoot.GetChild(i);
            Destroy(child.gameObject);
        }

        if (childCount > 0)
            Debug.Log($"CleanUp: 기존 객체 {childCount}개 제거");
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            StageManager.Instance.LoadNextStage();
        }

        // 테스트용 단축키 (8:저장, 9:로드)
        if (Keyboard.current.digit8Key.wasPressedThisFrame)
        {
            Debug.Log("[Test] Manual Save");
            SaveCurrentVehicle(0);
        }

        if (Keyboard.current.digit9Key.wasPressedThisFrame)
        {
            Debug.Log("[Test] Manual Load");
            LoadSavedVehicle(0);
        }
        if (Keyboard.current.digit0Key.wasPressedThisFrame)
        {
            Debug.Log("<color=yellow>[Test] 스테이지 전환 시뮬레이션 시작</color>");

            if (StageManager.Instance != null)
            {
                int currentIndex = StageManager.Instance.CurrentStageIndex;
                StageManager.Instance.ClearCurrentStage();
                StageManager.Instance.LoadStage(currentIndex);

                Debug.Log("<color=cyan>[Test] 스테이지 전환 및 병기 복구 완료</color>");
            }
            else
            {
                Debug.LogError("StageManager 인스턴스를 찾을 수 없습니다.");
            }
        }
#endif
    }
}