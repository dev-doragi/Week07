using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StageMapNodeData
{
    [SerializeField] private string _nodeId;
    [SerializeField] private int _stageIndex;
    [SerializeField] private Vector2 _normalizedPosition;
    [SerializeField] private StageMapReward _reward = new StageMapReward();
    [SerializeField] private List<string> _nextNodeIds = new List<string>();

    public string NodeId => _nodeId;
    public int StageIndex => _stageIndex;
    public Vector2 NormalizedPosition => _normalizedPosition;
    public StageMapReward Reward => _reward;
    public IReadOnlyList<string> NextNodeIds => _nextNodeIds;
}
