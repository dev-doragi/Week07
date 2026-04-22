using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct WaveData
{
    [Tooltip("이 웨이브에 등장할 적 공성병기")]
    public GameObject EnemySiegePrefab;
    public float WaveInterval; // 각 웨이브의 스톤 인터벌
}

[CreateAssetMenu(fileName = "StageData_", menuName = "08.Data/Stage/StageData")]
public class StageDataSO : ScriptableObject
{
    [Header("Stage Layout (위치 껍데기)")]
    public StageLayout StageLayoutPrefab;

    [Header("Wave Info (웨이브 목록)")]
    [Tooltip("이 스테이지에서 진행될 웨이브 목록 (0번 인덱스 = 1웨이브)")]
    public List<WaveData> Waves = new List<WaveData>();

    [Header("Stage Info")]
    public int StageIndex;
}