using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class QuantityWeight
{
    public int amount = 1;      // Quanti ne cadono? (es. 3 monete)
    [Range(0f, 100f)]
    public float chance = 10;   // Peso probabilistico (più è alto, più è probabile)
}

[System.Serializable]
public class LootItem
{
    public string name;         // Solo per ordine nell'Inspector
    public GameObject itemPrefab;
    [Range(0f, 100f)]
    public float dropChance;    // Probabilità globale che questo tipo di oggetto appaia (es. 50%)
    public List<QuantityWeight> quantityWeights = new List<QuantityWeight>();
}

[CreateAssetMenu(fileName = "NewRoom", menuName = "Dungeon/Room Data")]
public class RoomData : ScriptableObject
{
    [Header("Identità")]
    [Tooltip("Immutable authoring ID used by deterministic generated-room identities.")]
    public string stableId;
    public string roomName;
    public GameObject roomPrefab; 

    [Header("Dimensioni Griglia")]
    public Vector2Int size = new Vector2Int(1, 1); 

    [Header("Tipo")]
    public bool isBossRoom;
    public bool isTreasureRoom;
    public bool isStartRoom;
    public bool isShopRoom;
    public bool isBlessedRoom;
    public bool isEvilRoom;
    
    [Header("Rewards / Loot Table")]
    public List<LootItem> rewards = new List<LootItem>();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(stableId))
        {
            stableId = "roomdef-" + System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
