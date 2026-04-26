using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-120)]
public class UnlockManager : MonoBehaviour
{
    public static UnlockManager Instance { get; private set; }

    [Serializable]
    public class UnlockUIEntry
    {
        public string unlockId;
        public GameObject lockOverlay;
        public Button targetButton;
        public Image targetIcon;
        public Color lockedIconColor = Color.black;
        public Color unlockedIconColor = Color.white;
        [Tooltip("When true, this entry starts unlocked.")]
        public bool unlockedAtStart;
    }

    [Serializable]
    private class DoctrineUnitUnlockEntry
    {
        public string unlockId;
        public List<UnitDataSO> unitsToUnlock = new List<UnitDataSO>();
    }

    [Header("UI Bindings")]
    [SerializeField] private List<UnlockUIEntry> unlockUIEntries = new List<UnlockUIEntry>();

    [Header("Initial State")]
    [SerializeField] private List<string> initiallyUnlockedIds = new List<string>();

    [Header("Doctrine Unit Unlocks")]
    [SerializeField] private List<DoctrineUnitUnlockEntry> doctrineUnitUnlocks = new List<DoctrineUnitUnlockEntry>();

    private readonly HashSet<int> _lockedUnitKeys = new HashSet<int>();
    private readonly HashSet<int> _unlockedUnitKeys = new HashSet<int>();
    private readonly HashSet<int> _lockedSkillIndices = new HashSet<int>();
    private readonly HashSet<int> _unlockedSkillIndices = new HashSet<int>();
    private readonly Dictionary<int, List<int>> _skillsByClearStage = new Dictionary<int, List<int>>();

    private readonly Dictionary<string, bool> _unlockedStates = new Dictionary<string, bool>();
    private readonly Dictionary<string, List<UnlockUIEntry>> _entriesById = new Dictionary<string, List<UnlockUIEntry>>();

    public event Action<UnitDataSO> UnitUnlocked;
    public event Action<int> SkillUnlocked;

    private void Awake()
    {
        Instance = this;
        BuildEntryLookup();
        InitializeStates();
        RefreshUI();
    }

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<RatUnlockedEvent>(OnRatUnlocked);
        EventBus.Instance.Subscribe<RitualUnlockedEvent>(OnRitualUnlocked);
        EventBus.Instance.Subscribe<FeatureUnlockedEvent>(OnFeatureUnlocked);
        EventBus.Instance.Subscribe<StageMapRewardAppliedEvent>(OnStageMapRewardApplied);
        RefreshUI();
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<RatUnlockedEvent>(OnRatUnlocked);
        EventBus.Instance.Unsubscribe<RitualUnlockedEvent>(OnRitualUnlocked);
        EventBus.Instance.Unsubscribe<FeatureUnlockedEvent>(OnFeatureUnlocked);
        EventBus.Instance.Unsubscribe<StageMapRewardAppliedEvent>(OnStageMapRewardApplied);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterLockedUnits(IReadOnlyList<UnitDataSO> units)
    {
        _lockedUnitKeys.Clear();
        _unlockedUnitKeys.Clear();

        if (units == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            UnitDataSO unit = units[i];
            if (unit == null)
                continue;

            _lockedUnitKeys.Add(unit.Key);
        }
    }

    public bool RequiresUnlock(UnitDataSO unit)
    {
        return unit != null && _lockedUnitKeys.Contains(unit.Key);
    }

    public bool IsUnitUnlocked(UnitDataSO unit)
    {
        if (unit == null)
            return true;

        return !_lockedUnitKeys.Contains(unit.Key) || _unlockedUnitKeys.Contains(unit.Key);
    }

    public bool UnlockUnit(UnitDataSO unit)
    {
        if (unit == null || !_lockedUnitKeys.Contains(unit.Key))
            return false;

        if (!_unlockedUnitKeys.Add(unit.Key))
            return false;

        Unlock(unit.Key.ToString());
        UnitUnlocked?.Invoke(unit);
        return true;
    }

    public void RegisterSkillUnlocks(IReadOnlyList<StageSkillUnlockData> skillUnlocks)
    {
        _lockedSkillIndices.Clear();
        _unlockedSkillIndices.Clear();
        _skillsByClearStage.Clear();

        if (skillUnlocks == null)
            return;

        for (int i = 0; i < skillUnlocks.Count; i++)
        {
            StageSkillUnlockData unlock = skillUnlocks[i];
            if (unlock == null || unlock.SkillIndex <= 0)
                continue;

            _lockedSkillIndices.Add(unlock.SkillIndex);

            if (!_skillsByClearStage.TryGetValue(unlock.ClearStageIndex, out List<int> skills))
            {
                skills = new List<int>();
                _skillsByClearStage.Add(unlock.ClearStageIndex, skills);
            }

            if (!skills.Contains(unlock.SkillIndex))
                skills.Add(unlock.SkillIndex);
        }
    }

    public bool RequiresSkillUnlock(int skillIndex)
    {
        return _lockedSkillIndices.Contains(skillIndex);
    }

    public bool IsSkillUnlocked(int skillIndex)
    {
        return !_lockedSkillIndices.Contains(skillIndex) || _unlockedSkillIndices.Contains(skillIndex);
    }

    public void UnlockSkillsForClearedStage(int stageIndex)
    {
        if (!_skillsByClearStage.TryGetValue(stageIndex, out List<int> skills))
            return;

        for (int i = 0; i < skills.Count; i++)
            UnlockSkill(skills[i]);
    }

    public bool UnlockSkill(int skillIndex)
    {
        if (!_lockedSkillIndices.Contains(skillIndex))
            return false;

        if (!_unlockedSkillIndices.Add(skillIndex))
            return false;

        SkillUnlocked?.Invoke(skillIndex);
        return true;
    }

    public bool IsUnlocked(string unlockId)
    {
        if (string.IsNullOrWhiteSpace(unlockId))
            return false;

        return _unlockedStates.TryGetValue(unlockId, out bool isUnlocked) && isUnlocked;
    }

    public void Unlock(string unlockId)
    {
        if (string.IsNullOrWhiteSpace(unlockId))
            return;

        bool wasUnlocked = IsUnlocked(unlockId);
        _unlockedStates[unlockId] = true;
        RefreshUI(unlockId);
        TryApplyDoctrineUnitUnlocks(unlockId);

        if (!wasUnlocked)
            Debug.Log($"[UnlockManager] Unlocked: {unlockId}");
    }

    public void ResetDoctrineUnlocksFromEffectApplier(DoctrineEffectApplier effectApplier)
    {
        if (effectApplier == null)
        {
            Debug.LogWarning("[UnlockManager] DoctrineEffectApplier is null. Reset skipped.");
            return;
        }

        var doctrineUnlockIds = new HashSet<string>();
        effectApplier.CollectUnlockIdsForAppliedEffects(doctrineUnlockIds);
        foreach (string unlockId in doctrineUnlockIds)
        {
            if (string.IsNullOrWhiteSpace(unlockId))
                continue;

            _unlockedStates[unlockId] = false;
            RefreshUI(unlockId);
        }

        effectApplier.RepublishUnlockEventsForAppliedEffects();
    }

    [ContextMenu("Unlock/Rebuild Doctrine Unlocks From EffectApplier")]
    private void RebuildDoctrineUnlocksFromEffectApplierContextMenu()
    {
        DoctrineEffectApplier effectApplier = FindFirstObjectByType<DoctrineEffectApplier>(FindObjectsInactive.Include);
        ResetDoctrineUnlocksFromEffectApplier(effectApplier);
    }

    public void RefreshUI()
    {
        for (int i = 0; i < unlockUIEntries.Count; i++)
            ApplyEntryState(unlockUIEntries[i]);
    }

    private void OnRatUnlocked(RatUnlockedEvent evt)
    {
        Unlock(evt.RatId);
    }

    private void OnRitualUnlocked(RitualUnlockedEvent evt)
    {
        Unlock(evt.RitualId);
    }

    private void OnFeatureUnlocked(FeatureUnlockedEvent evt)
    {
        Unlock(evt.UnlockId);
    }

    private void OnStageMapRewardApplied(StageMapRewardAppliedEvent evt)
    {
        if (evt.RewardType != StageMapRewardType.RatTowerUnlock || string.IsNullOrWhiteSpace(evt.RewardId))
            return;

        Unlock(evt.RewardId);
    }

    private void RefreshUI(string unlockId)
    {
        if (!_entriesById.TryGetValue(unlockId, out List<UnlockUIEntry> entries))
            return;

        for (int i = 0; i < entries.Count; i++)
            ApplyEntryState(entries[i]);
    }

    private void ApplyEntryState(UnlockUIEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.unlockId))
            return;

        bool unlocked = IsUnlocked(entry.unlockId);

        if (entry.lockOverlay != null)
            entry.lockOverlay.SetActive(!unlocked);

        if (entry.targetButton != null)
            entry.targetButton.interactable = unlocked;

        Image icon = ResolveEntryIcon(entry);
        if (icon != null)
            icon.color = unlocked ? entry.unlockedIconColor : entry.lockedIconColor;
    }

    private static Image ResolveEntryIcon(UnlockUIEntry entry)
    {
        if (entry == null)
            return null;

        if (entry.targetIcon != null)
            return entry.targetIcon;

        if (entry.targetButton == null)
            return null;

        Transform iconTransform = entry.targetButton.transform.Find("Icon");
        if (iconTransform == null)
            return null;

        iconTransform.TryGetComponent(out entry.targetIcon);
        return entry.targetIcon;
    }

    private void BuildEntryLookup()
    {
        _entriesById.Clear();

        for (int i = 0; i < unlockUIEntries.Count; i++)
        {
            UnlockUIEntry entry = unlockUIEntries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.unlockId))
                continue;

            if (!_entriesById.TryGetValue(entry.unlockId, out List<UnlockUIEntry> entries))
            {
                entries = new List<UnlockUIEntry>();
                _entriesById.Add(entry.unlockId, entries);
            }

            entries.Add(entry);
        }
    }

    private void InitializeStates()
    {
        _unlockedStates.Clear();

        for (int i = 0; i < unlockUIEntries.Count; i++)
        {
            UnlockUIEntry entry = unlockUIEntries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.unlockId))
                continue;

            if (!_unlockedStates.ContainsKey(entry.unlockId))
                _unlockedStates.Add(entry.unlockId, false);

            if (entry.unlockedAtStart)
                _unlockedStates[entry.unlockId] = true;
        }

        for (int i = 0; i < initiallyUnlockedIds.Count; i++)
        {
            string unlockId = initiallyUnlockedIds[i];
            if (string.IsNullOrWhiteSpace(unlockId))
                continue;

            _unlockedStates[unlockId] = true;
        }
    }

    private void TryApplyDoctrineUnitUnlocks(string unlockId)
    {
        if (string.IsNullOrWhiteSpace(unlockId) || doctrineUnitUnlocks == null || doctrineUnitUnlocks.Count == 0)
            return;

        for (int i = 0; i < doctrineUnitUnlocks.Count; i++)
        {
            DoctrineUnitUnlockEntry entry = doctrineUnitUnlocks[i];
            if (entry == null || !string.Equals(entry.unlockId, unlockId, StringComparison.Ordinal))
                continue;

            if (entry.unitsToUnlock == null)
                continue;

            for (int j = 0; j < entry.unitsToUnlock.Count; j++)
            {
                UnitDataSO unit = entry.unitsToUnlock[j];
                if (unit != null)
                    UnlockUnit(unit);
            }
        }
    }
}
