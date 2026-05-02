using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
    [SerializeField] private RectTransform _inputBlockerRoot;
    [SerializeField] private RectTransform _backgroundRoot;
    [SerializeField] private RectTransform _lineRoot;
    [SerializeField] private RectTransform _nodeRoot;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private GameObject _doctrinePanelPrefab;
    [SerializeField] private RectTransform _doctrinePanelRoot;
    [Header("Stage Rewards")]
    [Tooltip("Stage indices that grant a doctrine point and open the doctrine selection panel after clear.")]
    [SerializeField] private List<int> _doctrineRewardStageIndices = new List<int> { 0, 2, 4, 6, 8 };
    [Header("Reward Choice UI")]
    [SerializeField] private RectTransform _choiceRewardPanelRoot;
    [SerializeField] private Button _productionChoiceButton;
    [SerializeField] private Image _productionChoiceIcon;
    [SerializeField] private TextMeshProUGUI _productionChoiceLabel;
    [SerializeField] private TextMeshProUGUI _productionChoiceDetail;
    [SerializeField] private Button _unitChoiceButton;
    [SerializeField] private Image _unitChoiceIcon;
    [SerializeField] private TextMeshProUGUI _unitChoiceLabel;
    [SerializeField] private TextMeshProUGUI _unitChoiceDetail;
    [SerializeField] private Vector2 _mapSize = new Vector2(1280f, 600f);
    [SerializeField] private bool _autoCreateMapHierarchy = true;
    [SerializeField] private bool _refreshExistingMapVisuals = true;
    [SerializeField] private bool _useHierarchyNodePositions = true;
    [SerializeField] private bool _saveHierarchyNodePositions = true;
    [SerializeField] private List<StageMapNodeLayoutOverride> _nodeLayoutOverrides = new List<StageMapNodeLayoutOverride>();

    [Header("Random Route")]
    [SerializeField] private bool _randomizePathOnStart = true;
    [SerializeField, HideInInspector] private bool _randomizeOnStart = true; // Legacy field migration.
    [SerializeField, HideInInspector] private bool _randomizeSettingsInitialized;
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
    [SerializeField] private Color _titleTextColor = Color.white;
    [SerializeField] private int _titleFontSize = 42;
    [SerializeField] private TMP_FontAsset _titleFontAsset;

    private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
    private readonly Dictionary<string, RuntimeNode> _runtimeNodes = new Dictionary<string, RuntimeNode>();
    private readonly List<RuntimeNode> _runtimeNodeList = new List<RuntimeNode>();
    private string _currentNodeId;
    private RuntimeNode _pendingClearedNode;
    private bool _waitingForDoctrineSelection;
    private bool _isInitialized;
    private bool _isMapVisible;
    private bool _hasPausedTimeScale;
    private float _timeScaleBeforeMap = 1f;
    private bool _suppressDoctrinePanel = false;

    [System.Serializable]
    private class StageMapNodeLayoutOverride
    {
        public string NodeId;
        public Vector2 AnchoredPosition;
    }

    private class RuntimeNode
    {
        public string NodeId;
        public int StageIndex;
        public Vector2 Position;
        public StageMapReward Reward;
        public UnitDataSO ChoiceUnitUnlock;
        public readonly List<string> NextNodeIds = new List<string>();
    }

    private void Awake()
    {
        EnsureRandomizeSettingsInitialized();

        if (_rewardApplier == null)
            _rewardApplier = GetComponent<StageMapRewardApplier>();
    }

    private void OnValidate()
    {
        EnsureRandomizeSettingsInitialized();

#if UNITY_EDITOR
        if (_titleFontAsset == null)
        {
            string[] guids = AssetDatabase.FindAssets("Galmuri9 t:TMP_FontAsset");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _titleFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            }
        }
#endif
    }

    private void OnEnable()
    {
        _activeController = this;

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<StageClearedEvent>(OnStageCleared);
            EventBus.Instance.Subscribe<DoctrineSelectionConfirmedEvent>(OnDoctrineSelectionConfirmed);
        }
    }

    private void OnDisable()
    {
        SetMapVisible(false);

        if (_activeController == this)
            _activeController = null;

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<StageClearedEvent>(OnStageCleared);
            EventBus.Instance.Unsubscribe<DoctrineSelectionConfirmedEvent>(OnDoctrineSelectionConfirmed);
        }
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

        RuntimeNode clearedNode = FindRuntimeNodeByStageIndex(evt.StageIndex);
        if (clearedNode == null)
            clearedNode = GetRuntimeNode(_currentNodeId);

        if (clearedNode == null)
            return;

        _currentNodeId = clearedNode.NodeId;
        _pendingClearedNode = clearedNode;

        ApplyClearRewards(clearedNode);

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

        if (ShouldGrantDoctrinePoint(clearedNode.StageIndex))
        {
            _rewardApplier?.AddDoctrinePoint(1);
            if (ShowDoctrinePanelOnly())
                return;

            Debug.LogWarning("[StageMap] Doctrine panel is not available. Continuing without doctrine selection.");
            ContinueAfterDoctrineStep();
            return;
        }

        ContinueAfterDoctrineStep();
    }

    private void OnDoctrineSelectionConfirmed(DoctrineSelectionConfirmedEvent evt)
    {
        if (!_waitingForDoctrineSelection)
            return;

        _waitingForDoctrineSelection = false;
        ContinueAfterDoctrineStep();
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
        EnsureDoctrinePanel();
        EnsureMapHierarchy();
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

        string currentPosition = string.IsNullOrEmpty(_currentNodeId) ? _routeData.StartNodeId : _currentNodeId;
        if (currentPosition == _routeData.FinalNodeId)
            return false;

        RuntimeNode node = GetRuntimeNode(currentPosition);
        return node != null && node.NextNodeIds.Count > 0;
    }

    private void ApplyClearRewards(RuntimeNode clearedNode)
    {
        if (clearedNode == null || clearedNode.StageIndex == 0 || IsFinalStageNode(clearedNode))
            return;

        _rewardApplier?.Apply(clearedNode.NodeId, StageMapReward.ProductionFacility(1, _productionRewardIcon), 0);
    }

    private void ContinueAfterDoctrineStep()
    {
        RuntimeNode clearedNode = _pendingClearedNode;
        if (clearedNode == null)
            return;

        if (ShouldShowChoiceReward(clearedNode.StageIndex))
        {
            ShowChoiceRewardPanel(clearedNode);
            return;
        }

        StartNextLinearStage(clearedNode);
    }

    private void StartNextLinearStage(RuntimeNode clearedNode)
    {
        if (clearedNode == null)
            return;

        RuntimeNode nextNode = GetNextLinearNode(clearedNode);
        _pendingClearedNode = null;

        if (nextNode == null)
        {
            HideMap();
            Debug.LogWarning($"[StageMap] Next node not found after {clearedNode.NodeId}.");
            return;
        }

        _currentNodeId = nextNode.NodeId;
        HideMap();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowInGamePanel();

        EventBus.Instance?.Publish(new StageMapNodeSelectedEvent
        {
            NodeId = nextNode.NodeId,
            StageIndex = nextNode.StageIndex
        });

        if (StageManager.Instance == null)
        {
            Debug.LogError("[StageMap] StageManager.Instance not found.");
            return;
        }

        StageManager.Instance.StartStageFromMapNode(nextNode.StageIndex, _routeData.WaveStartDelay);
    }

    private RuntimeNode GetNextLinearNode(RuntimeNode node)
    {
        if (node == null || node.NextNodeIds.Count == 0)
            return null;

        return GetRuntimeNode(node.NextNodeIds[0]);
    }

    private RuntimeNode FindRuntimeNodeByStageIndex(int stageIndex)
    {
        for (int i = 0; i < _runtimeNodeList.Count; i++)
        {
            RuntimeNode node = _runtimeNodeList[i];
            if (node != null && node.StageIndex == stageIndex)
                return node;
        }

        return null;
    }

    private bool ShouldGrantDoctrinePoint(int stageIndex)
    {
        return _doctrineRewardStageIndices != null && _doctrineRewardStageIndices.Contains(stageIndex);
    }

    private bool ShouldShowChoiceReward(int stageIndex)
    {
        return stageIndex == 3 || stageIndex == 6;
    }

    private bool IsFinalStageNode(RuntimeNode node)
    {
        return node != null && node.NodeId == _routeData.FinalNodeId;
    }

    private void BuildRuntimeRoute(bool allowRandomize = true)
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
                Reward = source.Reward,
                ChoiceUnitUnlock = source.ChoiceUnitUnlock
            };

            for (int j = 0; j < source.NextNodeIds.Count; j++)
                node.NextNodeIds.Add(source.NextNodeIds[j]);

            _runtimeNodes[node.NodeId] = node;
            _runtimeNodeList.Add(node);
        }

        bool appliedCustomLayout = false;

        if (!(allowRandomize && _randomizePathOnStart))
        {
            //랜덤화가 꺼져 있을 때만 저장된 레이아웃 적용
            bool appliedSavedLayout = ApplySavedNodeLayoutOverrides();
            bool appliedHierarchyLayout = ApplyHierarchyNodePositions();
            appliedCustomLayout = appliedSavedLayout || appliedHierarchyLayout;
        }

        if (allowRandomize && _randomizePathOnStart && !appliedCustomLayout)
            RandomizePositionsAndConnections();
    }

    private bool ApplyHierarchyNodePositions()
    {
        if (!_useHierarchyNodePositions || _nodeRoot == null)
            return false;

        bool appliedAny = false;
        for (int i = 0; i < _runtimeNodeList.Count; i++)
        {
            RuntimeNode node = _runtimeNodeList[i];
            if (node == null)
                continue;

            Transform existing = _nodeRoot.Find(node.NodeId);
            RectTransform rect = existing as RectTransform;
            if (rect == null)
                continue;

            node.Position = AnchoredToMap(rect.anchoredPosition);
            appliedAny = true;
        }

        return appliedAny;
    }

    private bool ApplySavedNodeLayoutOverrides()
    {
        if (_nodeLayoutOverrides == null || _nodeLayoutOverrides.Count == 0)
            return false;

        bool appliedAny = false;
        for (int i = 0; i < _runtimeNodeList.Count; i++)
        {
            RuntimeNode node = _runtimeNodeList[i];
            if (node == null)
                continue;

            if (!TryGetSavedNodeLayout(node.NodeId, out Vector2 anchoredPosition))
                continue;

            node.Position = AnchoredToMap(anchoredPosition);
            appliedAny = true;
        }

        return appliedAny;
    }

    private bool CaptureHierarchyNodeLayout()
    {
        if (!_saveHierarchyNodePositions || _routeData == null || _nodeRoot == null)
            return false;

        bool capturedAny = false;
        IReadOnlyList<StageMapNodeData> sourceNodes = _routeData.Nodes;
        for (int i = 0; i < sourceNodes.Count; i++)
        {
            StageMapNodeData source = sourceNodes[i];
            if (source == null || string.IsNullOrEmpty(source.NodeId))
                continue;

            Transform existing = _nodeRoot.Find(source.NodeId);
            RectTransform rect = existing as RectTransform;
            if (rect == null)
                continue;

            SetSavedNodeLayout(source.NodeId, rect.anchoredPosition);
            capturedAny = true;
        }

        return capturedAny;
    }

    private bool TryGetSavedNodeLayout(string nodeId, out Vector2 anchoredPosition)
    {
        if (_nodeLayoutOverrides != null)
        {
            for (int i = 0; i < _nodeLayoutOverrides.Count; i++)
            {
                StageMapNodeLayoutOverride nodeLayout = _nodeLayoutOverrides[i];
                if (nodeLayout == null || nodeLayout.NodeId != nodeId)
                    continue;

                anchoredPosition = nodeLayout.AnchoredPosition;
                return true;
            }
        }

        anchoredPosition = Vector2.zero;
        return false;
    }

    private void SetSavedNodeLayout(string nodeId, Vector2 anchoredPosition)
    {
        if (_nodeLayoutOverrides == null)
            _nodeLayoutOverrides = new List<StageMapNodeLayoutOverride>();

        for (int i = 0; i < _nodeLayoutOverrides.Count; i++)
        {
            StageMapNodeLayoutOverride nodeLayout = _nodeLayoutOverrides[i];
            if (nodeLayout == null || nodeLayout.NodeId != nodeId)
                continue;

            nodeLayout.AnchoredPosition = anchoredPosition;
            return;
        }

        _nodeLayoutOverrides.Add(new StageMapNodeLayoutOverride
        {
            NodeId = nodeId,
            AnchoredPosition = anchoredPosition
        });
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

    private void EnsureDoctrinePanel()
    {
        if (_doctrinePanelRoot != null || _doctrinePanelPrefab == null || _targetCanvas == null)
            return;

        GameObject instance = Instantiate(_doctrinePanelPrefab, _targetCanvas.transform, false);
        _doctrinePanelRoot = instance.GetComponent<RectTransform>();
        if (_doctrinePanelRoot == null)
            _doctrinePanelRoot = instance.GetComponentInChildren<RectTransform>(true);
    }

    private void BuildMap()
    {
        EnsureMapHierarchy();

        if (_lineRoot == null || _nodeRoot == null || _inputBlockerRoot == null || _backgroundRoot == null)
        {
            Debug.LogWarning("[StageMap] Map hierarchy is not assigned.");
            return;
        }

        if (CaptureHierarchyNodeLayout())
        {
            ApplySavedNodeLayoutOverrides();
            MarkLayoutDirty();
        }

        ClearChildren(_lineRoot);
        ClearChildren(_nodeRoot);
        _buttons.Clear();

        Image inputBlocker = GetOrAddComponent<Image>(_inputBlockerRoot.gameObject);
        inputBlocker.color = Color.clear;
        inputBlocker.raycastTarget = true;

        bool hadBackgroundImage = _backgroundRoot.TryGetComponent(out Image background);
        if (!hadBackgroundImage)
        {
            background = _backgroundRoot.gameObject.AddComponent<Image>();
            background.color = _backgroundColor;
        }

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
                    CreateLine(_lineRoot, MapToAnchored(from.Position), MapToAnchored(to.Position));
            }
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            RuntimeNode node = nodes[i];
            if (node != null)
                CreateNodeButton(_nodeRoot, node);
        }

        RefreshNodeStates();
    }

    [ContextMenu("Build Editable Stage Map Hierarchy")]
    private void BuildEditableStageMapHierarchy()
    {
        EnsureCanvas();
        EnsureMapRoot();
        EnsureMapHierarchy();

        if (_routeData != null)
        {
            BuildRuntimeRoute(false);
            BuildMap();
        }
    }

    [ContextMenu("Capture Stage Map Node Layout From Hierarchy")]
    private void CaptureStageMapNodeLayoutFromHierarchy()
    {
        EnsureMapHierarchy();
        if (CaptureHierarchyNodeLayout())
            MarkLayoutDirty();
    }

    private void EnsureMapHierarchy()
    {
        if (!_autoCreateMapHierarchy)
            return;

        _inputBlockerRoot = ResolveOrCreateRect(_mapRoot, _inputBlockerRoot, "InputBlocker", out _);
        _backgroundRoot = ResolveOrCreateRect(_mapRoot, _backgroundRoot, "Background", out bool createdBackground);
        _lineRoot = ResolveOrCreateRect(_mapRoot, _lineRoot, "Lines", out _);
        _nodeRoot = ResolveOrCreateRect(_mapRoot, _nodeRoot, "Nodes", out _);
        _titleText = ResolveOrCreateTitle(_mapRoot, out bool createdTitle);

        if (!_refreshExistingMapVisuals)
            return;

        Stretch(_inputBlockerRoot);
        Stretch(_lineRoot);
        Stretch(_nodeRoot);

        // Scene-authored Background and Title values should survive play mode.
        if (createdBackground)
            StretchWithPadding(_backgroundRoot, 300f);

        if (createdTitle)
            ConfigureTitle(_titleText);

        _inputBlockerRoot.SetAsFirstSibling();
        _backgroundRoot.SetSiblingIndex(1);
        _lineRoot.SetSiblingIndex(2);
        _nodeRoot.SetSiblingIndex(3);
        _titleText.transform.SetAsLastSibling();
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
        button.interactable = false;
        _buttons[node.NodeId] = button;

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

    private void RefreshNodeStates()
    {
        foreach (var pair in _buttons)
        {
            RuntimeNode node = GetRuntimeNode(pair.Key);
            Button button = pair.Value;
            bool isCurrent = pair.Key == _currentNodeId;
            bool canMove = CanMove(_currentNodeId, pair.Key);

            button.interactable = false;

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
        if (!_suppressDoctrinePanel)
        {
            EnsureDoctrinePanel();
            if (_doctrinePanelRoot != null)
            {
                _doctrinePanelRoot.gameObject.SetActive(true);
                _doctrinePanelRoot.SetAsLastSibling();
            }
        }
    }

    private bool ShowDoctrinePanelOnly()
    {
        SetMapVisible(true);

        if (_mapRoot != null)
            _mapRoot.gameObject.SetActive(false);

        EnsureDoctrinePanel();
        if (_doctrinePanelRoot == null)
            return false;

        _waitingForDoctrineSelection = true;
        _doctrinePanelRoot.gameObject.SetActive(true);
        _doctrinePanelRoot.SetAsLastSibling();
        return true;
    }

    private void ShowChoiceRewardPanel(RuntimeNode clearedNode)
    {
        SetMapVisible(true);

        if (!EnsureChoiceRewardPanel())
        {
            Debug.LogWarning("[StageMap] Choice reward panel could not be created.");
            StartNextLinearStage(clearedNode);
            return;
        }

        ConfigureProductionChoice(clearedNode);
        ConfigureUnitChoice(clearedNode);

        _choiceRewardPanelRoot.gameObject.SetActive(true);
        _choiceRewardPanelRoot.SetAsLastSibling();
    }

    [ContextMenu("Build Stage Map Choice Reward UI")]
    public void BuildStageMapChoiceRewardUI()
    {
        EnsureCanvas();
        EnsureChoiceRewardPanel();

        if (_choiceRewardPanelRoot != null && !Application.isPlaying)
            _choiceRewardPanelRoot.gameObject.SetActive(true);
    }

    private void ConfigureProductionChoice(RuntimeNode clearedNode)
    {
        if (_productionChoiceLabel != null)
            _productionChoiceLabel.text = "생산 시설 블록 추가 획득";

        if (_productionChoiceDetail != null)
            _productionChoiceDetail.text = string.Empty;

        if (_productionChoiceIcon != null)
        {
            _productionChoiceIcon.sprite = _productionRewardIcon;
            _productionChoiceIcon.enabled = _productionRewardIcon != null;
        }

        if (_productionChoiceButton == null)
            return;

        _productionChoiceButton.interactable = true;
        _productionChoiceButton.onClick.RemoveAllListeners();
        _productionChoiceButton.onClick.AddListener(() =>
        {
            _rewardApplier?.Apply(
                clearedNode.NodeId,
                StageMapReward.ProductionFacility(1, _productionRewardIcon),
                0,
                "ChoiceRewardPanel",
                0,
                "ProductionFacility",
                _productionChoiceLabel != null ? _productionChoiceLabel.text : "생산 시설 블록 추가 획득");
            if (_choiceRewardPanelRoot != null)
                _choiceRewardPanelRoot.gameObject.SetActive(false);
            StartNextLinearStage(clearedNode);
        });
    }

    private void ConfigureUnitChoice(RuntimeNode clearedNode)
    {
        UnitDataSO unit = clearedNode.ChoiceUnitUnlock;

        if (_unitChoiceLabel != null)
            _unitChoiceLabel.text = unit != null ? $"{unit.UnitName} 해금" : "쥐 해금";

        if (_unitChoiceDetail != null)
            _unitChoiceDetail.text = BuildUnitRewardDescription(unit);

        if (_unitChoiceIcon != null)
        {
            Sprite icon = unit != null && unit.Icon != null ? unit.Icon : _ratTowerRewardIcon;
            _unitChoiceIcon.sprite = icon;
            _unitChoiceIcon.enabled = icon != null;
        }

        if (_unitChoiceButton == null)
            return;

        _unitChoiceButton.interactable = unit != null;
        _unitChoiceButton.onClick.RemoveAllListeners();
        _unitChoiceButton.onClick.AddListener(() =>
        {
            _rewardApplier?.Apply(
                clearedNode.NodeId,
                StageMapReward.RatTowerUnlock(unit),
                0,
                "ChoiceRewardPanel",
                1,
                "RatTowerUnlock",
                _unitChoiceLabel != null ? _unitChoiceLabel.text : "쥐 해금");
            if (_choiceRewardPanelRoot != null)
                _choiceRewardPanelRoot.gameObject.SetActive(false);
            StartNextLinearStage(clearedNode);
        });
    }

    private bool EnsureChoiceRewardPanel()
    {
        if (_choiceRewardPanelRoot != null)
        {
            ResolveChoiceRewardReferences();
            EnsureChoiceRewardChildren();
            return true;
        }

        EnsureCanvas();
        if (_targetCanvas == null)
            return false;

        Transform existing = _targetCanvas.transform.Find("StageMapChoiceRewardPanel");
        if (existing != null && existing.TryGetComponent(out RectTransform existingRoot))
        {
            _choiceRewardPanelRoot = existingRoot;
            ResolveChoiceRewardReferences();
            EnsureChoiceRewardChildren();
            return true;
        }

        GameObject panel = new GameObject("StageMapChoiceRewardPanel", typeof(RectTransform), typeof(Image));
        _choiceRewardPanelRoot = panel.GetComponent<RectTransform>();
        _choiceRewardPanelRoot.SetParent(_targetCanvas.transform, false);
        _choiceRewardPanelRoot.anchorMin = Vector2.zero;
        _choiceRewardPanelRoot.anchorMax = Vector2.one;
        _choiceRewardPanelRoot.offsetMin = Vector2.zero;
        _choiceRewardPanelRoot.offsetMax = Vector2.zero;

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.72f);
        background.raycastTarget = true;

        CreateChoiceButton(_choiceRewardPanelRoot, "ProductionChoiceButton", new Vector2(-180f, 0f), false);
        CreateChoiceButton(_choiceRewardPanelRoot, "UnitChoiceButton", new Vector2(180f, 0f), true);

        ResolveChoiceRewardReferences();
        _choiceRewardPanelRoot.gameObject.SetActive(false);
        MarkLayoutDirty();
        return true;
    }

    private void EnsureChoiceRewardChildren()
    {
        if (_choiceRewardPanelRoot == null)
            return;

        if (_productionChoiceButton == null)
        {
            CreateChoiceButton(_choiceRewardPanelRoot, "ProductionChoiceButton", new Vector2(-180f, 0f), false);
            ResolveChoiceRewardReferences();
        }

        if (_unitChoiceButton == null)
        {
            CreateChoiceButton(_choiceRewardPanelRoot, "UnitChoiceButton", new Vector2(180f, 0f), true);
            ResolveChoiceRewardReferences();
        }
    }

    private void ResolveChoiceRewardReferences()
    {
        if (_choiceRewardPanelRoot == null)
            return;

        ResolveChoiceButtonReferences(
            _choiceRewardPanelRoot.Find("ProductionChoiceButton"),
            ref _productionChoiceButton,
            ref _productionChoiceIcon,
            ref _productionChoiceLabel,
            ref _productionChoiceDetail);

        ResolveChoiceButtonReferences(
            _choiceRewardPanelRoot.Find("UnitChoiceButton"),
            ref _unitChoiceButton,
            ref _unitChoiceIcon,
            ref _unitChoiceLabel,
            ref _unitChoiceDetail);

        ApplyChoiceFont(_productionChoiceLabel);
        ApplyChoiceFont(_productionChoiceDetail);
        ApplyChoiceFont(_unitChoiceLabel);
        ApplyChoiceFont(_unitChoiceDetail);
    }

    private static void ResolveChoiceButtonReferences(
        Transform root,
        ref Button button,
        ref Image icon,
        ref TextMeshProUGUI label,
        ref TextMeshProUGUI detail)
    {
        if (root == null)
            return;

        if (button == null)
            button = root.GetComponent<Button>();

        if (icon == null)
        {
            Transform iconTransform = root.Find("Icon");
            if (iconTransform != null)
                icon = iconTransform.GetComponent<Image>();
        }

        if (label == null)
        {
            Transform labelTransform = root.Find("Label");
            if (labelTransform != null)
                label = labelTransform.GetComponent<TextMeshProUGUI>();
        }

        if (detail == null)
        {
            Transform detailTransform = root.Find("Detail");
            if (detailTransform != null)
                detail = detailTransform.GetComponent<TextMeshProUGUI>();
        }
    }

    private void CreateChoiceButton(RectTransform parent, string objectName, Vector2 anchoredPosition, bool hasDetail)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(320f, hasDetail ? 300f : 160f);
        rect.anchoredPosition = anchoredPosition;

        Image image = obj.GetComponent<Image>();
        image.color = new Color(0.16f, 0.16f, 0.16f, 0.96f);

        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.SetParent(rect, false);
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(64f, 64f);
        iconRect.anchoredPosition = new Vector2(0f, hasDetail ? 146f : 28f);

        Image icon = iconObj.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 0f);
        textRect.pivot = new Vector2(0.5f, 0f);
        textRect.offsetMin = new Vector2(16f, hasDetail ? 216f : 20f);
        textRect.offsetMax = new Vector2(-16f, hasDetail ? 270f : 72f);

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = hasDetail ? "쥐 해금" : "생산 시설 블록 추가 획득";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 24f;
        text.color = Color.white;
        text.raycastTarget = false;
        if (_titleFontAsset != null)
            text.font = _titleFontAsset;

        if (hasDetail)
        {
            GameObject detailObj = new GameObject("Detail", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform detailRect = detailObj.GetComponent<RectTransform>();
            detailRect.SetParent(rect, false);
            detailRect.anchorMin = new Vector2(0f, 0f);
            detailRect.anchorMax = new Vector2(1f, 0f);
            detailRect.pivot = new Vector2(0.5f, 0f);
            detailRect.offsetMin = new Vector2(18f, 18f);
            detailRect.offsetMax = new Vector2(-18f, 136f);

            TextMeshProUGUI detailText = detailObj.GetComponent<TextMeshProUGUI>();
            detailText.text = string.Empty;
            detailText.alignment = TextAlignmentOptions.TopLeft;
            detailText.fontSize = 18f;
            detailText.color = Color.white;
            detailText.raycastTarget = false;
            detailText.textWrappingMode = TextWrappingModes.Normal;
            if (_titleFontAsset != null)
                detailText.font = _titleFontAsset;
        }
    }

    private void ApplyChoiceFont(TextMeshProUGUI text)
    {
        if (text == null || _titleFontAsset == null)
            return;

        text.font = _titleFontAsset;
    }

    private static string BuildUnitRewardDescription(UnitDataSO unit)
    {
        if (unit == null)
            return string.Empty;

        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"코스트: {unit.Cost}");
        builder.AppendLine("수용량: 1");

        if (!string.IsNullOrWhiteSpace(unit.Description))
            builder.AppendLine(unit.Description);

        if (unit.CanSupport && unit.Support.Effects != null && unit.Support.Effects.Count > 0)
        {
            for (int i = 0; i < unit.Support.Effects.Count; i++)
            {
                PartSupportEffectData effect = unit.Support.Effects[i];
                if (effect != null)
                    builder.AppendLine(effect.EffectDescription());
            }
        }

        return builder.ToString().TrimEnd();
    }

    private void HideMap()
    {
        if (_mapRoot != null)
            _mapRoot.gameObject.SetActive(false);

        if (_doctrinePanelRoot != null)
            _doctrinePanelRoot.gameObject.SetActive(false);

        if (_choiceRewardPanelRoot != null)
            _choiceRewardPanelRoot.gameObject.SetActive(false);

        SetMapVisible(false);
    }

    public void SetMapVisibleForTutorial(bool isVisible)
    {
        if (isVisible)
        {
            ShowMap();
            return;
        }

        HideMap();
    }

    public void SetDoctrinePanelSuppressed(bool suppressed)
    {
        _suppressDoctrinePanel = suppressed;

        if (_doctrinePanelRoot != null)
        {
            _doctrinePanelRoot.gameObject.SetActive(!suppressed && _isMapVisible);
        }
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

    private Vector2 AnchoredToMap(Vector2 anchored)
    {
        if (_mapSize.x <= 0.01f || _mapSize.y <= 0.01f)
            return new Vector2(0.5f, 0.5f);

        return new Vector2(
            Mathf.Clamp01((anchored.x / _mapSize.x) + 0.5f),
            Mathf.Clamp01((anchored.y / _mapSize.y) + 0.5f));
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

    private RectTransform ResolveOrCreateRect(RectTransform parent, RectTransform current, string childName, out bool created)
    {
        created = false;

        if (current != null)
            return current;

        Transform existing = parent != null ? parent.Find(childName) : null;
        if (existing != null)
            return existing as RectTransform;

        created = true;
        return CreateChild(childName, parent).GetComponent<RectTransform>();
    }

    private TextMeshProUGUI ResolveOrCreateTitle(RectTransform parent, out bool created)
    {
        created = false;

        if (_titleText != null)
            return _titleText;

        Transform existing = parent != null ? parent.Find("StageMapTitle") : null;
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI existingTitle))
            return existingTitle;

        GameObject titleObj = CreateChild("StageMapTitle", parent);
        created = true;
        return titleObj.AddComponent<TextMeshProUGUI>();
    }

    private void ConfigureTitle(TextMeshProUGUI title)
    {
        if (title == null)
            return;

        RectTransform rect = title.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -36f);
        rect.sizeDelta = new Vector2(900f, 72f);

        title.text = "지도에서 다음 스테이지 선택";
        title.alignment = TextAlignmentOptions.Center;
        title.fontSize = _titleFontSize;
        title.color = _titleTextColor;
        title.raycastTarget = false;
        if (_titleFontAsset != null)
            title.font = _titleFontAsset;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        if (target.TryGetComponent(out T component))
            return component;

        return target.AddComponent<T>();
    }

    private void MarkLayoutDirty()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;

        EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
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
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void EnsureRandomizeSettingsInitialized()
    {
        if (_randomizeSettingsInitialized)
            return;

        _randomizePathOnStart = _randomizeOnStart;
        _randomizeSettingsInitialized = true;
    }
}
