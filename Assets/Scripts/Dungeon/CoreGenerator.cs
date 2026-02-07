using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using System.Linq;
using UnityEngine.SceneManagement;

public class CoreGenerator : MonoBehaviour
{
    public static CoreGenerator Instance;

    #region --- Riferimenti & Configurazione ---

    [Header("Riferimenti")]
    public Transform playerTransform;
    public NavMeshSurface navMeshSurface;

    [Header("Scene Management")]
    public string hubSceneName = "HubScene";

    [Header("Configurazione Seed")]
    public string gameSeedString = "";
    public bool useRandomSeed = true;
    [HideInInspector] public int currentMasterSeed;

    [Header("Progressione Piani")]
    public int currentFloor = 1;
    public int maxFloors = 4;
    public Vector3 playerSpawnOffset = Vector3.zero;

    [Header("Generazione")]
    public int totalNormalRooms = 15;
    public int xOffset = 50;
    public int zOffset = 50;

    [Header("Probabilità special room")]
    [Range(0, 100)] public int curchsRoomsChance = 20;

    [Header("Probabilità Big Room (Normali)")]
    [Range(0, 100)] public int normalBigRoomChance = 30;

    [Header("Probabilità Big Room (Speciali)")]
    [Range(0, 100)] public int bossBigRoomChance = 100;
    [Range(0, 100)] public int shopBigRoomChance = 50;
    [Range(0, 100)] public int treasureBigRoomChance = 50;
    [Range(0, 100)] public int curchBigRoomChance = 50;
    [Range(0, 100)] public int evilCurchBigRoomChance = 50;

    [Header("Regole Distanza & Adiacenza")]
    [Tooltip("Distanza minima (celle) dallo Start per il Boss.")]
    public int minBossDistance = 4;
    [Tooltip("Se TRUE, il Boss non spawnerà MAI attaccato a Shop o Treasure.")]
    public bool avoidBossTouchingSpecials = true;
    [Tooltip("Se TRUE, il Boss avrà sempre e solo UN ingresso (Vicolo Cieco).")]
    public bool bossMustBeDeadEnd = true;
    
    [Header("Debug")]
    public bool showRngLogs = true;

    #endregion

    #region --- Prefabs ---

    [Header("Prefabs Stanze Normali")]
    public Room startRoomPrefab;
    public Room[] normal1x1Variants;
    public Room[] normal2x1Variants;
    public Room[] normal1x2Variants;
    public Room[] normal2x2Variants;

    [Header("Prefabs Boss")]
    public Room[] boss1x1Variants;
    public Room[] boss2x1Variants;
    public Room[] boss1x2Variants;
    public Room[] boss2x2Variants;

    [Header("Prefabs Tesoro")]
    public Room[] treasure1x1Variants;
    public Room[] treasure2x1Variants;
    public Room[] treasure1x2Variants;
    public Room[] treasure2x2Variants;

    [Header("Prefabs Shop")]
    public Room[] shop1x1Variants;
    public Room[] shop2x1Variants;
    public Room[] shop1x2Variants;
    public Room[] shop2x2Variants;

    [Header("Prefabs Curch")]
    public Room[] curch1x1Variants;
    public Room[] curch2x2Variants;

    [Header("Prefabs Evil Curch")]
    public Room[] evilCurch1x1Variants;
    public Room[] evilCurch2x2Variants;

    #endregion

    #region --- Strutture Dati Interne ---

    private class VirtualRoom
    {
        public Vector2Int anchorPos;
        public Vector2Int size;
        public string type;
        public Room prefabReference;

        public bool Contains(Vector2Int point)
        {
            return point.x >= anchorPos.x && point.x < anchorPos.x + size.x &&
                   point.y >= anchorPos.y && point.y < anchorPos.y + size.y;
        }
    }

    private List<Room> activeRoomObjects = new List<Room>();
    private Room startRoomInstance;
    private System.Random prng;
    private PlayerStats playerStats;
    
    // OTTIMIZZAZIONE: Dizionario per accesso rapido ai prefab, inizializzato una sola volta.
    private Dictionary<string, Dictionary<Vector2Int, Room[]>> _prefabLookup;

    private readonly Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    
    private readonly Vector2Int[] bigSizes = { 
        new Vector2Int(2, 2), 
        new Vector2Int(2, 1), 
        new Vector2Int(1, 2) 
    };

    #endregion

    #region --- Unity Lifecycle ---

    void Awake() 
    { 
        Instance = this;
        playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats == null) Debug.LogWarning("[CoreGenerator] PlayerStats non trovato! La generazione di stanze speciali (Curch/EvilCurch) non funzionerà.");
        
        InitializePrefabLookup();
    }
    
    void Start() { Generate(); }
    
    void Update() 
    { 
        if (MinimapManager.instance && playerTransform) 
            MinimapManager.instance.UpdatePlayerPosition(playerTransform.position, xOffset); 
    }

    #endregion

    #region --- Core Generation Logic ---

    public void Generate()
    {
        if (startRoomPrefab == null || startRoomPrefab.roomData == null)
        {
            Debug.LogError("[CoreGenerator] StartRoomPrefab o RoomData mancante. Configura il prefab di start.");
            return;
        }

        if (useRandomSeed)
        {
            gameSeedString = GenerateSeedString();
            useRandomSeed = false;
        }
        
        string floorSeedString = $"{gameSeedString}-{currentFloor}";
        currentMasterSeed = ComputeSeedHash(floorSeedString);
        if (showRngLogs) Debug.Log($"[CoreGenerator] Seed per piano {currentFloor}: '{floorSeedString}' -> Hash: {currentMasterSeed}");

        CleanupScene();
        startRoomInstance = null;

        List<VirtualRoom> finalLayout = null;
        int attempts = 0;
        bool success = false;

        while (!success && attempts < 300)
        {
            prng = new System.Random(currentMasterSeed + attempts);
            
            if(showRngLogs && attempts == 0) Debug.Log($"--- INIZIO GENERAZIONE PIANO {currentFloor} (Seed: {floorSeedString}) ---");

            finalLayout = TryBuildVirtualLayout();

            if (finalLayout != null) success = true;
            else attempts++;
        }

        if (success)
        {
            Debug.Log($"<color=cyan>DUNGEON GENERATO. Seed: {gameSeedString} (Tentativi: {attempts + 1})</color>");
            SpawnDungeon(finalLayout);
            ConnectDoors();
            if (navMeshSurface != null) navMeshSurface.BuildNavMesh();
            InitializeMinimap();
            RespawnPlayerAtStart();
        }
        else
        {
            Debug.LogError("CRITICO: Impossibile generare dungeon. Le regole di piazzamento sono troppo strette.");
        }
    }

    public void NextFloor()
    {
        if (currentFloor >= maxFloors)
        {
            SceneManager.LoadScene(hubSceneName);
        }
        else
        {
            currentFloor++;
            Generate();
        }
    }

    private List<VirtualRoom> TryBuildVirtualLayout()
    {
        HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
        List<VirtualRoom> layout = new List<VirtualRoom>();

        // 1. START
        AddRoomToLayout(layout, occupiedCells, Vector2Int.zero, new Vector2Int(1, 1), "Start", startRoomPrefab);

        // 2. CORPO CENTRALE
        List<VirtualRoom> expandableRooms = new List<VirtualRoom> { layout[0] };
        int normalCount = 0;
        while (normalCount < totalNormalRooms && expandableRooms.Count > 0)
        {
            VirtualRoom origin = expandableRooms[prng.Next(expandableRooms.Count)];
            Vector2Int dir = directions[prng.Next(directions.Length)];
            Vector2Int potentialAnchor = origin.anchorPos + dir;

            List<Vector2Int> sizesToTry = GetSizesToTry(normalBigRoomChance, "Normal");

            foreach (var size in sizesToTry)
            {
                if (CanFit(potentialAnchor, size, occupiedCells, false))
                {
                    Room prefab = GetRandomPrefab("Normal", size);
                    if (prefab != null)
                    {
                        VirtualRoom newRoom = AddRoomToLayout(layout, occupiedCells, potentialAnchor, size, "Normal", prefab);
                        expandableRooms.Add(newRoom);
                        normalCount++;
                        break; 
                    }
                }
            }
        }

        // --- FASE DI PIAZZAMENTO STANZE SPECIALI ---

        // 3. OTTIMIZZAZIONE: Crea una mappa spaziale una sola volta per velocizzare i controlli di adiacenza.
        var cellToRoomMap = BuildCellToRoomMap(layout);
        
        // 4. Trova candidati per la sostituzione (vicoli ciechi) e per l'aggiunta (spazi esterni).
        List<VirtualRoom> deadEndNormalRooms = FindDeadEndNormalRooms(layout, cellToRoomMap);
        deadEndNormalRooms = deadEndNormalRooms.OrderBy(x => prng.Next()).ToList();

        List<Vector2Int> freeSockets = FindFreeSockets(occupiedCells);
        freeSockets = freeSockets.OrderBy(x => prng.Next()).ToList();
        
        // 5. Tenta di piazzare le stanze speciali, passando la mappa spaziale per ottimizzare i controlli.
        if (!PlaceSpecialRoom("Shop", layout, occupiedCells, deadEndNormalRooms, freeSockets, -1, shopBigRoomChance, cellToRoomMap)) return null;
        if (!PlaceSpecialRoom("Treasure", layout, occupiedCells, deadEndNormalRooms, freeSockets, -1, treasureBigRoomChance, cellToRoomMap)) return null;
        if (!PlaceSpecialRoom("Boss", layout, occupiedCells, deadEndNormalRooms, freeSockets, minBossDistance, bossBigRoomChance, cellToRoomMap)) return null;

        // 5.1 CURCH / EVIL CURCH (opzionale)
        if (playerStats != null && prng.Next(0, 100) <= curchsRoomsChance)
        {
            string curchType = playerStats.benedetto > playerStats.malefico ? "Curch" : "EvilCurch";
            int curchChance = playerStats.benedetto > playerStats.malefico ? curchBigRoomChance : evilCurchBigRoomChance;
            if(playerStats.benedetto != playerStats.malefico)
                PlaceSpecialRoom(curchType, layout, occupiedCells, deadEndNormalRooms, freeSockets, 0, curchChance, cellToRoomMap);
        }

        return layout;
    }
    
    #endregion

    #region --- Logica di Piazzamento e Helpers ---
    
    private Dictionary<Vector2Int, VirtualRoom> BuildCellToRoomMap(List<VirtualRoom> layout)
    {
        var map = new Dictionary<Vector2Int, VirtualRoom>();
        foreach (var r in layout)
        {
            for (int x = 0; x < r.size.x; x++)
                for (int y = 0; y < r.size.y; y++)
                    map[r.anchorPos + new Vector2Int(x, y)] = r;
        }
        return map;
    }

    private List<VirtualRoom> FindDeadEndNormalRooms(List<VirtualRoom> layout, Dictionary<Vector2Int, VirtualRoom> cellToRoomMap)
    {
        var candidates = new List<VirtualRoom>();
        var normalRooms = layout.Where(r => r.type == "Normal").ToList();
        
        if (normalRooms.Count == 0) return candidates;

        foreach (var room in normalRooms)
        {
            var neighbors = new HashSet<VirtualRoom>();
            for (int x = 0; x < room.size.x; x++)
            {
                for (int y = 0; y < room.size.y; y++)
                {
                    var cell = room.anchorPos + new Vector2Int(x, y);
                    foreach (var dir in directions)
                    {
                        if (cellToRoomMap.TryGetValue(cell + dir, out var neighborRoom) && neighborRoom != room)
                            neighbors.Add(neighborRoom);
                    }
                }
            }

            if (neighbors.Count == 1)
            {
                candidates.Add(room);
            }
        }
        return candidates;
    }
    
    private List<Vector2Int> FindFreeSockets(HashSet<Vector2Int> occupiedCells)
    {
        var sockets = new List<Vector2Int>();
        foreach (var cell in occupiedCells)
        {
            foreach (var dir in directions)
            {
                var neighbor = cell + dir;
                if (!occupiedCells.Contains(neighbor) && !sockets.Contains(neighbor))
                    sockets.Add(neighbor);
            }
        }
        return sockets;
    }
    
    private void TemporarilyRemoveRoom(VirtualRoom room, List<VirtualRoom> layout, HashSet<Vector2Int> occupied, Dictionary<Vector2Int, VirtualRoom> cellToRoomMap)
    {
        layout.Remove(room);
        for (int x = 0; x < room.size.x; x++)
        {
            for (int y = 0; y < room.size.y; y++)
            {
                Vector2Int cell = room.anchorPos + new Vector2Int(x, y);
                occupied.Remove(cell);
                cellToRoomMap.Remove(cell);
            }
        }
    }

    private void RestoreRoom(VirtualRoom room, List<VirtualRoom> layout, HashSet<Vector2Int> occupied, Dictionary<Vector2Int, VirtualRoom> cellToRoomMap)
    {
        layout.Add(room);
        for (int x = 0; x < room.size.x; x++)
        {
            for (int y = 0; y < room.size.y; y++)
            {
                Vector2Int cell = room.anchorPos + new Vector2Int(x, y);
                occupied.Add(cell);
                cellToRoomMap[cell] = room;
            }
        }
    }
    
    bool PlaceSpecialRoom(string type, List<VirtualRoom> layout, HashSet<Vector2Int> occupied, List<VirtualRoom> replacementCandidates, List<Vector2Int> freeSockets, int minDistance, int bigRoomChance, Dictionary<Vector2Int, VirtualRoom> cellToRoomMap)
    {
        var sizesToTry = GetSizesToTry(bigRoomChance, type);

        // --- Strategia 1: Sostituzione di una stanza interna (vicolo cieco) ---
        foreach (var roomToReplace in replacementCandidates.ToList()) 
        {
            if (minDistance > 0 && GetManhattanDist(Vector2Int.zero, roomToReplace.anchorPos) < minDistance) continue;
            if (avoidBossTouchingSpecials && type == "Boss" && IsTouchingRestrictedRoom(roomToReplace.anchorPos, roomToReplace.size, cellToRoomMap, roomToReplace)) continue;
            if (IsTouchingAnySpecialRoom(roomToReplace.anchorPos, roomToReplace.size, cellToRoomMap, roomToReplace)) continue;
            
            Room prefab = GetRandomPrefab(type, roomToReplace.size);
            if (prefab != null)
            {
                TemporarilyRemoveRoom(roomToReplace, layout, occupied, cellToRoomMap);
                AddRoomToLayout(layout, occupied, roomToReplace.anchorPos, roomToReplace.size, type, prefab, cellToRoomMap);
                replacementCandidates.Remove(roomToReplace);
                return true; 
            }
        }

        // --- Strategia 2: Aggiunta su un bordo esterno (fallback) ---
        bool isStrictDeadEnd = (type == "Boss" && bossMustBeDeadEnd);
        foreach (var spot in freeSockets.ToList())
        {
            if (minDistance > 0 && GetManhattanDist(Vector2Int.zero, spot) < minDistance) continue;

            foreach (var size in sizesToTry)
            {
                if (CanFit(spot, size, occupied, isStrictDeadEnd))
                {
                    if (avoidBossTouchingSpecials && type == "Boss" && IsTouchingRestrictedRoom(spot, size, cellToRoomMap, null)) continue;
                    if (IsTouchingAnySpecialRoom(spot, size, cellToRoomMap, null)) continue;

                    Room prefab = GetRandomPrefab(type, size);
                    if (prefab != null)
                    {
                        AddRoomToLayout(layout, occupied, spot, size, type, prefab, cellToRoomMap);
                        freeSockets.Remove(spot);
                        return true;
                    }
                }
            }
        }
        
        return type == "Curch" || type == "EvilCurch";
    }

    bool IsTouchingAnySpecialRoom(Vector2Int anchor, Vector2Int size, Dictionary<Vector2Int, VirtualRoom> cellToRoomMap, VirtualRoom roomToIgnore)
    {
        var specialRoomTypes = new HashSet<string> { "Boss", "Shop", "Treasure", "Curch", "EvilCurch" };
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int cell = anchor + new Vector2Int(x, y);
                foreach (var dir in directions)
                {
                    if (cellToRoomMap.TryGetValue(cell + dir, out var neighborRoom) && neighborRoom != roomToIgnore && specialRoomTypes.Contains(neighborRoom.type))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    bool IsTouchingRestrictedRoom(Vector2Int anchor, Vector2Int size, Dictionary<Vector2Int, VirtualRoom> cellToRoomMap, VirtualRoom roomToIgnore)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int cell = anchor + new Vector2Int(x, y);
                foreach (var dir in directions)
                {
                    if (cellToRoomMap.TryGetValue(cell + dir, out var neighborRoom) && neighborRoom != roomToIgnore && (neighborRoom.type == "Shop" || neighborRoom.type == "Treasure"))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
    
    List<Vector2Int> GetSizesToTry(int chancePercent, string roomType)
    {
        var sizes = new List<Vector2Int>();
        bool tryBig = prng.Next(0, 100) < chancePercent;

        if (showRngLogs)
        {
            string color = tryBig ? "green" : "grey";
            string resultText = tryBig ? "GRANDE" : "Piccola";
            Debug.Log($"[RNG] Stanza <b>{roomType}</b>: Roll < {chancePercent}% -> <color={color}>{resultText}</color>");
        }

        if (tryBig)
        {
            sizes.AddRange(bigSizes.OrderBy(x => prng.Next()));
        }
        sizes.Add(new Vector2Int(1, 1)); // Aggiunge sempre 1x1 come fallback
        return sizes;
    }
    
    bool CanFit(Vector2Int anchor, Vector2Int size, HashSet<Vector2Int> occupied, bool strictOneDoor)
    {
        int connectionsCount = 0;
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int cell = anchor + new Vector2Int(x, y);
                if (occupied.Contains(cell)) return false;

                if (strictOneDoor)
                {
                    foreach (var dir in directions)
                    {
                        if (occupied.Contains(cell + dir))
                        {
                            connectionsCount++;
                        }
                    }
                }
            }
        }
        return !strictOneDoor || connectionsCount == 1;
    }

    VirtualRoom AddRoomToLayout(List<VirtualRoom> layout, HashSet<Vector2Int> occupied, Vector2Int anchor, Vector2Int size, string type, Room prefab, Dictionary<Vector2Int, VirtualRoom> cellToRoomMap = null)
    {
        var vr = new VirtualRoom { anchorPos = anchor, size = size, type = type, prefabReference = prefab };
        layout.Add(vr);
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int cell = anchor + new Vector2Int(x, y);
                occupied.Add(cell);
                cellToRoomMap?.Add(cell, vr);
            }
        }
        return vr;
    }
    
    // OTTIMIZZAZIONE: Usa un dizionario pre-calcolato per un accesso istantaneo ai prefab.
    Room GetRandomPrefab(string type, Vector2Int size)
    {
        if (_prefabLookup.TryGetValue(type, out var sizeMap) && sizeMap.TryGetValue(size, out var variants))
        {
            if (variants != null && variants.Length > 0)
            {
                return variants[prng.Next(variants.Length)];
            }
        }
        return null;
    }

    #endregion

    #region --- Costruzione Fisica e Inizializzazione ---

    private void InitializePrefabLookup()
    {
        _prefabLookup = new Dictionary<string, Dictionary<Vector2Int, Room[]>>
        {
            ["Normal"] = new Dictionary<Vector2Int, Room[]>
            {
                [new Vector2Int(1, 1)] = normal1x1Variants, [new Vector2Int(2, 1)] = normal2x1Variants,
                [new Vector2Int(1, 2)] = normal1x2Variants, [new Vector2Int(2, 2)] = normal2x2Variants
            },
            ["Boss"] = new Dictionary<Vector2Int, Room[]>
            {
                [new Vector2Int(1, 1)] = boss1x1Variants, [new Vector2Int(2, 1)] = boss2x1Variants,
                [new Vector2Int(1, 2)] = boss1x2Variants, [new Vector2Int(2, 2)] = boss2x2Variants
            },
            ["Shop"] = new Dictionary<Vector2Int, Room[]>
            {
                [new Vector2Int(1, 1)] = shop1x1Variants, [new Vector2Int(2, 1)] = shop2x1Variants,
                [new Vector2Int(1, 2)] = shop1x2Variants, [new Vector2Int(2, 2)] = shop2x2Variants
            },
            ["Treasure"] = new Dictionary<Vector2Int, Room[]>
            {
                [new Vector2Int(1, 1)] = treasure1x1Variants, [new Vector2Int(2, 1)] = treasure2x1Variants,
                [new Vector2Int(1, 2)] = treasure1x2Variants, [new Vector2Int(2, 2)] = treasure2x2Variants
            },
            ["Curch"] = new Dictionary<Vector2Int, Room[]>
            {
                [new Vector2Int(1, 1)] = curch1x1Variants, [new Vector2Int(2, 2)] = curch2x2Variants
            },
            ["EvilCurch"] = new Dictionary<Vector2Int, Room[]>
            {
                [new Vector2Int(1, 1)] = evilCurch1x1Variants, [new Vector2Int(2, 2)] = evilCurch2x2Variants
            }
        };
    }

    void SpawnDungeon(List<VirtualRoom> layout)
    {
        foreach (var vr in layout)
        {
            Vector3 worldPos = new Vector3(vr.anchorPos.x * xOffset, 0, vr.anchorPos.y * zOffset);
            Room instance = Instantiate(vr.prefabReference, worldPos, Quaternion.identity);
            instance.transform.parent = transform;
            instance.name = $"{vr.type}_{vr.anchorPos}";

            instance.roomData.size = vr.size;
            if (vr.type == "Start") startRoomInstance = instance;

            activeRoomObjects.Add(instance);
        }
    }

    void ConnectDoors()
    {
        var gridLookup = new Dictionary<Vector2Int, Room>();
        foreach (Room r in activeRoomObjects)
        {
            Vector2Int anchor = GetGridPos(r.transform.position);
            Vector2Int size = (r.roomData.size == Vector2Int.zero) ? Vector2Int.one : r.roomData.size;
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                    gridLookup[anchor + new Vector2Int(x, y)] = r;
        }

        foreach (Room r in activeRoomObjects)
        {
            Vector2Int anchor = GetGridPos(r.transform.position);
            Vector2Int size = (r.roomData.size == Vector2Int.zero) ? Vector2Int.one : r.roomData.size;
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    Vector2Int current = anchor + new Vector2Int(x, y);
                    foreach (Vector2Int dir in directions)
                    {
                        if (gridLookup.TryGetValue(current + dir, out Room neighbor) && neighbor != r)
                            r.OpenDoor(new Vector2Int(x, y), dir);
                    }
                }
            }
        }
    }

    #endregion

    #region --- Utilities ---

    void CleanupScene()
    {
        foreach (var r in activeRoomObjects) if (r != null) Destroy(r.gameObject);
        activeRoomObjects.Clear();
        if (MinimapManager.instance) MinimapManager.instance.ClearMap();
    }

    void InitializeMinimap()
    {
        if (!MinimapManager.instance) return;
        foreach (Room r in activeRoomObjects) MinimapManager.instance.RegisterRoom(GetGridPos(r.transform.position), r.roomData);
    }

    int GetManhattanDist(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    Vector2Int GetGridPos(Vector3 pos) => new Vector2Int(Mathf.RoundToInt(pos.x / xOffset), Mathf.RoundToInt(pos.z / zOffset));

    void RespawnPlayerAtStart()
    {
        if (playerTransform == null) return;
        
        Room startRoom = startRoomInstance ?? activeRoomObjects.FirstOrDefault(r => r != null && r.roomData != null && r.roomData.isStartRoom);
        if (startRoom == null) startRoom = activeRoomObjects.FirstOrDefault(r => r != null);
        
        if (startRoom == null)
        {
            Debug.LogError("[CoreGenerator] Nessuna stanza trovata per lo spawn. Teletrasporto all'origine.");
            TeleportPlayer(playerSpawnOffset, Quaternion.identity);
            return;
        }

        Vector3 basePos = startRoom.playerSpawnPoint != null ? startRoom.playerSpawnPoint.position : startRoom.transform.position + Vector3.up;
        Quaternion baseRot = startRoom.playerSpawnPoint != null ? startRoom.playerSpawnPoint.rotation : startRoom.transform.rotation;
        Vector3 targetPos = basePos + playerSpawnOffset;
        
        if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out var hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            targetPos = hit.position;
        }
        else if (Physics.Raycast(targetPos + Vector3.up * 5f, Vector3.down, out var rayHit, 20f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            targetPos = rayHit.point;
        }

        TeleportPlayer(targetPos, baseRot);

        // --- FOG OF WAR: Rivela l'area di partenza sulla minimappa ---
        if (MinimapManager.instance != null)
        {
            Vector2Int startGridPos = GetGridPos(startRoom.transform.position);
            MinimapManager.instance.RevealStartingArea(startGridPos);
        }
    }

    void TeleportPlayer(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (playerTransform == null) return;

        CharacterController controller = playerTransform.GetComponent<CharacterController>();
        if (controller != null && controller.enabled)
        {
            controller.enabled = false;
            playerTransform.SetPositionAndRotation(targetPosition, targetRotation);
            controller.enabled = true;
        }
        else
        {
            playerTransform.SetPositionAndRotation(targetPosition, targetRotation);
        }
    }

    string GenerateSeedString()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = new System.Random(Environment.TickCount);
        var s = new char[9];
        for (int i = 0; i < s.Length; i++)
        {
            if (i == 4) s[i] = '-';
            else s[i] = chars[rng.Next(chars.Length)];
        }
        return new string(s);
    }

    int ComputeSeedHash(string seed)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in seed)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }
    }

    #endregion
}