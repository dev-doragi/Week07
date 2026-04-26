using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-80)]
public class StageMapController : MonoBehaviour
{
    private static StageMapController _activeController;

    [Header("Data")]
    [SerializeField] private StageMapRouteData _routeData;
    [SerializeField] private StageMapRewardApplier _rewardApplier;

    [Header("Runtime UI")]
    [SerializeField] private Canvas _targetCanvas;
    [SerializeField] private RectTransform _mapRoot;
    [SerializeField] private Vector2 _mapSize = new Vector2(1280f, 600f);

    [Header("Random Route")]
    [SerializeField] private bool _randomizeOnStart = true;
    [SerializeField] private Vector2 _randomYRange = new Vector2(0.22f, 0.78f);
    [SerializeField] private int _minConnectionsPerNode = 1;
    [SerializeField] private int _maxConnectionsPerNode = 2;

    [Header("Visuals")]
    [SerializeField] private Sprite _productionRewardIcon;
    [SerializeField] private Sprite _ratTowerRewardIcon;
    [SerializeField] private Color _backgroundColor = new Color32(0x20, 0x20, 0x20, 0xff);
    [SerializeField] private Color _lineColor = new Color(0.63f, 0.63f, 0.63f, 1f);
    [SerializeField] private Color _lockedNodeColor = new Color(0.34f, 0.34f, 0.34f, 1f);
    [SerializeField] private Color _availableNodeColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    [SerializeField] private Color _selectedNodeColor = new Color(0.3f, 0.85f, 0.4f, 1f);
    [SerializeField] private Color _finalNodeColor = new Color(1f, 0.28f, 0.33f, 1f);

    private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
    private readonly Dictionary<string, RuntimeNode> _runtimeNodes = new Dictionary<string, RuntimeNode>();
    private readonly List<RuntimeNode> _runtimeNodeList = new List<RuntimeNode>();
    private string _currentNodeId;
    private string _selectedNodeId;
    private bool _isInitialized;
    private bool _isMapVisible;
    private bool _hasPausedTimeScale;
    private float _timeScaleBeforeMap = 1f;

    private class RuntimeNode
    {
        public string NodeId;
        public int StageIndex;
        public Vector2 Position;
        public StageMapReward Reward;
        public readonly List<string> NextNodeIds = new List<string>();
    }

    private void Awake()
    {
        if (_rewardApplier == null)
            _rewardApplier = GetComponent<StageMapRewardApplier>();
    }

    private void OnEnable()
    {
        _activeController = this;

        if (EventBus.Instance != null)
            EventBus.Instance.Subscribe<StageClearedEvent>(OnStageCleared);
    }

    private void OnDisable()
    {
        SetMapVisible(false);

        if (_activeController == this)
            _activeController = null;

        if (EventBus.Instance != null)
            EventBus.Instance.Unsubscribe<StageClearedEvent>(OnStageCleared);
    }

    public static bool ShouldSuppressStageClearScreen()
    {
        return _activeController != null && _activeController.HasNextMapStepAfterCurrentClear();
    }

    public static bool IsMapVisible()
    {
        return _activeController != null && _activeController._isMapVisible;
    }

    private void Start()
    {
        InitializeIfNeeded();
        HideMap();
    }

    private void OnStageCleared(StageClearedEvent evt)
    {
        InitializeIfNeeded();

        if (_routeData == null)
            return;

        if (!string.IsNullOrEmpty(_selectedNodeId))
        {
            RuntimeNode selectedNode = GetRuntimeNode(_selectedNodeId);
            _rewardApplier?.Apply(_selectedNodeId, selectedNode?.Reward, _routeData.RitualPointsPerClear);
            _currentNodeId = _selectedNodeId;
            _selectedNodeId = null;
        }
        else if (string.IsNullOrEmpty(_currentNodeId))
        {
            _currentNodeId = _routeData.StartNodeId;
        }

        UnlockManager unlockManager = FindFirstObjectByType<UnlockManager>(FindObjectsInactive.Include);
        unlockManager?.UnlockSkillsForClearedStage(evt.StageIndex);

        if (_currentNodeId == _routeData.FinalNodeId)
        {
            HideMap();
            Debug.Log("[StageMap] Final node reached.");
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.GameClear)
            GameManager.Instance.ChangeState(GameState.Playing);

        if (UIManager.Instance != null)
            UIManager.Instance.HideAllPanels();

        ShowMap();
    }

    private void InitializeIfNeeded()
    {
        if (_isInitialized)
            return;

        if (_routeData == null)
        {
            Debug.LogWarning("[StageMap] RouteData is not assigned.");
            return;
        }

        EnsureCanvas();
        EnsureMapRoot();
        BuildRuntimeRoute();
        UnlockManager unlockManager = FindFirstObjectByType<UnlockManager>(FindObjectsInactive.Include);
        if (unlockManager != null)
        {
            unlockManager.RegisterLockedUnits(_routeData.UnlockableRatUnits);
            unlockManager.RegisterSkillUnlocks(_routeData.SkillUnlocks);
        }
        BuildMap();
        _currentNodeId = _routeData.StartNodeId;
        _isInitialized = true;
    }

    private bool HasNextMapStepAfterCurrentClear()
    {
        if (_routeData == null)
            return false;

        string nextPosition = !string.IsNullOrEmpty(_selectedNodeId) ? _selectedNodeId : _currentNodeId;
        if (string.IsNullOrEmpty(nextPosition))
            nextPosition = _routeData.StartNodeId;

        if (nextPosition == _routeData.FinalNodeId)
            return false;

        RuntimeNode node = GetRuntimeNode(nextPosition);
        return node != null && node.NextNodeIds.Count > 0;
    }

    private void BuildRuntimeRoute()
    {
        _runtimeNodes.Clear();
        _runtimeNodeList.Clear();

        IReadOnlyList<StageMapNodeData> sourceNodes = _routeData.Nodes;
        for (int i = 0; i < sourceNodes.Count; i++)
        {
            StageMapNodeData source = sourceNodes[i];
            if (source == null) continue;

            var node = new RuntimeNode
            {
                NodeId = source.NodeId,
                StageIndex = source.StageIndex,
                Position = source.NormalizedPosition,
                Reward = source.Reward
            };

            for (int j = 0; j < source.NextNodeIds.Count; j++)
                node.NextNodeIds.Add(source.NextNodeIds[j]);

            _runtimeNodes[node.NodeId] = node;
            _runtimeNodeList.Add(node);
        }

        if (_routeData.UnlockableRatUnits != null && _routeData.UnlockableRatUnits.Count > 0)
        {
            AssignRouteRewardPool();
        }
        else if (_randomizeOnStart)
        {
            RandomizeRewards();
        }

        if (_randomizeOnStart)
            RandomizePositionsAndConnections();
    }

    private void RandomizeRewards()
    {
        var rewardPool = new List<StageMapReward>();
        for (int i = 0; i < _runtimeNodeList.Count; i++)
        {
            RuntimeNode node = _runtimeNodeList[i];
            if (IsEndpoint(node.NodeId) || node.Reward == null || node.Reward.Type == StageMapRewardType.None)
                continue;

            rewardPool.Add(node.Reward);
        }

        Shuffle(rewardPool);

        int rewardIndex = 0;
        for (int i = 0; i < _runtimeNodeList.Count; i++)
        {
            RuntimeNode node = _runtimeNodeList[i];
            if (IsEndpoint(node.NodeId) || node.Reward == null || node.Reward.Type == StageMapRewardType.None)
                continue;

            node.Reward = rewardPool[rewardIndex++];
        }
    }

    private void AssignRouteRewardPool()
    {
        var rewardNodes = new List<RuntimeNode>();
        for (int i = 0; i < _runtimeNodeList.Count; i++)
        {
            RuntimeNode node = _runtimeNodeList[i];
            if (!IsEndpoint(node.NodeId))
                rewardNodes.Add(node);
        }

        Shuffle(rewardNodes);

        var rewardPool = new List<StageMapReward>();
        IReadOnlyList<UnitDataSO> unlockUnits = _routeData.UnlockableRatUnits;
        for (int i = 0; i < unlockUnits.Count; i++)
        {
            UnitDataSO unit = unlockUnits[i];
            if (unit != null)
                rewardPool.Add(StageMapReward.RatTowerUnlock(unit));
        }

        while (rewardPool.Count < rewardNodes.Count)
            rewardPool.Add(StageMapReward.ProductionFacility());

        Shuffle(rewardPool);

        int count = Mathf.Min(rewardNodes.Count, rewardPool.Count);
        for (int i = 0; i < count; i++)
            rewardNodes[i].Reward = rewardPool[i];
    }

    private void RandomizePositionsAndConnections()
    {
        var columns = CollectRouteColumns();
        for (int i = 0; i < columns.Count; i++)
            AlignColumnPositions(columns[i]);

        for (int i = 0; i < _runtimeNodeList.Count; i++)
            _runtimeNodeList[i].NextNodeIds.Clear();

        RuntimeNode start = GetRuntimeNode(_routeData.StartNodeId);
        RuntimeNode final = GetRuntimeNode(_routeData.FinalNodeId);

        if (start == null || final == null || columns.Count == 0)
            return;

        ConnectStartToFirstColumn(start, columns[0]);

        for (int i = 0; i < columns.Count - 1; i++)
            ConnectColumns(columns[i], columns[i + 1]);

        for (int i = 0; i < columns[columns.Count - 1].Count; i++)
            columns[columns.Count - 1][i].NextNodeIds.Add(final.NodeId);
    }

    private List<List<RuntimeNode>> CollectRouteColumns()
    {
        var columns = new List<List<RuntimeNode>>();

        for (int i = 0; i < _runtimeNodeList.Count; i++)
        {
            RuntimeNode node = _runtimeNodeList[i];
            if (IsEndpoint(node.NodeId))
                continue;

            int columnIndex = FindColumnIndex(columns, node.Position.x);
            if (columnIndex < 0)
            {
                columns.Add(new List<RuntimeNode> { node });
            }
            else
            {
                columns[columnIndex].Add(node);
            }
        }

        columns.Sort((a, b) => a[0].Position.x.CompareTo(b[0].Position.x));
        return columns;
    }

    private int FindColumnIndex(List<List<RuntimeNode>> columns, float x)
    {
        const float columnTolerance = 0.03f;
        for (int i = 0; i < columns.Count; i++)
        {
            if (Mathf.Abs(columns[i][0].Position.x - x) <= columnTolerance)
                return i;
        }

        return -1;
    }

    private void AlignColumnPositions(List<RuntimeNode> column)
    {
        column.Sort((a, b) => a.Position.y.CompareTo(b.Position.y));

        for (int i = 0; i < column.Count; i++)
        {
            float t = column.Count == 1 ? 0.5f : i / (float)(column.Count - 1);
            float y = Mathf.Lerp(_randomYRange.x, _randomYRange.y, t);
            column[i].Position = new Vector2(column[i].Position.x, y);
        }
    }

    private void ConnectColumns(List<RuntimeNode> fromColumn, List<RuntimeNode> toColumn)
    {
        var shuffledTargets = new List<RuntimeNode>(toColumn);
        Shuffle(shuffledTargets);

        for (int i = 0; i < shuffledTargets.Count; i++)
        {
            RuntimeNode from = FindSourceWithFewestConnections(fromColumn);
            AddUniqueConnection(from, shuffledTargets[i].NodeId);
        }

        for (int i = 0; i < fromColumn.Count; i++)
        {
            RuntimeNode from = fromColumn[i];
            int minConnections = Mathf.Clamp(_minConnectionsPerNode, 1, toColumn.Count);
            int maxConnections = Mathf.Clamp(_maxConnectionsPerNode, minConnections, toColumn.Count);
            int targetConnectionCount = Random.Range(minConnections, maxConnections + 1);

            while (from.NextNodeIds.Count < targetConnectionCount)
            {
                RuntimeNode target = PickUnconnectedTarget(from, toColumn);
                if (target == null)
                    break;

                AddUniqueConnection(from, target.NodeId);
            }
        }
    }

    private void ConnectStartToFirstColumn(RuntimeNode start, List<RuntimeNode> firstColumn)
    {
        for (int i = 0; i < firstColumn.Count; i++)
            AddUniqueConnection(start, firstColumn[i].NodeId);
    }

    private RuntimeNode FindSourceWithFewestConnections(List<RuntimeNode> fromColumn)
    {
        RuntimeNode best = fromColumn[0];
        for (int i = 1; i < fromColumn.Count; i++)
        {
            RuntimeNode candidate = fromColumn[i];
            if (candidate.NextNodeIds.Count < best.NextNodeIds.Count)
                best = candidate;
        }

        return best;
    }

    private RuntimeNode PickUnconnectedTarget(RuntimeNode from, List<RuntimeNode> toColumn)
    {
        var candidates = new List<RuntimeNode>();
        for (int i = 0; i < toColumn.Count; i++)
        {
            RuntimeNode target = toColumn[i];
            if (!from.NextNodeIds.Contains(target.NodeId))
                candidates.Add(target);
        }

        if (candidates.Count == 0)
            return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    private void AddUniqueConnection(RuntimeNode from, string toNodeId)
    {
        if (!from.NextNodeIds.Contains(toNodeId))
            from.NextNodeIds.Add(toNodeId);
    }

    private void EnsureCanvas()
    {
        if (_targetCanvas != null)
            return;

        GameObject canvasObj = new GameObject("StageMapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _targetCanvas = canvasObj.GetComponent<Canvas>();
        _targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _targetCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
    }

    private void EnsureMapRoot()
    {
        if (_mapRoot != null)
            return;

        GameObject rootObj = new GameObject("StageMapRoot", typeof(RectTransform));
        _mapRoot = rootObj.GetComponent<RectTransform>();
        _mapRoot.SetParent(_targetCanvas.transform, false);
        _mapRoot.anchorMin = Vector2.zero;
        _mapRoot.anchorMax = Vector2.one;
        _mapRoot.offsetMin = Vector2.zero;
        _mapRoot.offsetMax = Vector2.zero;
    }

    private void BuildMap()
    {
        ClearChildren(_mapRoot);
        _buttons.Clear();

        RectTransform inputBlockerRoot = CreateChild("InputBlocker", _mapRoot).GetComponent<RectTransform>();
        RectTransform backgroundRoot = CreateChild("Background", _mapRoot).GetComponent<RectTransform>();
        RectTransform lineRoot = CreateChild("Lines", _mapRoot).GetComponent<RectTransform>();
        RectTransform nodeRoot = CreateChild("Nodes", _mapRoot).GetComponent<RectTransform>();
        Stretch(inputBlockerRoot);
        StretchWithPadding(backgroundRoot, 300f);
        Stretch(lineRoot);
        Stretch(nodeRoot);

        Image inputBlocker = inputBlockerRoot.gameObject.AddComponent<Image>();
        inputBlocker.color = Color.clear;
        inputBlocker.raycastTarget = true;

        Image background = backgroundRoot.gameObject.AddComponent<Image>();
        background.color = _backgroundColor;
        background.raycastTarget = true;

        IReadOnlyList<RuntimeNode> nodes = _runtimeNodeList;
        for (int i = 0; i < nodes.Count; i++)
        {
            RuntimeNode from = nodes[i];
            if (from == null) continue;

            for (int j = 0; j < from.NextNodeIds.Count; j++)
            {
                RuntimeNode to = GetRuntimeNode(from.NextNodeIds[j]);
                if (to != null)
                    CreateLine(lineRoot, MapToAnchored(from.Position), MapToAnchored(to.Position));
            }
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            RuntimeNode node = nodes[i];
            if (node != null)
                CreateNodeButton(nodeRoot, node);
        }

        RefreshNodeStates();
    }

    private void CreateNodeButton(RectTransform parent, RuntimeNode node)
    {
        GameObject obj = CreateChild(node.NodeId, parent);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = node.NodeId == _routeData.FinalNodeId ? new Vector2(96f, 96f) : new Vector2(72f, 72f);
        rect.anchoredPosition = MapToAnchored(node.Position);

        Image image = obj.AddComponent<Image>();
        image.color = node.NodeId == _routeData.FinalNodeId ? _finalNodeColor : _lockedNodeColor;

        Button button = obj.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        string capturedNodeId = node.NodeId;
        button.onClick.AddListener(() => SelectNode(capturedNodeId));
        _buttons[capturedNodeId] = button;

        Sprite rewardIcon = ResolveRewardIcon(node.Reward);
        if (rewardIcon != null)
        {
            GameObject iconObj = CreateChild("RewardIcon", rect);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(34f, 34f);
            iconRect.anchoredPosition = new Vector2(0f, 54f);

            Image icon = iconObj.AddComponent<Image>();
            icon.sprite = rewardIcon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
    }

    private void SelectNode(string nodeId)
    {
        if (_routeData == null || !CanMove(_currentNodeId, nodeId))
            return;

        RuntimeNode node = GetRuntimeNode(nodeId);
        if (node == null)
            return;

        _selectedNodeId = node.NodeId;
        HideMap();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowInGamePanel();

        EventBus.Instance?.Publish(new StageMapNodeSelectedEvent
        {
            NodeId = node.NodeId,
            StageIndex = node.StageIndex
        });

        if (StageManager.Instance == null)
        {
            Debug.LogError("[StageMap] StageManager.Instance not found.");
            return;
        }

        StageManager.Instance.StartStageFromMapNode(node.StageIndex, _routeData.WaveStartDelay);
    }

    private void RefreshNodeStates()
    {
        foreach (var pair in _buttons)
        {
            RuntimeNode node = GetRuntimeNode(pair.Key);
            Button button = pair.Value;
            bool isCurrent = pair.Key == _currentNodeId;
            bool canMove = CanMove(_currentNodeId, pair.Key);

            button.interactable = canMove;

            Image image = button.GetComponent<Image>();
            if (image == null || node == null) continue;

            if (node.NodeId == _routeData.FinalNodeId)
                image.color = canMove ? _finalNodeColor : Color.Lerp(_lockedNodeColor, _finalNodeColor, 0.35f);
            else if (isCurrent)
                image.color = _selectedNodeColor;
            else
                image.color = canMove ? _availableNodeColor : _lockedNodeColor;
        }
    }

    private RuntimeNode GetRuntimeNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return null;

        _runtimeNodes.TryGetValue(nodeId, out RuntimeNode node);
        return node;
    }

    private bool CanMove(string fromNodeId, string toNodeId)
    {
        RuntimeNode from = GetRuntimeNode(fromNodeId);
        return from != null && from.NextNodeIds.Contains(toNodeId);
    }

    private bool IsEndpoint(string nodeId)
    {
        return nodeId == _routeData.StartNodeId || nodeId == _routeData.FinalNodeId;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temp;
        }
    }

    private void ShowMap()
    {
        if (_mapRoot == null) return;
        RefreshNodeStates();
        SetMapVisible(true);
        _mapRoot.gameObject.SetActive(true);
    }

    private void HideMap()
    {
        if (_mapRoot != null)
            _mapRoot.gameObject.SetActive(false);

        SetMapVisible(false);
    }

    private void SetMapVisible(bool isVisible)
    {
        if (_isMapVisible == isVisible)
            return;

        _isMapVisible = isVisible;
        if (isVisible)
            PauseGameForMap();
        else
            ResumeGameAfterMap();

        EventBus.Instance?.Publish(new StageMapVisibilityChangedEvent { IsVisible = isVisible });
    }

    private void PauseGameForMap()
    {
        if (_hasPausedTimeScale)
            return;

        _timeScaleBeforeMap = Time.timeScale;
        Time.timeScale = 0f;
        _hasPausedTimeScale = true;
    }

    private void ResumeGameAfterMap()
    {
        if (!_hasPausedTimeScale)
            return;

        Time.timeScale = _timeScaleBeforeMap;
        _hasPausedTimeScale = false;
    }

    private Sprite ResolveRewardIcon(StageMapReward reward)
    {
        if (reward == null) return null;
        if (reward.Icon != null) return reward.Icon;

        return reward.Type switch
        {
            StageMapRewardType.ProductionFacility => _productionRewardIcon,
            StageMapRewardType.RatTowerUnlock => reward.UnitUnlock != null && reward.UnitUnlock.Icon != null ? reward.UnitUnlock.Icon : _ratTowerRewardIcon,
            _ => null
        };
    }

    private Vector2 MapToAnchored(Vector2 normalized)
    {
        return new Vector2(
            (normalized.x - 0.5f) * _mapSize.x,
            (normalized.y - 0.5f) * _mapSize.y);
    }

    private void CreateLine(RectTransform parent, Vector2 from, Vector2 to)
    {
        GameObject obj = CreateChild("Line", parent);
        Image image = obj.AddComponent<Image>();
        image.color = _lineColor;
        image.raycastTarget = false;

        RectTransform rect = obj.GetComponent<RectTransform>();
        Vector2 delta = to - from;
        rect.sizeDelta = new Vector2(delta.magnitude, 8f);
        rect.anchoredPosition = from + delta * 0.5f;
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private GameObject CreateChild(string childName, Transform parent)
    {
        GameObject obj = new GameObject(childName, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return obj;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void StretchWithPadding(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}
