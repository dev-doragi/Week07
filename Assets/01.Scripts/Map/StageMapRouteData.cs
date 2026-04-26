using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StageMapRouteData", menuName = "Data/Stage Map/Route Data")]
public class StageMapRouteData : ScriptableObject
{
    [Header("Route")]
    [SerializeField] private string _startNodeId = "start";
    [SerializeField] private string _finalNodeId = "final";
    [SerializeField] private List<StageMapNodeData> _nodes = new List<StageMapNodeData>();

    [Header("Stage Flow")]
    [Min(0f)]
    [SerializeField] private float _waveStartDelay = 15f;
    [Min(0)]
    [SerializeField] private int _ritualPointsPerClear = 1;

    [Header("Unlock Rewards")]
    [SerializeField] private List<UnitDataSO> _unlockableRatUnits = new List<UnitDataSO>();
    [SerializeField] private List<StageSkillUnlockData> _skillUnlocks = new List<StageSkillUnlockData>();

    public string StartNodeId => _startNodeId;
    public string FinalNodeId => _finalNodeId;
    public IReadOnlyList<StageMapNodeData> Nodes => _nodes;
    public float WaveStartDelay => _waveStartDelay;
    public int RitualPointsPerClear => _ritualPointsPerClear;
    public IReadOnlyList<UnitDataSO> UnlockableRatUnits => _unlockableRatUnits;
    public IReadOnlyList<StageSkillUnlockData> SkillUnlocks => _skillUnlocks;

    public StageMapNodeData GetNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;
        for (int i = 0; i < _nodes.Count; i++)
        {
            StageMapNodeData node = _nodes[i];
            if (node != null && node.NodeId == nodeId)
                return node;
        }

        return null;
    }

    public bool CanMove(string fromNodeId, string toNodeId)
    {
        StageMapNodeData from = GetNode(fromNodeId);
        if (from == null || string.IsNullOrEmpty(toNodeId)) return false;

        IReadOnlyList<string> nextIds = from.NextNodeIds;
        for (int i = 0; i < nextIds.Count; i++)
        {
            if (nextIds[i] == toNodeId)
                return true;
        }

        return false;
    }
}
