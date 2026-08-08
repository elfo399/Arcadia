[System.Serializable]
public class SavedQuestObjectiveData
{
    public int phase;
    public string title;
    public string description;
    public string eventType;
    public string targetId;
    public string targetTag;
    public int requiredAmount;
    public int currentAmount;
    public bool completed;
}

[System.Serializable]
public class SavedQuestRewardData
{
    public string type;
    public int amount;
    public string itemName;
}

[System.Serializable]
public class SavedQuestData
{
    public string questId;
    public string title;
    public string location;
    public bool completed;
    public bool rewardClaimed;
    public string questTypeLabel;
    public string recommendedLabel;
    public string loreTitle;
    public string loreDescription;
    public string loreAuthor;
    public SavedQuestObjectiveData[] objectives;
    public SavedQuestRewardData[] rewards;
}

[System.Serializable]
public class SavedInventoryItemData
{
    // weapon | magic | armor | usable | item
    public string itemType;
    // Nome asset ScriptableObject (es. Sword)
    public string assetName;
    // Nome display (fallback)
    public string itemName;
    public string instanceId;
    public int amount;
    public string title;
    public string description;
}

[System.Serializable]
public class SavedLoadoutSlotData
{
    public string assetName;
    public string instanceId;
}

[System.Serializable]
public class SavedPlayerInventoryData
{
    public SavedInventoryItemData[] items;
    public SavedLoadoutSlotData[] rightLoadout;
    public SavedLoadoutSlotData[] leftLoadout;
    public SavedLoadoutSlotData[] magicLoadout;
    public SavedLoadoutSlotData[] usableLoadout;
    public SavedLoadoutSlotData[] armorLoadout;
    public int currentRightIndex;
    public int currentLeftIndex;
    public int currentMagicIndex;
    public int currentUsableIndex;
}

[System.Serializable]
public class SavedDialogueHistoryData
{
    public string[] readNodeKeys;
    public string[] selectedChoiceKeys;
}

[System.Serializable]
public class GameData
{
    // Incrementato quando cambia la struttura persistente del salvataggio.
    public int saveVersion;

    // Single player identity. The starting class is applied once, then runtime
    // stats/inventory become authoritative.
    public string playerId;
    public string playerName;
    public string selectedClassId;
    public bool startingClassApplied;

    // Leveling
    public int playerLevel;
    public int levelExperience;
    public int experienceToNextLevel;
    public int unspentAttributePoints;

    // Player attributes
    public int vigor;
    public int mind;
    public int endurance;
    public int strength;
    public int dexterity;
    public int intelligence;
    public int faith;

    public int karma;
    public int benedetto;
    public int malefico;

    // Banked currency (persistent across runs)
    public bool usesUnifiedCoins;
    public int bankCoins;
    public int runCoins;

    // Checkpoint della run: permette di ricostruire lo stesso piano dal suo ingresso.
    public bool dungeonCheckpointActive;
    public int dungeonFloor;
    public string dungeonSeed;

    // Legacy currency fields, kept only in memory to migrate older saves.
    [System.NonSerialized]
    public int bankGold;
    [System.NonSerialized]
    public int bankSilver;
    [System.NonSerialized]
    public int bankCopper;

    // Quest state (completo) per ricostruire il Journal identico al salvataggio
    public SavedQuestData[] quests;
    // Inventory + equipment/loadout
    public SavedPlayerInventoryData playerInventory;

    // Narrative state. Arrays remain JsonUtility-compatible; runtime systems
    // may use sets and explicitly import/export them.
    public string[] storyFlags;
    public SavedDialogueHistoryData dialogueHistory;

    // Aggiungi qui altre statistiche o dati da salvare in futuro
}
