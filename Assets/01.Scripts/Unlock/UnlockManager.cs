using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UnlockManager : MonoBehaviour
{
    [System.Serializable]
    public class UnlockUIEntry
    {
        public string unlockId;
        public GameObject lockOverlay;
        public Button targetButton;
        [Tooltip("true면 시작 시점부터 해금 상태로 반영됩니다.")]
        public bool unlockedAtStart;
    }

    [Header("UI Bindings")]
    [SerializeField] private List<UnlockUIEntry> unlockUIEntries = new List<UnlockUIEntry>();

    [Header("Initial State")]
    [SerializeField] private List<string> initiallyUnlockedIds = new List<string>();

    private readonly Dictionary<string, bool> _unlockedStates = new Dictionary<string, bool>();
    private readonly Dictionary<string, List<UnlockUIEntry>> _entriesById = new Dictionary<string, List<UnlockUIEntry>>();

    private void Awake()
    {
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

    public bool IsUnlocked(string unlockId)
    {
        if (string.IsNullOrWhiteSpace(unlockId))
        {
            return false;
        }

        return _unlockedStates.TryGetValue(unlockId, out bool isUnlocked) && isUnlocked;
    }

    public void Unlock(string unlockId)
    {
        if (string.IsNullOrWhiteSpace(unlockId))
        {
            return;
        }

        bool wasUnlocked = IsUnlocked(unlockId);
        _unlockedStates[unlockId] = true;
        RefreshUI(unlockId);

        if (!wasUnlocked)
        {
            Debug.Log($"[UnlockManager] Unlocked: {unlockId}");
        }
    }

    public void RefreshUI()
    {
        for (int i = 0; i < unlockUIEntries.Count; i++)
        {
            ApplyEntryState(unlockUIEntries[i]);
        }
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
        {
            return;
        }

        Unlock(evt.RewardId);
    }

    private void RefreshUI(string unlockId)
    {
        if (!_entriesById.TryGetValue(unlockId, out List<UnlockUIEntry> entries))
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            ApplyEntryState(entries[i]);
        }
    }

    private void ApplyEntryState(UnlockUIEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.unlockId))
        {
            return;
        }

        bool unlocked = IsUnlocked(entry.unlockId);

        if (entry.lockOverlay != null)
        {
            entry.lockOverlay.SetActive(!unlocked);
        }

        if (entry.targetButton != null)
        {
            entry.targetButton.interactable = unlocked;
        }
    }

    private void BuildEntryLookup()
    {
        _entriesById.Clear();

        for (int i = 0; i < unlockUIEntries.Count; i++)
        {
            UnlockUIEntry entry = unlockUIEntries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.unlockId))
            {
                continue;
            }

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
            {
                continue;
            }

            if (!_unlockedStates.ContainsKey(entry.unlockId))
            {
                _unlockedStates.Add(entry.unlockId, false);
            }

            if (entry.unlockedAtStart)
            {
                _unlockedStates[entry.unlockId] = true;
            }
        }

        for (int i = 0; i < initiallyUnlockedIds.Count; i++)
        {
            string unlockId = initiallyUnlockedIds[i];
            if (string.IsNullOrWhiteSpace(unlockId))
            {
                continue;
            }

            _unlockedStates[unlockId] = true;
        }
    }
}
