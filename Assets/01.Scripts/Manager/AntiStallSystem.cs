using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AntiStallSystem : MonoBehaviour
{
    [Header("Spawn Unit")]
    [SerializeField] private UnitDataSO _stallUnitData;      //스폰할 공격 적 SO
    [Header("Timing")]
    [SerializeField] private float _firstSpawnDelay = 60f;   // 첫 스폰까지 대기시간
    [SerializeField] private float _spawnInterval   = 20f;  // 이후 스폰 주기
    [SerializeField] private int   _spawnPerCycle   = 2;    // 주기당 스폰 수
    [Header("Position")]
    [SerializeField] private float _xOffsetFirst    = 3f;   // 코어 기준 첫 위치
    [SerializeField] private float _xOffsetNext     = 2f;   // 이후 슬롯 간격
    [SerializeField] private float _spawnY          = 0f;   // 스폰 Y 위치

    private class SpawnSlot
    {
        public Vector3 Position;
        public Unit Unit;
        public bool IsEmpty => Unit == null || Unit.IsDead;
    }

    private List<SpawnSlot> _slots = new();
    private float _nextSlotX;
    private Coroutine _routine;

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance.Subscribe<WaveEndedEvent>(OnWaveEnded);
        EventBus.Instance.Subscribe<CoreDestroyedEvent>(OnCoreDestroyed);
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance.Unsubscribe<WaveEndedEvent>(OnWaveEnded);
        EventBus.Instance.Unsubscribe<CoreDestroyedEvent>(OnCoreDestroyed);
        StopRoutine();
    }

    private void OnWaveStarted(WaveStartedEvent evt)
    {
        StopRoutine();
        _slots.Clear();
        _routine = StartCoroutine(StallRoutine());
    }

    private void OnWaveEnded(WaveEndedEvent evt) => StopRoutine();

    private void OnCoreDestroyed(CoreDestroyedEvent evt)
    {
        // 어느 코어든 파괴되면 전투 종료 -> 루틴 중단
        StopRoutine();
    }

    private void StopRoutine()
    {
        if(_routine == null) return;
        StopCoroutine(_routine);
        _routine = null;
    }


    IEnumerator StallRoutine()
    {
        //적 코어 위치 확정
        Unit enemyCore = FindEnemyCore();
        if(enemyCore == null)
        {
            Debug.LogWarning("[AntiStall] 적 코어를 찾을 수 없어 스톨링 방지 루틴을 시작하지 않습니다.");
            yield break;
        }
        
        _nextSlotX = enemyCore.transform.position.x + _xOffsetFirst;

        //첫 스폰(60초 후 , 1마리)
        yield return new WaitForSeconds(_firstSpawnDelay);
        SpawnUnits(1);

        // 이후 20초마다 2마리씩
        while(true)
        {
            yield return new WaitForSeconds(_spawnInterval);
            SpawnUnits(_spawnPerCycle);
        }
    }

    //스폰 로직
    private void SpawnUnits(int count)
    {
        int remaining = count;

        //1 단계: 기존 슬롯 중 중 빈자리 먼저 채우기 
        foreach(var slot in _slots)
        {
            if(remaining <= 0) break;
            if(!slot.IsEmpty) continue;

            slot.Unit = SpawnAt(slot.Position);
            remaining--;
        }

        //2단계: 남은 수 만큼 신규 슬롯 추가
        while(remaining > 0)
        {
            var pos = new Vector3(_nextSlotX, _spawnY, 0f);
            var unit = SpawnAt(pos);
            _slots.Add(new SpawnSlot {Position = pos, Unit = unit});
            _nextSlotX += _xOffsetNext;
            remaining--;
        }
    }

    private Unit SpawnAt(Vector3 position)
    {
        if(_stallUnitData == null || _stallUnitData.Prefab == null)
        {
            Debug.LogError("[AntiStall] UnitDataSO 또는 Prefab이 설정되지 않았습니다.");
            return null;
        }

        var go = Instantiate(_stallUnitData.Prefab, position, Quaternion.identity);
        var unit = go.GetComponent<Unit>();

        if(unit == null)
        {
            Debug.LogError("[AntiStall] 스폰한 프리팹에 Unit 컴포넌트가 없습니다.");
            Destroy(go);
            return null;
        }

        Debug.Log("60초 지남 스톨링 하지마셈 ㅇㅅㅇ");
        unit.InitializeRuntime();
        return unit;
    }

    //유틸
    private Unit FindEnemyCore()
    {
        var allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        foreach(var u in allUnits)
        {
            if(u.Team == TeamType.Enemy && u.Category == UnitCategory.Core)
                return u;
            
        }
        return null;
    }

}

