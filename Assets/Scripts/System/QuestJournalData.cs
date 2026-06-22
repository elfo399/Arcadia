using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[System.Serializable]
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "InventoryUI")]
public class QuestObjectiveEntryData
{
    public string title;
    public string description;
    public QuestObjectiveEventType eventType = QuestObjectiveEventType.None;
    public UnityEngine.Object targetObject;
    public string targetId;
    public string targetTag;
    [Min(1)] public int requiredAmount = 1;
    [Min(0)] public int currentAmount = 0;
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
    public bool rewardClaimed;
    public string questTypeLabel = "Main Quest";
    public string recommendedLabel = "";
    public Sprite questImage;
    public string loreTitle = "";
    [TextArea(2, 6)] public string loreDescription = "";
    public string loreAuthor = "";
    public List<QuestObjectiveEntryData> objectives = new();
    public List<QuestRewardEntryData> rewards = new();
}
