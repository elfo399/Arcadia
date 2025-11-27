using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

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

    [Header("Prefabs")]
    public Room startRoomPrefab; 
    public Room normal1x1;    
    public Room normal2x1; 
    public Room normal1x2; 
    public Room normal2x2; 
    public Room boss1x1;     
    public Room boss2x1; 
    public Room boss1x2; 
    public Room boss2x2; 
    public Room treasure1x1; 

    // Database Stanze
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

        // PULIZIA
        foreach (var r in activeRooms) if (r != null) Destroy(r.gameObject);
        activeRooms.Clear();
        gridMap.Clear();
        anchors.Clear();
        if (MinimapManager.instance) MinimapManager.instance.ClearMap();

        // 3. START ROOM
        SpawnRoom(Vector2Int.zero, (startRoomPrefab != null ? startRoomPrefab : normal1x1));

        // 4. RANDOM WALKER
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

        // 5. STANZE SPECIALI
        HandleSpecialRooms();

        // 6. FINISH
        ConnectAllDoors();
        if (navMeshSurface != null) navMeshSurface.BuildNavMesh();
        DrawMinimapFinal();
    }

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

    // Abbiamo rimosso il parametro 'string type' perché è ridondante!
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
        // Boss (Ultima stanza valida)
        for (int i = anchors.Count - 1; i >= 0; i--)
        {
            Vector2Int pos = anchors[i];
            if (pos != Vector2Int.zero && gridMap.ContainsKey(pos))
            {
                Room current = gridMap[pos];
                // Controlliamo che non sia già una stanza speciale usando i BOOLeani
                if (current.roomData.isStartRoom || current.roomData.isBossRoom) continue;

                Room bossPrefab = GetBossPrefab(current.roomData.size);
                if (bossPrefab != null)
                {
                    ReplaceRoom(current, bossPrefab);
                    break; 
                }
            }
        }

        // Treasure (Random 1x1)
        for (int i = 0; i < 50; i++)
        {
            Vector2Int rndPos = anchors[Random.Range(0, anchors.Count)];
            if (rndPos != Vector2Int.zero && gridMap.ContainsKey(rndPos))
            {
                Room r = gridMap[rndPos];
                
                // Usiamo i flag di RoomData per assicurarci di sostituire solo stanze normali
                if (!r.roomData.isStartRoom && !r.roomData.isBossRoom && r.roomData.size == new Vector2Int(1,1))
                {
                    ReplaceRoom(r, treasure1x1);
                    break;
                }
            }
        }
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
            
            // Passiamo direttamente RoomData, che contiene tutte le info (Size e Tipo)
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