using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class DoctrineManager : MonoBehaviour
{
    [Header("Progress")]
    [SerializeField] private int doctrinePoint = 0;
    [SerializeField] private int currentRowIndex = 0;

    [Header("References")]
    [SerializeField] private List<DoctrineNodeUI> allNodes = new List<DoctrineNodeUI>();
    [SerializeField] private DoctrineTooltipUI tooltipUI;
    [SerializeField] private DoctrineEffectApplier effectApplier;
    [SerializeField] private Button confirmButton;
    [SerializeField] private GameObject doctrinePanelToHide;

    [Header("Options")]
    [SerializeField] private bool autoCollectNodesFromChildren = true;

    [Header("Debug / Save Ready")]
    [SerializeField] private List<string> confirmedNodeIds = new List<string>();

    private readonly Dictionary<int, List<DoctrineNodeUI>> _nodesByRow = new Dictionary<int, List<DoctrineNodeUI>>();
    private DoctrineNodeUI _pendingNode;
    private int _maxRowIndex = -1;

    public int DoctrinePoint => doctrinePoint;
    public int CurrentRowIndex => currentRowIndex;
    public IReadOnlyList<string> ConfirmedNodeIds => confirmedNodeIds;

    private void Awake()
    {
        if (doctrinePanelToHide == null)
        {
            doctrinePanelToHide = gameObject;
        }

        InitializeTree();
    }

    private void OnEnable()
    {
        RefreshConfirmButtonState();
    }

    public void InitializeTree()
    {
        _nodesByRow.Clear();
        _maxRowIndex = -1;
        _pendingNode = null;
        if (tooltipUI != null)
        {
            tooltipUI.UnpinAndHide();
        }

        if (autoCollectNodesFromChildren)
        {
            allNodes = new List<DoctrineNodeUI>(GetComponentsInChildren<DoctrineNodeUI>(true));
        }

        for (int i = 0; i < allNodes.Count; i++)
        {
            RegisterNode(allNodes[i]);
        }

        BuildInitialNodeStates();
        RefreshConfirmButtonState();

        Debug.Log($"[DoctrineManager] Initialized | Points: {doctrinePoint}, CurrentRow: {currentRowIndex}, NodeCount: {allNodes.Count}");
    }

    public void RegisterNode(DoctrineNodeUI node)
    {
        if (node == null)
        {
            return;
        }

        DoctrineNodeData data = node.GetData();
        if (data == null)
        {
            Debug.LogWarning($"[DoctrineManager] Node data is missing on {node.name}");
            return;
        }

        node.Initialize(this, tooltipUI);

        if (!_nodesByRow.TryGetValue(data.rowIndex, out List<DoctrineNodeUI> rowNodes))
        {
            rowNodes = new List<DoctrineNodeUI>();
            _nodesByRow.Add(data.rowIndex, rowNodes);
        }

        if (!rowNodes.Contains(node))
        {
            rowNodes.Add(node);
        }

        if (data.rowIndex > _maxRowIndex)
        {
            _maxRowIndex = data.rowIndex;
        }
    }

    public void TrySelectNode(DoctrineNodeUI node)
    {
        if (node == null)
        {
            return;
        }

        DoctrineNodeData data = node.GetData();
        if (data == null)
        {
            Debug.LogWarning("[DoctrineManager] TrySelectNode failed: node data is null");
            return;
        }

        if (doctrinePoint < 1)
        {
            Debug.Log("[DoctrineManager] Cannot select node: doctrine point is 0");
            return;
        }

        if (data.rowIndex != currentRowIndex)
        {
            Debug.Log($"[DoctrineManager] Cannot select node: row mismatch (current: {currentRowIndex}, clicked: {data.rowIndex})");
            return;
        }

        DoctrineNodeState state = node.GetState();
        if (state != DoctrineNodeState.Available && state != DoctrineNodeState.Pending)
        {
            Debug.Log($"[DoctrineManager] Cannot select node: invalid state {state}");
            return;
        }

        if (_pendingNode == node && state == DoctrineNodeState.Pending)
        {
            _pendingNode.SetState(DoctrineNodeState.Available);
            _pendingNode = null;

            if (tooltipUI != null)
            {
                tooltipUI.UnpinAndHide();
            }

            Debug.Log($"[DoctrineManager] Pending canceled | Row: {data.rowIndex}, Col: {data.columnIndex}, NodeId: {data.nodeId}");
            RefreshConfirmButtonState();
            return;
        }

        if (_pendingNode != null && _pendingNode != node)
        {
            _pendingNode.SetState(DoctrineNodeState.Available);
        }

        _pendingNode = node;
        _pendingNode.SetState(DoctrineNodeState.Pending);
        if (tooltipUI != null)
        {
            tooltipUI.Pin(data, DoctrineNodeState.Pending);
        }

        Debug.Log($"[DoctrineManager] Pending selected | Row: {data.rowIndex}, Col: {data.columnIndex}, NodeId: {data.nodeId}");
        RefreshConfirmButtonState();
    }

    public void ConfirmPendingNode()
    {
        if (_pendingNode == null)
        {
            Debug.Log("[DoctrineManager] Confirm failed: no pending node");
            return;
        }

        if (doctrinePoint < 1)
        {
            Debug.Log("[DoctrineManager] Confirm failed: doctrine point is 0");
            return;
        }

        DoctrineNodeData data = _pendingNode.GetData();
        if (data == null)
        {
            Debug.LogWarning("[DoctrineManager] Confirm failed: pending node data is null");
            return;
        }

        if (data.rowIndex != currentRowIndex)
        {
            Debug.LogWarning($"[DoctrineManager] Confirm failed: pending row mismatch (current: {currentRowIndex}, pending: {data.rowIndex})");
            return;
        }

        _pendingNode.SetState(DoctrineNodeState.Confirmed);
        RecordConfirmedNode(data.nodeId);

        if (_nodesByRow.TryGetValue(currentRowIndex, out List<DoctrineNodeUI> rowNodes))
        {
            for (int i = 0; i < rowNodes.Count; i++)
            {
                DoctrineNodeUI rowNode = rowNodes[i];
                if (rowNode == null || rowNode == _pendingNode)
                {
                    continue;
                }

                if (rowNode.GetState() != DoctrineNodeState.Confirmed)
                {
                    rowNode.SetState(DoctrineNodeState.Disabled);
                }
            }
        }

        doctrinePoint -= 1;
        Debug.Log($"[DoctrineManager] Confirmed | NodeId: {data.nodeId}, RemainingPoint: {doctrinePoint}");
        GameLogger.Instance?.RecordDoctrineSelected(data);
        GameCsvLogger.Instance.LogEvent(GameLogEventType.DoctrineSelected, actor: gameObject, metadata: new Dictionary<string, object>
        {
            { "rowIndex", data.rowIndex },
            { "rowStep", data.rowIndex + 1 },
            { "columnIndex", data.columnIndex },
            { "nodeId", data.nodeId },
            { "nodeName", data.nodeName },
            { "doctrineType", data.doctrineType.ToString() },
            { "effectId", data.effectId },
            { "path", BuildDoctrinePathForCsv(data) }
        });

        ApplyDoctrineEffect(data);
        if (tooltipUI != null)
        {
            tooltipUI.UnpinAndHide();
        }

        _pendingNode = null;
        currentRowIndex += 1;

        UnlockCurrentRow();
        RefreshConfirmButtonState();

        if (doctrinePanelToHide != null)
        {
            doctrinePanelToHide.SetActive(false);
        }
    }

    public void AddDoctrinePoint(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        doctrinePoint = Mathf.Max(0, doctrinePoint + amount);
        Debug.Log($"[DoctrineManager] DoctrinePoint changed: {amount:+#;-#;0}, Current: {doctrinePoint}");

        RefreshConfirmButtonState();
    }

    private void BuildInitialNodeStates()
    {
        if (currentRowIndex < 0)
        {
            currentRowIndex = 0;
        }

        if (_maxRowIndex >= 0)
        {
            currentRowIndex = Mathf.Clamp(currentRowIndex, 0, _maxRowIndex + 1);
        }

        for (int i = 0; i < allNodes.Count; i++)
        {
            DoctrineNodeUI node = allNodes[i];
            DoctrineNodeData nodeData = node != null ? node.GetData() : null;
            if (nodeData == null)
            {
                continue;
            }

            if (IsConfirmedNode(nodeData.nodeId))
            {
                node.SetState(DoctrineNodeState.Confirmed);
            }
            else if (nodeData.rowIndex < currentRowIndex)
            {
                node.SetState(DoctrineNodeState.Disabled);
            }
            else
            {
                node.SetState(DoctrineNodeState.Locked);
            }
        }

        UnlockCurrentRow();
    }

    private bool IsConfirmedNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        return confirmedNodeIds.Contains(nodeId);
    }

    private void UnlockCurrentRow()
    {
        if (!_nodesByRow.TryGetValue(currentRowIndex, out List<DoctrineNodeUI> rowNodes))
        {
            Debug.Log($"[DoctrineManager] No more row to unlock. CurrentRow: {currentRowIndex}");
            return;
        }

        for (int i = 0; i < rowNodes.Count; i++)
        {
            DoctrineNodeUI rowNode = rowNodes[i];
            if (rowNode == null)
            {
                continue;
            }

            DoctrineNodeState state = rowNode.GetState();
            if (state == DoctrineNodeState.Locked || state == DoctrineNodeState.Available)
            {
                rowNode.SetState(DoctrineNodeState.Available);
            }
        }

        Debug.Log($"[DoctrineManager] Row unlocked: {currentRowIndex}");
    }

    private void ApplyDoctrineEffect(DoctrineNodeData data)
    {
        if (data == null)
        {
            return;
        }

        if (effectApplier != null)
        {
            effectApplier.ApplyEffect(data.effectId);
            return;
        }

        Debug.Log($"[DoctrineManager] EffectApplier missing. Requested effectId: {data.effectId}");
    }

    private void RecordConfirmedNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        if (!confirmedNodeIds.Contains(nodeId))
        {
            confirmedNodeIds.Add(nodeId);
        }
    }

    private string BuildDoctrinePathForCsv(DoctrineNodeData latestSelection)
    {
        if (allNodes == null || allNodes.Count == 0)
        {
            return "none";
        }

        Dictionary<int, string> rowTypeMap = new Dictionary<int, string>();

        for (int i = 0; i < allNodes.Count; i++)
        {
            DoctrineNodeUI node = allNodes[i];
            DoctrineNodeData data = node != null ? node.GetData() : null;
            if (data == null || string.IsNullOrWhiteSpace(data.nodeId))
            {
                continue;
            }

            if (confirmedNodeIds.Contains(data.nodeId))
            {
                rowTypeMap[data.rowIndex] = data.doctrineType.ToString();
            }
        }

        if (latestSelection != null)
        {
            rowTypeMap[latestSelection.rowIndex] = latestSelection.doctrineType.ToString();
        }

        if (rowTypeMap.Count == 0)
        {
            return "none";
        }

        List<int> rows = new List<int>(rowTypeMap.Keys);
        rows.Sort();

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(" - ");
            }

            int rowIndex = rows[i];
            builder.Append(rowIndex + 1).Append(":").Append(rowTypeMap[rowIndex]);
        }

        return builder.ToString();
    }
    private void RefreshConfirmButtonState()
    {
        if (confirmButton == null)
        {
            return;
        }

        confirmButton.interactable = _pendingNode != null && doctrinePoint > 0;
    }
}

