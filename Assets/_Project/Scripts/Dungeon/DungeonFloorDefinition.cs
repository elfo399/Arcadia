using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName="DungeonFloor", menuName="Dungeon/Floor Definition")]
public sealed class DungeonFloorDefinition : ScriptableObject
{
    [Serializable] public sealed class RoomCount { [Min(0)] public int min=15; [Min(0)] public int max=15; public int Resolve(System.Random random)=>random.Next(Mathf.Min(min,max),Mathf.Max(min,max)+1); }
    [Tooltip("min == max creates an exact quantity.")] public RoomCount normalRooms = new RoomCount();
    public List<LootPoolDefinition> lootPools = new List<LootPoolDefinition>();
    public List<SpawnTable> enemyPools = new List<SpawnTable>();
    private void OnValidate(){if(normalRooms!=null&&normalRooms.max<normalRooms.min)normalRooms.max=normalRooms.min;}
}
