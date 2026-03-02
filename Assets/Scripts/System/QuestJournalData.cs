using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[System.Serializable]
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "InventoryUI")]
public class QuestObjectiveEntryData
{
    public string title;
    public string description;
    public bool completed;
}

[System.Serializable]
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "InventoryUI")]
public class QuestRewardEntryData
{
    public QuestRewardType rewardType = QuestRewardType.Item;
    public string type;
    public int amount = 1;
    public string itemName;
    public WeaponItem weaponAsset;
    public UsableItemData usableAsset;
    public ItemData itemAsset;
    public MagicItemData magicAsset;
    public ArmorItemData armorAsset;
}

[System.Serializable]
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "InventoryUI")]
public class QuestEntryData
{
    public string questId;
    public string title;
    public string location;
    public bool completed;
    public string questTypeLabel = "Main Quest";
    public string recommendedLabel = "";
    public string loreTitle = "";
    [TextArea(2, 6)] public string loreDescription = "";
    public string loreAuthor = "";
    public List<QuestObjectiveEntryData> objectives = new();
    public List<QuestRewardEntryData> rewards = new();
}
