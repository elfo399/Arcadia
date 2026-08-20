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
public class RoomData : ScriptableObject, ISerializationCallbackReceiver
{
    [Header("Identità")]
    [Tooltip("Immutable authoring ID used by deterministic generated-room identities.")]
    public string stableId;
    public string roomName;
    public GameObject roomPrefab; 
    [Min(1)] [Tooltip("Relative deterministic selection weight inside a matching room pool.")]
    public int generationWeight = 1;

    [Header("Dimensioni Griglia")]
    public Vector2Int size = new Vector2Int(1, 1); 

    [Header("Structural type")]
    public RoomType roomType = RoomType.Normal;
    
    [Header("Rewards / Loot Table")]
    public List<LootItem> rewards = new List<LootItem>();

    public void OnBeforeSerialize()
    {
        roomType = RoomTypeMigration.Normalize(roomType);
    }

    public void OnAfterDeserialize()
    {
        roomType = RoomTypeMigration.Normalize(roomType);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        roomType = RoomTypeMigration.Normalize(roomType);
        if (string.IsNullOrWhiteSpace(stableId))
        {
            stableId = "roomdef-" + System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
