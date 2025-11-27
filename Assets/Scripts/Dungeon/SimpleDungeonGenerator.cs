using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using System.Linq; 

public class SimpleDungeonGenerator : MonoBehaviour
{
    [Header("Riferimenti")]
    public Transform playerTransform;
    public NavMeshSurface navMeshSurface; 

    [Header("Configurazione Seed")]
    public string gameSeed = "";       
    public bool useRandomSeed = true;  

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
    public Room normal1x1;    
    public Room normal2x1; 
    public Room normal1x2; 
    public Room normal2x2; 
    
    [Header("Prefabs Stanze Speciali")]
    public Room boss1x1;     
    public Room boss2x1; 
    public Room boss1x2; 
    public Room boss2x2; 
    public Room treasure1x1; 
    public Room shop1x1;

    // Database
    private List<Room> activeRooms = new List<Room>(); 
    private Dictionary<Vector2Int, Room> gridMap = new Dictionary<Vector2Int, Room>();
    private List<Vector2Int> anchors = new List<Vector2Int>(); 

    private readonly Vector2Int North = Vector2Int.up;
    private readonly Vector2Int South = Vector2Int.down;
    private readonly Vector2Int East = Vector2Int.right;
    private readonly Vector2Int West = Vector2Int.left;

    void Start() { Generate(); }
    
    void Update() { 
        if (MinimapManager.instance && playerTransform) 
            MinimapManager.instance.UpdatePlayerPosition(playerTransform.position, xOffset); 
    }

    void Generate()
    {
        if (useRandomSeed) gameSeed = GenerateRandomId();
        gameSeed = gameSeed.ToUpper();
        Random.InitState(gameSeed.GetHashCode());
        Debug.Log($"<color=cyan>SEED: {gameSeed}</color>");

        foreach (var r in activeRooms) if (r != null) Destroy(r.gameObject);
        activeRooms.Clear();
        gridMap.Clear();
        anchors.Clear();
        if (MinimapManager.instance) MinimapManager.instance.ClearMap();

        SpawnRoom(Vector2Int.zero, (startRoomPrefab != null ? startRoomPrefab : normal1x1));

        int safety = 0;
        while (anchors.Count < totalRooms && safety < 1000)
        {
            safety++;
            Vector2Int startPoint = anchors[Random.Range(0, anchors.Count)];
            Vector2Int targetPos = startPoint + GetRandomDirection();

            if (gridMap.ContainsKey(targetPos)) continue; 

            bool spawned = false;
            if (!spawned && TrySpawn(targetPos, normal2x2, chanceBigRoom)) spawned = true;
            if (!spawned && TrySpawn(targetPos, normal2x1, chanceBigRoom)) spawned = true;
            if (!spawned && TrySpawn(targetPos, normal1x2, chanceBigRoom)) spawned = true;
            if (!spawned) TrySpawn(targetPos, normal1x1, 100);
        }

        // QUESTA È LA FUNZIONE CHE HO RISCRITTO PER ESSERE INFALLIBILE
        HandleSpecialRooms();

        ConnectAllDoors();
        if (navMeshSurface != null) navMeshSurface.BuildNavMesh();
        DrawMinimapFinal();
    }

    // --- LOGICA SPAWN ---
    bool TrySpawn(Vector2Int pos, Room prefab, int chance)
    {
        if (prefab == null) return false;
        if (Random.Range(0, 100) > chance) return false;
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

    // --- NUOVA LOGICA: MAI FALLIRE ---
    void HandleSpecialRooms()
    {
        // 1. Creiamo una lista di TUTTI i candidati possibili (Esclusa Start)
        List<Room> allCandidates = new List<Room>();
        foreach (var r in activeRooms)
        {
            if (!r.roomData.isStartRoom) allCandidates.Add(r);
        }

        // Sottolista: Solo Vicoli Ciechi (Preferiti)
        List<Room> deadEnds = allCandidates.FindAll(r => GetRoomConnectionsCount(r) == 1);

        // ================= BOSS (OBBLIGATORIO) =================
        Room bossTarget = null;

        // TENTATIVO 1: Vicolo cieco LONTANO
        var bossPool = deadEnds.FindAll(r => GetDistanceFromStart(r) >= minBossDistance);
        
        // TENTATIVO 2 (Fallback): Qualsiasi Vicolo Cieco
        if (bossPool.Count == 0) bossPool = new List<Room>(deadEnds);

        // TENTATIVO 3 (Emergenza): Qualsiasi stanza lontana (anche di passaggio)
        if (bossPool.Count == 0) bossPool = allCandidates.FindAll(r => GetDistanceFromStart(r) >= minBossDistance);

        // TENTATIVO 4 (Disperazione): Qualsiasi stanza disponibile
        if (bossPool.Count == 0) bossPool = new List<Room>(allCandidates);

        // Ora scegliamo la MIGLIORE tra quelle disponibili (la più lontana)
        if (bossPool.Count > 0)
        {
            bossPool.Sort((a, b) => GetDistanceFromStart(b).CompareTo(GetDistanceFromStart(a))); // Ordine decrescente
            bossTarget = bossPool[0]; // La prima è la più lontana

            Room bossPrefab = GetBossPrefab(bossTarget.roomData.size);
            if (bossPrefab != null)
            {
                ReplaceRoom(bossTarget, bossPrefab);
                // Rimuoviamo la stanza (ora distrutta) dalle liste per non riusarla
                allCandidates.Remove(bossTarget);
                deadEnds.Remove(bossTarget);
            }
        }
        else
        {
            Debug.LogError("IMPOSSIBILE PIAZZARE IL BOSS: Nessuna stanza disponibile!");
        }

        // ================= TREASURE (OBBLIGATORIO) =================
        if (treasure1x1 != null)
        {
            Room treasureTarget = null;
            
            // Filtro: Solo stanze 1x1
            var smallRooms = allCandidates.FindAll(r => r.roomData.size == new Vector2Int(1, 1));
            var smallDeadEnds = deadEnds.FindAll(r => r.roomData.size == new Vector2Int(1, 1));

            // TENTATIVO 1: Vicolo Cieco 1x1 Lontano
            var treasurePool = smallDeadEnds.FindAll(r => GetDistanceFromStart(r) >= minTreasureDistance);

            // TENTATIVO 2: Vicolo Cieco 1x1 Qualsiasi
            if (treasurePool.Count == 0) treasurePool = new List<Room>(smallDeadEnds);

            // TENTATIVO 3: Stanza 1x1 Qualsiasi Lontana (anche di passaggio)
            if (treasurePool.Count == 0) treasurePool = smallRooms.FindAll(r => GetDistanceFromStart(r) >= minTreasureDistance);

            // TENTATIVO 4: Stanza 1x1 Qualsiasi
            if (treasurePool.Count == 0) treasurePool = new List<Room>(smallRooms);

            if (treasurePool.Count > 0)
            {
                treasureTarget = treasurePool[Random.Range(0, treasurePool.Count)];
                ReplaceRoom(treasureTarget, treasure1x1);
                allCandidates.Remove(treasureTarget);
                deadEnds.Remove(treasureTarget);
            }
        }

        // ================= SHOP (OBBLIGATORIO) =================
        if (shop1x1 != null)
        {
            Room shopTarget = null;
            
            var smallRooms = allCandidates.FindAll(r => r.roomData.size == new Vector2Int(1, 1));
            var smallDeadEnds = deadEnds.FindAll(r => r.roomData.size == new Vector2Int(1, 1));

            // TENTATIVO 1: Vicolo Cieco 1x1 Lontano
            var shopPool = smallDeadEnds.FindAll(r => GetDistanceFromStart(r) >= minShopDistance);

            // TENTATIVO 2: Vicolo Cieco 1x1 Qualsiasi
            if (shopPool.Count == 0) shopPool = new List<Room>(smallDeadEnds);

            // TENTATIVO 3: Stanza 1x1 Qualsiasi
            if (shopPool.Count == 0) shopPool = new List<Room>(smallRooms);

            if (shopPool.Count > 0)
            {
                shopTarget = shopPool[Random.Range(0, shopPool.Count)];
                ReplaceRoom(shopTarget, shop1x1);
            }
        }
    }

    int GetRoomConnectionsCount(Room room)
    {
        HashSet<Room> neighbors = new HashSet<Room>();
        Vector3 wPos = room.transform.position;
        int ax = Mathf.RoundToInt(wPos.x / xOffset);
        int ay = Mathf.RoundToInt(wPos.z / zOffset);
        Vector2Int anchor = new Vector2Int(ax, ay);
        Vector2Int size = room.roomData.size;

        for (int x = 0; x < size.x; x++) {
            for (int y = 0; y < size.y; y++) {
                Vector2Int cell = anchor + new Vector2Int(x, y);
                CheckNeighborForCount(cell + North, room, neighbors);
                CheckNeighborForCount(cell + South, room, neighbors);
                CheckNeighborForCount(cell + East, room, neighbors);
                CheckNeighborForCount(cell + West, room, neighbors);
            }
        }
        return neighbors.Count;
    }

    void CheckNeighborForCount(Vector2Int targetPos, Room myRoom, HashSet<Room> list)
    {
        if (gridMap.ContainsKey(targetPos))
        {
            Room neighbor = gridMap[targetPos];
            if (neighbor != myRoom) list.Add(neighbor);
        }
    }

    int GetDistanceFromStart(Room r)
    {
        Vector3 pos = r.transform.position;
        int x = Mathf.RoundToInt(pos.x / xOffset);
        int y = Mathf.RoundToInt(pos.z / zOffset);
        return Mathf.Abs(x) + Mathf.Abs(y);
    }

    void ReplaceRoom(Room oldRoom, Room newPrefab)
    {
        Vector3 oldPos = oldRoom.transform.position;
        int ax = Mathf.RoundToInt(oldPos.x / xOffset);
        int ay = Mathf.RoundToInt(oldPos.z / zOffset);
        Vector2Int anchor = new Vector2Int(ax, ay);

        Vector2Int size = oldRoom.roomData.size;
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                gridMap.Remove(anchor + new Vector2Int(x, y));

        activeRooms.Remove(oldRoom);
        Destroy(oldRoom.gameObject);

        SpawnRoom(anchor, newPrefab);
    }

    void ConnectAllDoors()
    {
        foreach (Room r in activeRooms)
        {
            if (r == null) continue;
            int ax = Mathf.RoundToInt(r.transform.position.x / xOffset);
            int ay = Mathf.RoundToInt(r.transform.position.z / zOffset);
            Vector2Int anchor = new Vector2Int(ax, ay);
            Vector2Int size = r.roomData.size;

            for (int x = 0; x < size.x; x++) {
                for (int y = 0; y < size.y; y++) {
                    Vector2Int currentCell = anchor + new Vector2Int(x, y);
                    Vector2Int relPos = new Vector2Int(x, y);
                    SafeCheckNeighbor(r, currentCell, relPos, North);
                    SafeCheckNeighbor(r, currentCell, relPos, South);
                    SafeCheckNeighbor(r, currentCell, relPos, East);
                    SafeCheckNeighbor(r, currentCell, relPos, West);
                }
            }
        }
    }

    void SafeCheckNeighbor(Room myRoom, Vector2Int myCell, Vector2Int relPos, Vector2Int dir)
    {
        Vector2Int neighborCell = myCell + dir;
        if (gridMap.ContainsKey(neighborCell))
        {
            Room neighborRoom = gridMap[neighborCell];
            if (neighborRoom != null && neighborRoom != myRoom)
            {
                myRoom.OpenDoor(relPos, dir);
            }
        }
    }

    void DrawMinimapFinal()
    {
        if (MinimapManager.instance == null) return;
        foreach (Room r in activeRooms)
        {
            if (r == null) continue;
            int ax = Mathf.RoundToInt(r.transform.position.x / xOffset);
            int ay = Mathf.RoundToInt(r.transform.position.z / zOffset);
            MinimapManager.instance.RegisterRoom(new Vector2Int(ax, ay), r.roomData);
        }
    }

    Room GetBossPrefab(Vector2Int size)
    {
        if (size == new Vector2Int(2, 2)) return boss2x2;
        if (size == new Vector2Int(2, 1)) return boss2x1;
        if (size == new Vector2Int(1, 2)) return boss1x2;
        return boss1x1;
    }

    string GenerateRandomId()
    {
        string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        System.Random rng = new System.Random((int)System.DateTime.Now.Ticks);
        char[] s = new char[9];
        for (int i=0; i<9; i++) { if(i==4) s[i]='-'; else s[i]=chars[rng.Next(chars.Length)]; }
        return new string(s);
    }

    Vector2Int GetRandomDirection() { int r = Random.Range(0, 4); return r == 0 ? North : r == 1 ? South : r == 2 ? West : East; }
}