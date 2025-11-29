using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class QuantityWeight
{
    public int amount = 1;
    [Range(0f, 100f)]
    public float chance = 1;
}

[System.Serializable]
public class LootItem
{
    public GameObject itemPrefab;
    [Range(0f, 100f)]
    public float dropChance;
    public List<QuantityWeight> quantityWeights = new List<QuantityWeight>();
}

[CreateAssetMenu(fileName = "NewRoom", menuName = "Dungeon/Room Data")]
public class RoomData : ScriptableObject
{
    [Header("Identità")]
    public string roomName;
    public GameObject roomPrefab; // Il modello 3D della stanza

    [Header("Dimensioni Griglia")]
    public Vector2Int size = new Vector2Int(1, 1); // 1x1, 2x1, 2x2, ecc.

    [Header("Tipo")]
    public bool isBossRoom;
    public bool isTreasureRoom;
    public bool isStartRoom;
    public bool isShopRoom;
    
    [Header("Rewards")]
    public List<LootItem> rewards = new List<LootItem>();
}