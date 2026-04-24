using UnityEngine;

[CreateAssetMenu(menuName = "Doctrine/Doctrine Node Data", fileName = "DoctrineNodeData")]
public class DoctrineNodeData : ScriptableObject
{
    [Header("Identity")]
    public string nodeId;
    public string nodeName;

    [Header("Info")]
    [TextArea] public string description;
    public DoctrineType doctrineType;

    [Header("Grid Position")]
    [Min(0)] public int rowIndex;
    [Range(0, 2)] public int columnIndex;

    [Header("Effect")]
    public string effectId;
    [TextArea] public string effectSummary;
}
