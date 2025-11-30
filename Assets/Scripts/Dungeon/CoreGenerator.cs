using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using System.Linq; 

public class CoreGenerator : MonoBehaviour
{
    public static CoreGenerator Instance; // Singleton per accesso facile

    [Header("Riferimenti")]
    public Transform playerTransform;
    public NavMeshSurface navMeshSurface; 

    [Header("Configurazione Seed")]
    public string gameSeedString = ""; // La stringa (es. "PIPPO")
    public bool useRandomSeed = true;  
    
    // Questo è il numero magico che guiderà tutto il gioco
    [HideInInspector] public int currentMasterSeed; 

    [Header("Generazione")]
    public int totalRooms = 15;
    public int xOffset = 50; 
    public int zOffset = 50;
    [Range(0, 100)] public int chanceBigRoom = 30; 

    [Header("Regole Distanza")]
    public int minBossDistance = 3; 
    public int minTreasureDistance = 1;
    public int minShopDistance = 1;

    [Header("Prefabs Stanze Normali")]
    public Room startRoomPrefab; 
    public Room[] normal1x1Variants;    
    public Room[] normal2x1Variants; 
    public Room[] normal1x2Variants; 
    public Room[] normal2x2Variants; 
    
    [Header("Prefabs Stanze Speciali")]
    public Room[] boss1x1Variants;     
    public Room[] boss2x1Variants; 
    public Room[] boss1x2Variants; 
    public Room[] boss2x2Variants; 
    public Room[] treasure1x1Variants; 
    public Room[] shop1x1Variants; 
    // Aggiungi varianti shop/treasure 2x1 ecc se vuoi

    // Database interno
    private List<Room> activeRooms = new List<Room>(); 
    private Dictionary<Vector2Int, Room> gridMap = new Dictionary<Vector2Int, Room>();
    private List<Vector2Int> anchors = new List<Vector2Int>(); 

    // Usiamo System.Random per stabilità totale della mappa
    private System.Random prng; 

    private readonly Vector2Int North = Vector2Int.up;
    private readonly Vector2Int South = Vector2Int.down;
    private readonly Vector2Int East = Vector2Int.right;
    private readonly Vector2Int West = Vector2Int.left;

    void Awake()
    {
        Instance = this;
    }

    void Start() { Generate(); }
    
    void Update() { 
        if (MinimapManager.instance && playerTransform) 
            MinimapManager.instance.UpdatePlayerPosition(playerTransform.position, xOffset); 
    }

    public void Generate()
    {
        // 1. CALCOLO SEED
        if (useRandomSeed) gameSeedString = GenerateRandomString();
        
        // Convertiamo la stringa in un numero intero stabile
        currentMasterSeed = gameSeedString.GetHashCode();
        
        // Inizializziamo il generatore casuale PRINCIPALE
        prng = new System.Random(currentMasterSeed);
        
        Debug.Log($"<color=cyan>GENERATING DUNGEON - SEED: {gameSeedString} ({currentMasterSeed})</color>");

        // 2. PULIZIA
        foreach (var r in activeRooms) if (r != null) Destroy(r.gameObject);
        activeRooms.Clear();
        gridMap.Clear();
        anchors.Clear();
        if (MinimapManager.instance) MinimapManager.instance.ClearMap();

        // 3. START ROOM
        SpawnRoom(Vector2Int.zero, (startRoomPrefab != null ? startRoomPrefab : GetRandomRoom(normal1x1Variants)));

        // 4. RANDOM WALKER
        int safety = 0;
        while (anchors.Count < totalRooms && safety < 2000)
        {
            safety++;
            Vector2Int startPoint = anchors[prng.Next(0, anchors.Count)];
            Vector2Int targetPos = startPoint + GetRandomDirection();

            if (gridMap.ContainsKey(targetPos)) continue; 

            bool spawned = false;
            if (!spawned && TrySpawn(targetPos, GetRandomRoom(normal2x2Variants), chanceBigRoom)) spawned = true;
            if (!spawned && TrySpawn(targetPos, GetRandomRoom(normal2x1Variants), chanceBigRoom)) spawned = true;
            if (!spawned && TrySpawn(targetPos, GetRandomRoom(normal1x2Variants), chanceBigRoom)) spawned = true;
            if (!spawned) TrySpawn(targetPos, GetRandomRoom(normal1x1Variants), 100);
        }

        // 5. STANZE SPECIALI
        HandleSpecialRooms();

        // 6. FINISH
        ConnectAllDoors();
        if (navMeshSurface != null) navMeshSurface.BuildNavMesh();
        DrawMinimapFinal();
    }

    // --- LOGICA HELPER ---

    Room GetRandomRoom(Room[] variants)
    {
        if (variants == null || variants.Length == 0) return null;
        return variants[prng.Next(0, variants.Length)];
    }

    bool TrySpawn(Vector2Int pos, Room prefab, int chance)
    {
        if (prefab == null) return false;
        if (prng.Next(0, 100) > chance) return false;
        if (!CanFitRoom(pos, prefab.roomData.size)) return false;
        SpawnRoom(pos, prefab);
        return true;
    }

    bool CanFitRoom(Vector2Int anchorPos, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                if (gridMap.ContainsKey(anchorPos + new Vector2Int(x, y))) return false; 
        return true;
    }

    void SpawnRoom(Vector2Int anchorPos, Room prefab)
    {
        Vector3 worldPos = new Vector3(anchorPos.x * xOffset, 0, anchorPos.y * zOffset);
        Room newRoom = Instantiate(prefab, worldPos, Quaternion.identity);
        newRoom.transform.parent = transform;
        newRoom.name = $"{prefab.roomData.roomName}_{anchorPos}";

        Vector2Int size = prefab.roomData.size;
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                gridMap.Add(anchorPos + new Vector2Int(x, y), newRoom);
        
        anchors.Add(anchorPos);
        activeRooms.Add(newRoom);
    }

    void HandleSpecialRooms()
    {
        List<Room> candidates = activeRooms.Where(r => !r.roomData.isStartRoom).ToList();
        List<Room> deadEnds = candidates.FindAll(r => GetRoomConnectionsCount(r) == 1);

        // BOSS
        PlaceSpecialRoomType(deadEnds, candidates, minBossDistance, "Boss");
        RefreshLists(ref deadEnds, ref candidates);

        // TREASURE
        PlaceSpecialRoomType(deadEnds, candidates, minTreasureDistance, "Treasure");
        RefreshLists(ref deadEnds, ref candidates);

        // SHOP
        PlaceSpecialRoomType(deadEnds, candidates, minShopDistance, "Shop");
    }

    void PlaceSpecialRoomType(List<Room> primaryPool, List<Room> fallbackPool, int minDistance, string type)
    {
        var validPrimary = primaryPool.FindAll(r => GetDistanceFromStart(r) >= minDistance);
        var validFallback = fallbackPool.FindAll(r => GetDistanceFromStart(r) >= minDistance);

        // Filtra se abbiamo prefab per quella taglia
        validPrimary = validPrimary.FindAll(r => HasPrefabForTypeAndSize(type, r.roomData.size));
        validFallback = validFallback.FindAll(r => HasPrefabForTypeAndSize(type, r.roomData.size));

        Room target = null;

        if (validPrimary.Count > 0)
        {
            if (type == "Boss") // Boss: Più lontano
            {
                validPrimary.Sort((a, b) => GetDistanceFromStart(b).CompareTo(GetDistanceFromStart(a)));
                target = validPrimary[0];
            }
            else // Altri: Random
            {
                target = validPrimary[prng.Next(0, validPrimary.Count)];
            }
        }
        else if (validFallback.Count > 0)
        {
            if (type == "Boss") { validFallback.Sort((a, b) => GetDistanceFromStart(b).CompareTo(GetDistanceFromStart(a))); target = validFallback[0]; }
            else target = validFallback[prng.Next(0, validFallback.Count)];
        }

        if (target != null)
        {
            Room prefab = GetPrefabForTypeAndSize(type, target.roomData.size);
            if (prefab != null) ReplaceRoom(target, prefab);
        }
    }

    void RefreshLists(ref List<Room> deadEnds, ref List<Room> all)
    {
        deadEnds.RemoveAll(r => r == null || r.roomData.isBossRoom || r.roomData.isTreasureRoom || r.roomData.isShopRoom);
        all.RemoveAll(r => r == null || r.roomData.isBossRoom || r.roomData.isTreasureRoom || r.roomData.isShopRoom);
    }

    bool HasPrefabForTypeAndSize(string type, Vector2Int size) => GetPrefabForTypeAndSize(type, size) != null;

    Room GetPrefabForTypeAndSize(string type, Vector2Int size)
    {
        Room[] variants = null;
        if (type == "Boss") variants = (size == new Vector2Int(2,2)) ? boss2x2Variants : (size == new Vector2Int(2,1)) ? boss2x1Variants : (size == new Vector2Int(1,2)) ? boss1x2Variants : boss1x1Variants;
        else if (type == "Treasure") variants = (size == new Vector2Int(2,2)) ? treasure2x2Variants : (size == new Vector2Int(2,1)) ? treasure2x1Variants : (size == new Vector2Int(1,2)) ? treasure1x2Variants : treasure1x1Variants;
        else if (type == "Shop") variants = (size == new Vector2Int(2,2)) ? shop2x2Variants : (size == new Vector2Int(2,1)) ? shop2x1Variants : (size == new Vector2Int(1,2)) ? shop1x2Variants : shop1x1Variants;
        return GetRandomRoom(variants);
    }

    int GetRoomConnectionsCount(Room room)
    {
        HashSet<Room> neighbors = new HashSet<Room>();
        Vector3 wPos = room.transform.position;
        int ax = Mathf.RoundToInt(wPos.x / xOffset);
        int ay = Mathf.RoundToInt(wPos.z / zOffset);
        Vector2Int anchor = new Vector2Int(ax, ay);
        Vector2Int size = room.roomData.size;
        for (int x = 0; x < size.x; x++) for (int y = 0; y < size.y; y++) {
            Vector2Int cell = anchor + new Vector2Int(x, y);
            CheckNeighborForCount(cell + North, room, neighbors); CheckNeighborForCount(cell + South, room, neighbors);
            CheckNeighborForCount(cell + East, room, neighbors); CheckNeighborForCount(cell + West, room, neighbors);
        }
        return neighbors.Count;
    }

    void CheckNeighborForCount(Vector2Int targetPos, Room myRoom, HashSet<Room> list)
    {
        if (gridMap.ContainsKey(targetPos)) {
            Room neighbor = gridMap[targetPos];
            if (neighbor != myRoom) list.Add(neighbor);
        }
    }

    int GetDistanceFromStart(Room r)
    {
        Vector3 pos = r.transform.position;
        int x = Mathf.RoundToInt(pos.x / xOffset); int y = Mathf.RoundToInt(pos.z / zOffset);
        return Mathf.Abs(x) + Mathf.Abs(y);
    }

    void ReplaceRoom(Room oldRoom, Room newPrefab)
    {
        Vector3 oldPos = oldRoom.transform.position;
        int ax = Mathf.RoundToInt(oldPos.x / xOffset); int ay = Mathf.RoundToInt(oldPos.z / zOffset);
        Vector2Int anchor = new Vector2Int(ax, ay);
        Vector2Int size = oldRoom.roomData.size;
        for (int x = 0; x < size.x; x++) for (int y = 0; y < size.y; y++) gridMap.Remove(anchor + new Vector2Int(x, y));
        activeRooms.Remove(oldRoom);
        Destroy(oldRoom.gameObject);
        SpawnRoom(anchor, newPrefab);
    }

    void ConnectAllDoors()
    {
        foreach (Room r in activeRooms) {
            if(r==null) continue;
            int ax = Mathf.RoundToInt(r.transform.position.x/xOffset); int ay = Mathf.RoundToInt(r.transform.position.z/zOffset);
            Vector2Int anchor = new Vector2Int(ax, ay);
            Vector2Int size = r.roomData.size;
            for (int x=0; x<size.x; x++) for (int y=0; y<size.y; y++) {
                Vector2Int cell = anchor + new Vector2Int(x,y); Vector2Int rel = new Vector2Int(x,y);
                SafeCheckNeighbor(r, cell, rel, North); SafeCheckNeighbor(r, cell, rel, South);
                SafeCheckNeighbor(r, cell, rel, East); SafeCheckNeighbor(r, cell, rel, West);
            }
        }
    }

    void SafeCheckNeighbor(Room myRoom, Vector2Int myCell, Vector2Int relPos, Vector2Int dir)
    {
        if (gridMap.ContainsKey(myCell + dir)) {
            Room neighbor = gridMap[myCell + dir];
            if (neighbor != null && neighbor != myRoom) myRoom.OpenDoor(relPos, dir);
        }
    }

    void DrawMinimapFinal()
    {
        if (MinimapManager.instance == null) return;
        foreach (Room r in activeRooms) {
            if(r==null) continue;
            int ax = Mathf.RoundToInt(r.transform.position.x/xOffset); int ay = Mathf.RoundToInt(r.transform.position.z/zOffset);
            MinimapManager.instance.RegisterRoom(new Vector2Int(ax, ay), r.roomData);
        }
    }

    string GenerateRandomString()
    {
        string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        System.Random rng = new System.Random((int)System.DateTime.Now.Ticks);
        char[] s = new char[9];
        for (int i=0; i<9; i++) { if(i==4) s[i]='-'; else s[i]=chars[rng.Next(chars.Length)]; }
        return new string(s);
    }

    Vector2Int GetRandomDirection() { int r = prng.Next(0, 4); return r == 0 ? North : r == 1 ? South : r == 2 ? West : East; }
}