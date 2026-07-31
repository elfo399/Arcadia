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

    [Header("Theme Selection")]
    public DungeonFloorThemeTable floorThemeTable;

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
    private bool savedCheckpointApplied;
    private DungeonThemeDefinition activeThemeDefinition;
    private DungeonRoomSet activeRoomSet;

    public event Action<int, string> FloorThemeChanged;
    public event Action<int> FloorGenerated;
    public int CurrentFloor => currentFloor;
    public DungeonThemeDefinition ActiveThemeDefinition => activeThemeDefinition;
    public string ActiveThemeDisplayName => GetThemeDisplayName(activeThemeDefinition);

    // Lookup ricostruito in base al tema attivo del piano.
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
        ResolvePlayerTransform();
        CachePlayerStats();
        ApplySavedCheckpointIfAvailable();
        if (playerStats == null) Debug.LogWarning("[CoreGenerator] PlayerStats non trovato! La generazione di stanze speciali (Curch/EvilCurch) non funzionerà.");
        
    }
    
    void Start()
    {
        CachePlayerStats();
        ApplySavedCheckpointIfAvailable();
        Generate();
    }
    
    void Update() 
    { 
        if (MinimapManager.instance && playerTransform) 
            MinimapManager.instance.UpdatePlayerPosition(playerTransform.position, xOffset); 
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region --- Core Generation Logic ---

    private void CachePlayerStats()
    {
        if (playerStats == null)
            playerStats = PlayerStats.instance;
    }

    private void ApplySavedCheckpointIfAvailable()
    {
        if (savedCheckpointApplied)
            return;

        if (playerStats == null || !playerStats.TryGetDungeonCheckpoint(out int savedFloor, out string savedSeed))
            return;

        savedCheckpointApplied = true;
        currentFloor = Mathf.Clamp(savedFloor, 1, Mathf.Max(1, maxFloors));
        if (!string.IsNullOrWhiteSpace(savedSeed))
        {
            gameSeedString = savedSeed;
            useRandomSeed = false;
        }

        Debug.Log($"[CoreGenerator] Ripristino checkpoint: piano {currentFloor}, seed '{gameSeedString}'.");
    }

    public void Generate()
    {
        CachePlayerStats();
        ResolvePlayerTransform();

        if (useRandomSeed)
        {
            gameSeedString = GenerateSeedString();
            useRandomSeed = false;
        }
        
        string floorSeedString = $"{gameSeedString}-{currentFloor}";
        currentMasterSeed = ComputeSeedHash(floorSeedString);
        if (showRngLogs) Debug.Log($"[CoreGenerator] Seed per piano {currentFloor}: '{floorSeedString}' -> Hash: {currentMasterSeed}");

        ResolveActiveThemeForCurrentFloor();
        if (!ValidateActiveThemeConfiguration(out string configError))
        {
            Debug.LogError($"[CoreGenerator] Configurazione tema non valida per il piano {currentFloor}: {configError}");
            return;
        }

        InitializePrefabLookup();

        Room effectiveStartRoomPrefab = GetStartRoomPrefab();
        if (effectiveStartRoomPrefab == null || effectiveStartRoomPrefab.roomData == null)
        {
            Debug.LogError("[CoreGenerator] StartRoomPrefab o RoomData mancante nel room set attivo.");
            return;
        }

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
            FloorGenerated?.Invoke(currentFloor);
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
            CachePlayerStats();
            if (playerStats != null)
            {
                playerStats.ClearDungeonCheckpoint();
                playerStats.SaveStatsImmediate();
            }

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
        AddRoomToLayout(layout, occupiedCells, Vector2Int.zero, new Vector2Int(1, 1), "Start", GetStartRoomPrefab());

        // 2. CORPO CENTRALE
        List<VirtualRoom> expandableRooms = new List<VirtualRoom> { layout[0] };
        int normalCount = 0;
        int consecutiveFailedNormalPlacements = 0;
        int maxNormalPlacementFailures = Mathf.Max(50, totalNormalRooms * 8);
        while (normalCount < totalNormalRooms && expandableRooms.Count > 0)
        {
            VirtualRoom origin = expandableRooms[prng.Next(expandableRooms.Count)];
            Vector2Int dir = directions[prng.Next(directions.Length)];
            Vector2Int potentialAnchor = origin.anchorPos + dir;

            List<Vector2Int> sizesToTry = GetSizesToTry(normalBigRoomChance, "Normal");
            bool placed = false;

            foreach (var size in sizesToTry)
            {
                if (CanFit(potentialAnchor, size, occupiedCells, false))
                {
                    Room prefab = GetRandomPrefab("Normal", size);
                    if (prefab != null)
                    {
                        LogRoomPlacement("Normal", size, potentialAnchor);
                        VirtualRoom newRoom = AddRoomToLayout(layout, occupiedCells, potentialAnchor, size, "Normal", prefab);
                        expandableRooms.Add(newRoom);
                        normalCount++;
                        consecutiveFailedNormalPlacements = 0;
                        placed = true;
                        break; 
                    }
                }
            }

            if (!placed)
            {
                consecutiveFailedNormalPlacements++;
                if (consecutiveFailedNormalPlacements >= maxNormalPlacementFailures)
                    return null;
            }
        }

        if (normalCount < totalNormalRooms)
            return null;

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

    bool PlaceSpecialRoom(string type, List<VirtualRoom> layout, HashSet<Vector2Int> occupied, List<VirtualRoom> replacementCandidates, List<Vector2Int> freeSockets, int minDistance, int bigRoomChance, Dictionary<Vector2Int, VirtualRoom> cellToRoomMap)
    {
        var sizesToTry = GetSizesToTry(bigRoomChance, type);

        // --- Strategia 1: Sostituzione di una stanza interna (vicolo cieco) ---
        foreach (var roomToReplace in replacementCandidates.ToList()) 
        {
            if (minDistance > 0 && GetManhattanDist(Vector2Int.zero, roomToReplace.anchorPos) < minDistance) continue;
            if (avoidBossTouchingSpecials && type == "Boss" && IsTouchingRestrictedRoom(roomToReplace.anchorPos, roomToReplace.size, cellToRoomMap, roomToReplace)) continue;
            if (IsTouchingAnySpecialRoom(roomToReplace.anchorPos, roomToReplace.size, cellToRoomMap, roomToReplace)) continue;
            if (!sizesToTry.Contains(roomToReplace.size)) continue;
            
            Room prefab = GetRandomPrefab(type, roomToReplace.size);
            if (prefab != null)
            {
                TemporarilyRemoveRoom(roomToReplace, layout, occupied, cellToRoomMap);
                LogRoomPlacement(type, roomToReplace.size, roomToReplace.anchorPos);
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
                        LogRoomPlacement(type, size, spot);
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
        bool forceBigOnly = chancePercent >= 100;
        bool tryBig = forceBigOnly || prng.Next(0, 100) < chancePercent;

        if (tryBig)
        {
            sizes.AddRange(bigSizes.OrderBy(x => prng.Next()));
        }

        if (!forceBigOnly)
            sizes.Add(new Vector2Int(1, 1)); // Fallback solo se non sto forzando big al 100%

        return sizes;
    }

    private void LogRoomPlacement(string roomType, Vector2Int size, Vector2Int anchor)
    {
        if (!showRngLogs)
            return;

        string label;
        string color;

        if (size == new Vector2Int(2, 2))
        {
            label = "GRANDE";
            color = "green";
        }
        else if (size == new Vector2Int(2, 1))
        {
            label = "LONG";
            color = "orange";
        }
        else if (size == new Vector2Int(1, 2))
        {
            label = "TALL";
            color = "yellow";
        }
        else
        {
            label = "Piccola";
            color = "grey";
        }

        Debug.Log($"[RNG] Piazzo stanza <b>{roomType}</b> -> <color={color}>{label}</color> ({size.x}x{size.y}) @ {anchor}");
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

    private void ResolveActiveThemeForCurrentFloor()
    {
        activeThemeDefinition = SelectThemeForFloor(currentFloor, currentMasterSeed);
        activeRoomSet = activeThemeDefinition != null ? activeThemeDefinition.roomSet : null;
        string themeLabel = ActiveThemeDisplayName;

        if (activeThemeDefinition != null && activeRoomSet != null)
        {
            Debug.Log($"[CoreGenerator] Piano {currentFloor}: tema selezionato '{themeLabel}' | RoomSet: '{activeRoomSet.name}'");
        }
        else
        {
            Debug.Log($"[CoreGenerator] Piano {currentFloor}: nessun tema valido trovato, uso i pool legacy del CoreGenerator.");
        }

        FloorThemeChanged?.Invoke(currentFloor, themeLabel);
    }

    public static string GetThemeDisplayName(DungeonThemeDefinition themeDefinition)
    {
        if (themeDefinition == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(themeDefinition.displayName)
            ? themeDefinition.name
            : themeDefinition.displayName;
    }

    private bool ValidateActiveThemeConfiguration(out string error)
    {
        if (floorThemeTable == null)
        {
            error = "DungeonFloorThemeTable non assegnata.";
            return false;
        }

        if (activeThemeDefinition == null)
        {
            error = "nessun tema valido trovato per questo piano.";
            return false;
        }

        if (activeRoomSet == null)
        {
            error = $"il tema '{activeThemeDefinition.name}' non ha un DungeonRoomSet assegnato.";
            return false;
        }

        if (activeRoomSet.startRoomPrefab == null || activeRoomSet.startRoomPrefab.roomData == null)
        {
            error = $"il room set '{activeRoomSet.name}' non ha uno Start valido.";
            return false;
        }

        if (!HasAnyVariant(activeRoomSet.normal1x1Variants, activeRoomSet.normal2x1Variants, activeRoomSet.normal1x2Variants, activeRoomSet.normal2x2Variants))
        {
            error = $"il room set '{activeRoomSet.name}' non ha varianti Normal.";
            return false;
        }

        if (!HasAnyVariant(activeRoomSet.boss1x1Variants, activeRoomSet.boss2x1Variants, activeRoomSet.boss1x2Variants, activeRoomSet.boss2x2Variants))
        {
            error = $"il room set '{activeRoomSet.name}' non ha varianti Boss.";
            return false;
        }

        if (!HasAnyVariant(activeRoomSet.shop1x1Variants, activeRoomSet.shop2x1Variants, activeRoomSet.shop1x2Variants, activeRoomSet.shop2x2Variants))
        {
            error = $"il room set '{activeRoomSet.name}' non ha varianti Shop.";
            return false;
        }

        if (!HasAnyVariant(activeRoomSet.treasure1x1Variants, activeRoomSet.treasure2x1Variants, activeRoomSet.treasure1x2Variants, activeRoomSet.treasure2x2Variants))
        {
            error = $"il room set '{activeRoomSet.name}' non ha varianti Treasure.";
            return false;
        }

        error = null;
        return true;
    }

    private DungeonThemeDefinition SelectThemeForFloor(int floor, int seed)
    {
        if (floorThemeTable == null)
            return null;

        DungeonFloorThemeTable.FloorThemeEntry entry = floorThemeTable.GetEntryForFloor(floor);
        if (entry == null || entry.themes == null || entry.themes.Count == 0)
            return null;

        int totalWeight = 0;
        for (int i = 0; i < entry.themes.Count; i++)
        {
            DungeonFloorThemeTable.ThemeChoice choice = entry.themes[i];
            if (choice == null || choice.theme == null || choice.theme.roomSet == null)
                continue;

            totalWeight += Mathf.Max(1, choice.weight);
        }

        if (totalWeight <= 0)
            return null;

        var themePrng = new System.Random(seed ^ (floor * 486187739));
        int roll = themePrng.Next(totalWeight);

        for (int i = 0; i < entry.themes.Count; i++)
        {
            DungeonFloorThemeTable.ThemeChoice choice = entry.themes[i];
            if (choice == null || choice.theme == null || choice.theme.roomSet == null)
                continue;

            roll -= Mathf.Max(1, choice.weight);
            if (roll < 0)
                return choice.theme;
        }

        return null;
    }

    private Room GetStartRoomPrefab()
    {
        return activeRoomSet != null ? activeRoomSet.startRoomPrefab : null;
    }

    private Dictionary<Vector2Int, Room[]> BuildSizeMap(Room[] oneByOne, Room[] twoByOne, Room[] oneByTwo, Room[] twoByTwo)
    {
        return new Dictionary<Vector2Int, Room[]>
        {
            [new Vector2Int(1, 1)] = oneByOne,
            [new Vector2Int(2, 1)] = twoByOne,
            [new Vector2Int(1, 2)] = oneByTwo,
            [new Vector2Int(2, 2)] = twoByTwo
        };
    }

    private bool HasAnyVariant(params Room[][] groups)
    {
        if (groups == null)
            return false;

        for (int i = 0; i < groups.Length; i++)
        {
            Room[] variants = groups[i];
            if (variants == null || variants.Length == 0)
                continue;

            for (int j = 0; j < variants.Length; j++)
            {
                if (variants[j] != null)
                    return true;
            }
        }

        return false;
    }

    private void InitializePrefabLookup()
    {
        _prefabLookup = new Dictionary<string, Dictionary<Vector2Int, Room[]>>
        {
            ["Normal"] = BuildSizeMap(
                activeRoomSet.normal1x1Variants,
                activeRoomSet.normal2x1Variants,
                activeRoomSet.normal1x2Variants,
                activeRoomSet.normal2x2Variants),
            ["Boss"] = BuildSizeMap(
                activeRoomSet.boss1x1Variants,
                activeRoomSet.boss2x1Variants,
                activeRoomSet.boss1x2Variants,
                activeRoomSet.boss2x2Variants),
            ["Shop"] = BuildSizeMap(
                activeRoomSet.shop1x1Variants,
                activeRoomSet.shop2x1Variants,
                activeRoomSet.shop1x2Variants,
                activeRoomSet.shop2x2Variants),
            ["Treasure"] = BuildSizeMap(
                activeRoomSet.treasure1x1Variants,
                activeRoomSet.treasure2x1Variants,
                activeRoomSet.treasure1x2Variants,
                activeRoomSet.treasure2x2Variants),
            ["Curch"] = new Dictionary<Vector2Int, Room[]>
            {
                [new Vector2Int(1, 1)] = activeRoomSet.curch1x1Variants,
                [new Vector2Int(2, 2)] = activeRoomSet.curch2x2Variants
            },
            ["EvilCurch"] = new Dictionary<Vector2Int, Room[]>
            {
                [new Vector2Int(1, 1)] = activeRoomSet.evilCurch1x1Variants,
                [new Vector2Int(2, 2)] = activeRoomSet.evilCurch2x2Variants
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
        ResolvePlayerTransform();
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
        ResolvePlayerTransform();
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

    private void ResolvePlayerTransform()
    {
        // Priorita' assoluta al player persistente gia' registrato nel singleton.
        if (PlayerStats.instance != null)
        {
            Transform persistentRoot = PlayerStats.instance.transform.root;
            if (persistentRoot != null)
            {
                CharacterController persistentController = persistentRoot.GetComponentInChildren<CharacterController>(true);
                if (persistentController != null)
                {
                    if (playerTransform == persistentController.transform)
                        return;

                    playerTransform = persistentController.transform;
                    return;
                }
            }
        }

        if (playerTransform != null && playerTransform.GetComponent<CharacterController>() != null)
            return;

        if (playerTransform == null)
            Debug.LogWarning("[CoreGenerator] Player Transform non assegnato e PlayerStats.instance non disponibile.");
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


