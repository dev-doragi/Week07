using System;
using UnityEngine;

[Serializable]
public class StageSkillUnlockData
{
    [Min(0)]
    [SerializeField] private int _clearStageIndex;
    [Min(1)]
    [SerializeField] private int _skillIndex = 1;

    public int ClearStageIndex => _clearStageIndex;
    public int SkillIndex => _skillIndex;
}
