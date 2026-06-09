using System.Collections.Generic;
using UnityEngine;

public partial class QuestManager
{
    public enum JournalPadSection { List, Detail }
    private enum QuestRewardKind { Item, Weapon, Usable, Magic, Armor, Experience }

    private JournalPadSection currentJournalPadSection = JournalPadSection.List;
    private int journalPadListIndex;
    private string selectedJournalQuestId;

    private Dictionary<string, WeaponItem> questRewardWeaponLookup;
    private Dictionary<string, UsableItemData> questRewardUsableLookup;
    private Dictionary<string, ItemData> questRewardItemLookup;
    private Dictionary<string, MagicItemData> questRewardMagicLookup;
    private Dictionary<string, ArmorItemData> questRewardArmorLookup;

    public JournalPadSection CurrentJournalPadSection => currentJournalPadSection;
    public int JournalPadListIndex => journalPadListIndex;
    public string SelectedJournalQuestId => selectedJournalQuestId;

    public void FocusJournalPadDefault()
    {
        currentJournalPadSection = JournalPadSection.List;
        SyncJournalPadListIndexToSelection();
    }

    public void MoveJournalPadFocusHorizontal(int direction)
    {
        if (currentJournalPadSection == JournalPadSection.Detail)
            return;

        int dir = direction >= 0 ? 1 : -1;
        if (dir > 0 && HasVisibleJournalQuests())
            currentJournalPadSection = JournalPadSection.List;
    }

    public void MoveJournalPadFocusVertical(int direction)
    {
        int dir = direction >= 0 ? 1 : -1;
        if (currentJournalPadSection != JournalPadSection.List)
            return;

        int count = GetVisibleJournalQuestCount();
        if (count > 0)
            journalPadListIndex = (journalPadListIndex + dir + count) % count;
    }

    public bool ConfirmJournalSelection(PlayerInventory inventory, PlayerStats stats, int normalCapacity, int magicCapacity)
    {
        if (currentJournalPadSection == JournalPadSection.List)
        {
            string questId = GetVisibleJournalQuestIdAt(journalPadListIndex);
            if (string.IsNullOrWhiteSpace(questId))
                return false;

            SelectJournalQuest(questId);
            currentJournalPadSection = JournalPadSection.Detail;
            return false;
        }

        if (currentJournalPadSection == JournalPadSection.Detail)
            return TryClaimSelectedQuestRewards(inventory, stats, normalCapacity, magicCapacity);

        return false;
    }

    public bool HandleJournalBack()
    {
        if (currentJournalPadSection == JournalPadSection.Detail)
        {
            currentJournalPadSection = JournalPadSection.List;
            return true;
        }

        return false;
    }

    public List<QuestEntryData> GetVisibleJournalQuestEntriesSnapshot()
    {
        var all = GetQuestEntriesSnapshot();
        var result = new List<QuestEntryData>();
        for (int i = 0; i < all.Count; i++)
        {
            var quest = all[i];
            if (quest == null)
                continue;
            result.Add(quest);
        }

        return result;
    }

    public QuestEntryData GetSelectedVisibleJournalQuest()
    {
        if (string.IsNullOrWhiteSpace(selectedJournalQuestId))
            return null;

        var visible = GetVisibleJournalQuestEntriesSnapshot();
        for (int i = 0; i < visible.Count; i++)
        {
            var quest = visible[i];
            if (quest == null)
                continue;
            if (string.Equals(quest.questId, selectedJournalQuestId, System.StringComparison.OrdinalIgnoreCase))
                return quest;
        }

        return null;
    }

    public bool IsJournalQuestSelected(string questId)
    {
        return !string.IsNullOrWhiteSpace(selectedJournalQuestId)
               && !string.IsNullOrWhiteSpace(questId)
               && string.Equals(selectedJournalQuestId, questId, System.StringComparison.OrdinalIgnoreCase);
    }

    public void SelectJournalQuest(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return;

        selectedJournalQuestId = questId.Trim();
        SyncJournalPadListIndexToSelection();
    }

    public int GetVisibleJournalQuestCount()
    {
        return GetVisibleJournalQuestEntriesSnapshot().Count;
    }

    public bool HasVisibleJournalQuests()
    {
        return GetVisibleJournalQuestCount() > 0;
    }

    public string GetVisibleJournalQuestIdAt(int index)
    {
        if (index < 0)
            return null;

        var visible = GetVisibleJournalQuestEntriesSnapshot();
        if (index >= visible.Count)
            return null;

        return visible[index]?.questId;
    }

    public bool IsQuestReadyToClaim(QuestEntryData quest)
    {
        if (quest == null)
            return false;

        if (quest.objectives == null || quest.objectives.Count == 0)
            return quest.completed;

        for (int i = 0; i < quest.objectives.Count; i++)
        {
            var objective = quest.objectives[i];
            if (objective == null)
                continue;
            if (!objective.completed)
                return false;
        }

        return true;
    }

    public bool CanClaimSelectedQuestRewards(PlayerInventory inventory, PlayerStats stats, int normalCapacity, int magicCapacity)
    {
        return CanClaimQuestRewards(GetSelectedVisibleJournalQuest(), inventory, stats, normalCapacity, magicCapacity);
    }

    public bool TryClaimSelectedQuestRewards(PlayerInventory inventory, PlayerStats stats, int normalCapacity, int magicCapacity)
    {
        var quest = GetSelectedVisibleJournalQuest();
        if (quest == null)
            return false;
        if (!IsQuestReadyToClaim(quest) || quest.rewards == null || quest.rewards.Count == 0)
            return false;
        if (!CanClaimQuestRewards(quest, inventory, stats, normalCapacity, magicCapacity))
            return false;
        if (!TryApplyQuestRewards(quest, inventory, stats))
            return false;

        ConsumeQuestRewards(quest.questId);
        return true;
    }

    private void ClampJournalPadIndices()
    {
        int visibleCount = GetVisibleJournalQuestCount();
        if (visibleCount <= 0)
        {
            journalPadListIndex = 0;
            currentJournalPadSection = JournalPadSection.List;
            return;
        }

        journalPadListIndex = Mathf.Clamp(journalPadListIndex, 0, visibleCount - 1);
        if (currentJournalPadSection == JournalPadSection.Detail)
        {
            string currentVisibleId = GetVisibleJournalQuestIdAt(journalPadListIndex);
            if (string.IsNullOrWhiteSpace(selectedJournalQuestId)
                || !string.Equals(selectedJournalQuestId, currentVisibleId, System.StringComparison.OrdinalIgnoreCase))
            {
                currentJournalPadSection = JournalPadSection.List;
            }
        }
    }

    private void SyncJournalPadListIndexToSelection()
    {
        if (string.IsNullOrWhiteSpace(selectedJournalQuestId))
        {
            ClampJournalPadIndices();
            return;
        }

        var visible = GetVisibleJournalQuestEntriesSnapshot();
        for (int i = 0; i < visible.Count; i++)
        {
            var quest = visible[i];
            if (quest == null)
                continue;
            if (!string.Equals(quest.questId, selectedJournalQuestId, System.StringComparison.OrdinalIgnoreCase))
                continue;

            journalPadListIndex = i;
            ClampJournalPadIndices();
            return;
        }

        ClampJournalPadIndices();
    }

    private bool CanClaimQuestRewards(QuestEntryData quest, PlayerInventory inventory, PlayerStats stats, int normalCapacity, int magicCapacity)
    {
        if (quest == null || inventory == null)
            return false;

        CountInventoryUsage(inventory.Items, out int normalUsed, out int magicUsed);

        int normalAdditional = 0;
        int magicAdditional = 0;
        var normalStacks = new HashSet<string>();
        var magicStacks = new HashSet<string>();

        if (quest.rewards == null)
            return false;

        for (int i = 0; i < quest.rewards.Count; i++)
        {
            var reward = quest.rewards[i];
            if (reward == null)
                continue;

            int amount = Mathf.Max(1, reward.amount);
            if (!TryResolveQuestRewardType(reward, out var kind))
                return false;

            switch (kind)
            {
                case QuestRewardKind.Experience:
                    continue;
                case QuestRewardKind.Weapon:
                    if (!TryResolveWeaponReward(reward, out _)) return false;
                    normalAdditional += amount;
                    break;
                case QuestRewardKind.Armor:
                    if (!TryResolveArmorReward(reward, out _)) return false;
                    normalAdditional += amount;
                    break;
                case QuestRewardKind.Usable:
                    if (!TryResolveUsableReward(reward, out var usable)) return false;
                    if (!WouldStackUsable(inventory, usable, normalStacks))
                        normalAdditional += 1;
                    break;
                case QuestRewardKind.Item:
                    if (!TryResolveItemReward(reward, out var item)) return false;
                    if (!WouldStackItem(inventory, item, normalStacks))
                        normalAdditional += 1;
                    break;
                case QuestRewardKind.Magic:
                    if (!TryResolveMagicReward(reward, out var magic)) return false;
                    if (!WouldStackMagic(inventory, magic, magicStacks))
                        magicAdditional += 1;
                    break;
            }
        }

        bool normalOk = normalCapacity <= 0 || (normalUsed + normalAdditional) <= normalCapacity;
        bool magicOk = magicCapacity <= 0 || (magicUsed + magicAdditional) <= magicCapacity;
        return normalOk && magicOk;
    }

    private bool TryApplyQuestRewards(QuestEntryData quest, PlayerInventory inventory, PlayerStats stats)
    {
        if (quest == null || inventory == null)
            return false;

        for (int i = 0; i < quest.rewards.Count; i++)
        {
            var reward = quest.rewards[i];
            if (reward == null)
                continue;

            int amount = Mathf.Max(1, reward.amount);
            if (!TryResolveQuestRewardType(reward, out var kind))
                return false;

            switch (kind)
            {
                case QuestRewardKind.Experience:
                    if (stats == null) return false;
                    stats.AddExperience(amount);
                    break;
                case QuestRewardKind.Weapon:
                    if (!TryResolveWeaponReward(reward, out var weapon)) return false;
                    for (int n = 0; n < amount; n++)
                        inventory.AddItem(new InventoryItem(weapon, 1));
                    break;
                case QuestRewardKind.Armor:
                    if (!TryResolveArmorReward(reward, out var armor)) return false;
                    for (int n = 0; n < amount; n++)
                        inventory.AddItem(new InventoryItem(armor, 1));
                    break;
                case QuestRewardKind.Usable:
                    if (!TryResolveUsableReward(reward, out var usable)) return false;
                    AddOrStackUsableReward(inventory, usable, amount);
                    break;
                case QuestRewardKind.Item:
                    if (!TryResolveItemReward(reward, out var item)) return false;
                    AddOrStackItemReward(inventory, item, amount);
                    break;
                case QuestRewardKind.Magic:
                    if (!TryResolveMagicReward(reward, out var magic)) return false;
                    AddOrStackMagicReward(inventory, magic, amount);
                    break;
            }
        }

        return true;
    }

    private void ConsumeQuestRewards(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return;

        string normalized = questId.Trim();
        bool changed = false;
        for (int i = 0; i < quests.Count; i++)
        {
            var quest = quests[i];
            if (quest == null)
                continue;
            if (!string.Equals(quest.questId, normalized, System.StringComparison.OrdinalIgnoreCase))
                continue;

            quest.completed = true;
            if (quest.rewards != null && quest.rewards.Count > 0)
                quest.rewards.Clear();
            changed = true;
            break;
        }

        if (!changed)
            return;

        ClampJournalPadIndices();
        NotifyChanged();
    }

    private static bool IsMagicInventoryItem(InventoryItem item)
    {
        return item != null && item.magicData != null;
    }

    private static void CountInventoryUsage(IReadOnlyList<InventoryItem> source, out int normalUsed, out int magicUsed)
    {
        normalUsed = 0;
        magicUsed = 0;
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            var item = source[i];
            if (item == null)
                continue;
            if (IsMagicInventoryItem(item))
                magicUsed++;
            else
                normalUsed++;
        }
    }

    private static bool WouldStackItem(PlayerInventory inventory, ItemData item, HashSet<string> plannedStacks)
    {
        if (item == null || inventory == null)
            return false;

        string key = NormalizeLookupKey(item.name);
        if (string.IsNullOrEmpty(key))
            return false;

        if (plannedStacks.Contains(key))
            return true;

        var items = inventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var existing = items[i];
            if (existing == null || existing.itemData != item)
                continue;
            plannedStacks.Add(key);
            return true;
        }

        return false;
    }

    private static bool WouldStackUsable(PlayerInventory inventory, UsableItemData usable, HashSet<string> plannedStacks)
    {
        if (usable == null || inventory == null)
            return false;

        string key = NormalizeLookupKey(usable.name);
        if (string.IsNullOrEmpty(key))
            return false;

        if (plannedStacks.Contains(key))
            return true;

        var items = inventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var existing = items[i];
            if (existing == null || existing.usableData != usable)
                continue;
            plannedStacks.Add(key);
            return true;
        }

        return false;
    }

    private static bool WouldStackMagic(PlayerInventory inventory, MagicItemData magic, HashSet<string> plannedStacks)
    {
        if (magic == null || inventory == null)
            return false;

        string key = NormalizeLookupKey(magic.name);
        if (string.IsNullOrEmpty(key))
            return false;

        if (plannedStacks.Contains(key))
            return true;

        var items = inventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var existing = items[i];
            if (existing == null || existing.magicData != magic)
                continue;
            plannedStacks.Add(key);
            return true;
        }

        return false;
    }

    private static void AddOrStackItemReward(PlayerInventory inventory, ItemData item, int amount)
    {
        if (inventory == null || item == null || amount <= 0)
            return;

        var items = inventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var existing = items[i];
            if (existing == null || existing.itemData != item)
                continue;
            existing.amount += amount;
            return;
        }

        inventory.AddItem(new InventoryItem(item, amount));
    }

    private static void AddOrStackUsableReward(PlayerInventory inventory, UsableItemData usable, int amount)
    {
        if (inventory == null || usable == null || amount <= 0)
            return;

        var items = inventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var existing = items[i];
            if (existing == null || existing.usableData != usable)
                continue;
            existing.amount += amount;
            return;
        }

        inventory.AddItem(new InventoryItem(usable, amount));
    }

    private static void AddOrStackMagicReward(PlayerInventory inventory, MagicItemData magic, int amount)
    {
        if (inventory == null || magic == null || amount <= 0)
            return;

        var items = inventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var existing = items[i];
            if (existing == null || existing.magicData != magic)
                continue;
            existing.amount += amount;
            return;
        }

        inventory.AddItem(new InventoryItem(magic, amount));
    }

    private static bool TryResolveQuestRewardType(QuestRewardEntryData reward, out QuestRewardKind kind)
    {
        kind = QuestRewardKind.Item;
        if (reward == null)
            return false;

        switch (reward.rewardType)
        {
            case QuestRewardType.Weapon: kind = QuestRewardKind.Weapon; return true;
            case QuestRewardType.Usable: kind = QuestRewardKind.Usable; return true;
            case QuestRewardType.Magic: kind = QuestRewardKind.Magic; return true;
            case QuestRewardType.Armor: kind = QuestRewardKind.Armor; return true;
            case QuestRewardType.Experience: kind = QuestRewardKind.Experience; return true;
            case QuestRewardType.Item: kind = QuestRewardKind.Item; break;
        }

        string raw = string.IsNullOrWhiteSpace(reward.type) ? string.Empty : reward.type.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(raw))
            return false;

        if (raw.Contains("weapon")) { kind = QuestRewardKind.Weapon; return true; }
        if (raw.Contains("usable") || raw.Contains("consumable") || raw.Contains("potion")) { kind = QuestRewardKind.Usable; return true; }
        if (raw.Contains("magic") || raw.Contains("spell") || raw.Contains("magia")) { kind = QuestRewardKind.Magic; return true; }
        if (raw.Contains("armor") || raw.Contains("helmet") || raw.Contains("chestplate") || raw.Contains("leggings") || raw.Contains("boots")) { kind = QuestRewardKind.Armor; return true; }
        if (raw.Contains("experience") || raw == "xp" || raw.Contains("exp") || raw.Contains("esperienza")) { kind = QuestRewardKind.Experience; return true; }
        if (raw.Contains("item")) { kind = QuestRewardKind.Item; return true; }

        return false;
    }

    private bool TryResolveWeaponReward(QuestRewardEntryData reward, out WeaponItem weapon)
    {
        EnsureQuestRewardLookups();
        weapon = null;
        if (reward != null && reward.weaponAsset != null)
        {
            weapon = reward.weaponAsset;
            return true;
        }
        return reward != null && questRewardWeaponLookup != null && TryLookupByRewardName(reward, questRewardWeaponLookup, out weapon);
    }

    private bool TryResolveUsableReward(QuestRewardEntryData reward, out UsableItemData usable)
    {
        EnsureQuestRewardLookups();
        usable = null;
        if (reward != null && reward.usableAsset != null)
        {
            usable = reward.usableAsset;
            return true;
        }
        return reward != null && questRewardUsableLookup != null && TryLookupByRewardName(reward, questRewardUsableLookup, out usable);
    }

    private bool TryResolveItemReward(QuestRewardEntryData reward, out ItemData item)
    {
        EnsureQuestRewardLookups();
        item = null;
        if (reward != null && reward.itemAsset != null)
        {
            item = reward.itemAsset;
            return true;
        }
        return reward != null && questRewardItemLookup != null && TryLookupByRewardName(reward, questRewardItemLookup, out item);
    }

    private bool TryResolveMagicReward(QuestRewardEntryData reward, out MagicItemData magic)
    {
        EnsureQuestRewardLookups();
        magic = null;
        if (reward != null && reward.magicAsset != null)
        {
            magic = reward.magicAsset;
            return true;
        }
        return reward != null && questRewardMagicLookup != null && TryLookupByRewardName(reward, questRewardMagicLookup, out magic);
    }

    private bool TryResolveArmorReward(QuestRewardEntryData reward, out ArmorItemData armor)
    {
        EnsureQuestRewardLookups();
        armor = null;
        if (reward != null && reward.armorAsset != null)
        {
            armor = reward.armorAsset;
            return true;
        }
        return reward != null && questRewardArmorLookup != null && TryLookupByRewardName(reward, questRewardArmorLookup, out armor);
    }

    private void EnsureQuestRewardLookups()
    {
        if (questRewardWeaponLookup != null
            && questRewardUsableLookup != null
            && questRewardItemLookup != null
            && questRewardMagicLookup != null
            && questRewardArmorLookup != null)
            return;

        questRewardWeaponLookup = new Dictionary<string, WeaponItem>();
        questRewardUsableLookup = new Dictionary<string, UsableItemData>();
        questRewardItemLookup = new Dictionary<string, ItemData>();
        questRewardMagicLookup = new Dictionary<string, MagicItemData>();
        questRewardArmorLookup = new Dictionary<string, ArmorItemData>();

        RegisterAssets(questRewardWeaponLookup, Resources.LoadAll<WeaponItem>(""), x => x != null ? x.weaponName : null);
        RegisterAssets(questRewardUsableLookup, Resources.LoadAll<UsableItemData>(""), x => x != null ? x.itemName : null);
        RegisterAssets(questRewardItemLookup, Resources.LoadAll<ItemData>(""), x => x != null ? x.itemName : null);
        RegisterAssets(questRewardMagicLookup, Resources.LoadAll<MagicItemData>(""), x => x != null ? x.magicName : null);
        RegisterAssets(questRewardArmorLookup, Resources.LoadAll<ArmorItemData>(""), x => x != null ? x.itemName : null);

        RegisterAssets(questRewardWeaponLookup, Resources.FindObjectsOfTypeAll<WeaponItem>(), x => x != null ? x.weaponName : null);
        RegisterAssets(questRewardUsableLookup, Resources.FindObjectsOfTypeAll<UsableItemData>(), x => x != null ? x.itemName : null);
        RegisterAssets(questRewardItemLookup, Resources.FindObjectsOfTypeAll<ItemData>(), x => x != null ? x.itemName : null);
        RegisterAssets(questRewardMagicLookup, Resources.FindObjectsOfTypeAll<MagicItemData>(), x => x != null ? x.magicName : null);
        RegisterAssets(questRewardArmorLookup, Resources.FindObjectsOfTypeAll<ArmorItemData>(), x => x != null ? x.itemName : null);
    }

    private static void RegisterAssets<T>(Dictionary<string, T> lookup, T[] source, System.Func<T, string> displayNameResolver) where T : Object
    {
        if (lookup == null || source == null)
            return;

        for (int i = 0; i < source.Length; i++)
        {
            T asset = source[i];
            if (asset == null)
                continue;

            string assetName = NormalizeLookupKey(asset.name);
            if (!string.IsNullOrEmpty(assetName) && !lookup.ContainsKey(assetName))
                lookup.Add(assetName, asset);

            string displayName = NormalizeLookupKey(displayNameResolver != null ? displayNameResolver(asset) : null);
            if (!string.IsNullOrEmpty(displayName) && !lookup.ContainsKey(displayName))
                lookup.Add(displayName, asset);
        }
    }

    private static bool TryLookupByRewardName<T>(QuestRewardEntryData reward, Dictionary<string, T> lookup, out T resolved) where T : Object
    {
        resolved = null;
        if (reward == null || lookup == null)
            return false;

        string key = NormalizeLookupKey(reward.itemName);
        return !string.IsNullOrEmpty(key) && lookup.TryGetValue(key, out resolved);
    }

    private static string NormalizeLookupKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}

