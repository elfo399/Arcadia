[System.Serializable]
public class SavedQuestObjectiveData
{
    public string title;
    public string description;
    public bool completed;
}

[System.Serializable]
public class SavedQuestRewardData
{
    public string type;
    public int amount;
    public string itemName;
    public string iconName;
}

[System.Serializable]
public class SavedQuestData
{
    public string questId;
    public string title;
    public string location;
    public bool completed;
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
    // weapon | usable | item
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
    public SavedLoadoutSlotData[] usableLoadout;
    public int currentRightIndex;
    public int currentLeftIndex;
    public int currentUsableIndex;
}

[System.Serializable]
public class GameData
{
    public int karma;
    public int benedetto;
    public int malefico;

    // Banked currency (persistent across runs)
    public int bankGold;
    public int bankSilver;
    public int bankCopper;

    // Quest state (completo) per ricostruire il Journal identico al salvataggio
    public SavedQuestData[] quests;
    // Inventory + equipment/loadout
    public SavedPlayerInventoryData playerInventory;

    // Aggiungi qui altre statistiche o dati da salvare in futuro
}
