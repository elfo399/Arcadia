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
    [Range(0, 100)] public int waveBigRoomChance = 0;
    [Range(0, 100)] public int challengeBigRoomChance = 0;
    [Range(0, 100)] public int minibossBigRoomChance = 0;
    [Range(0, 100)] public int parkourBigRoomChance = 0;
    [Range(0, 100)] public int narrativeBigRoomChance = 0;
    [Range(0, 100)] public int npcEncounterBigRoomChance = 0;
    [Range(0, 100)] public int secretAccessBigRoomChance = 0;

    [Header("Regole Distanza & Adiacenza")]
    [Tooltip("Distanza minima (celle) dallo Start per il Boss.")]
    public int minBossDistance = 4;
    [Tooltip("Se TRUE, il Boss non spawnerà MAI attaccato a Shop o Treasure.")]
    public bool avoidBossTouchingSpecials = true;
    [Tooltip("Se TRUE, il Boss avrà sempre e solo UN ingresso (Vicolo Cieco).")]
    public bool bossMustBeDeadEnd = true;
    
    [Header("Debug")]
    public bool showRngLogs = true;

    [Header("Debug / Testing")]
    [Tooltip("TEST ONLY: when GameScene starts, ignore and clear any saved dungeon checkpoint, reset to floor 1, and generate a fresh random seed.")]
    public bool forceNewRunOnStartForTesting = false;

    #endregion

    #region --- Strutture Dati Interne ---

    private class VirtualRoom
    {
        public Vector2Int anchorPos;
        public Vector2Int size;
        public RoomType roomType;
        public Room prefabReference;

        public bool Contains(Vector2Int point)
        {
            return point.x >= anchorPos.x && point.x < anchorPos.x + size.x &&
                   point.y >= anchorPos.y && point.y < anchorPos.y + size.y;
        }
    }

    // A prefab pool is an authoring selector, not a graph role. The two
    // SecretAccess pools deliberately share RoomType.SecretAccess.
    private enum RoomPoolKey
    {
        Normal, Shop, Treasure, Wave, Challenge, Miniboss, Parkour,
        Narrative, NpcEncounter, SecretAccessSecret, SecretAccessSuperSecret,
        Curch, EvilCurch, Boss
    }

    private List<Room> activeRoomObjects = new List<Room>();
    private Room startRoomInstance;
    private System.Random prng;
    private PlayerStats playerStats;
    private bool savedCheckpointApplied;
    private bool resumingSavedRun;
    private SavedDungeonRunState resumeRunState;
    private DungeonThemeDefinition activeThemeDefinition;
    private DungeonRoomSet activeRoomSet;
    private DungeonFloorDefinition activeFloorDefinition;
    private DungeonRunStateController runStateController;
    private int activeNormalRoomCount;
    private string pendingThemeOverrideId;

    public event Action<int, string> FloorThemeChanged;
    public event Action<int> FloorGenerated;
    public int CurrentFloor => currentFloor;
    public DungeonThemeDefinition ActiveThemeDefinition => activeThemeDefinition;
    public DungeonFloorDefinition ActiveFloorDefinition => activeFloorDefinition;
    public string ActiveThemeDisplayName => GetThemeDisplayName(activeThemeDefinition);
    public IReadOnlyList<Room> ActiveRooms => activeRoomObjects;

    // Lookup ricostruito in base al tema attivo del piano.
    private Dictionary<RoomPoolKey, Dictionary<Vector2Int, Room[]>> _prefabLookup;

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
        runStateController = GetComponent<DungeonRunStateController>();
        if (runStateController == null) runStateController = gameObject.AddComponent<DungeonRunStateController>();
        if (GetComponent<RunModifierController>() == null) gameObject.AddComponent<RunModifierController>();
        ResolvePlayerTransform();
        CachePlayerStats();
        if (!forceNewRunOnStartForTesting)
            ApplySavedCheckpointIfAvailable();
        if (playerStats == null) Debug.LogWarning("[CoreGenerator] PlayerStats non trovato! La generazione di stanze speciali (Curch/EvilCurch) non funzionerà.");
        
    }
    
    void Start()
    {
        CachePlayerStats();
        if (forceNewRunOnStartForTesting)
        {
            PrepareNewRunForGeneration(forcedForTesting: true);
        }
        else
        {
            ApplySavedCheckpointIfAvailable();
            if (!resumingSavedRun)
                PrepareNewRunForGeneration(forcedForTesting: false);
        }
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

    private void PrepareNewRunForGeneration(bool forcedForTesting)
    {
        resumingSavedRun = false;
        resumeRunState = null;
        currentFloor = 1;
        gameSeedString = string.Empty;
        useRandomSeed = true;
        pendingThemeOverrideId = null;

        runStateController?.ClearRun();
        playerStats?.BeginNewDungeonRun();

        if (forcedForTesting)
            Debug.Log("[DungeonLifecycle] TEST override: ignored saved checkpoint and forced NEW run with a fresh seed.");
    }

    private void ApplySavedCheckpointIfAvailable()
    {
        if (savedCheckpointApplied)
            return;

        if (playerStats == null || !playerStats.HasActiveDungeonCheckpoint)
            return;

        if (!playerStats.TryGetDungeonResumeCheckpoint(out int savedFloor, out string savedSeed, out SavedDungeonRunState savedRun))
        {
            Debug.LogWarning("[DungeonLifecycle] Discarded invalid dungeon resume checkpoint; starting a new run.");
            playerStats.ClearDungeonResumeState(save: true);
            return;
        }

        savedCheckpointApplied = true;
        resumingSavedRun = true;
        resumeRunState = savedRun;
        currentFloor = Mathf.Clamp(savedFloor, 1, Mathf.Max(1, maxFloors));
        if (!string.IsNullOrWhiteSpace(savedSeed))
        {
            gameSeedString = savedSeed;
            useRandomSeed = false;
        }

        Debug.Log($"[DungeonLifecycle] Resume run seed '{gameSeedString}' floor {currentFloor}.");
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

        if (runStateController == null) runStateController = GetComponent<DungeonRunStateController>();
        SavedDungeonRunState savedRun = !runStateController.IsInitialized && resumingSavedRun ? resumeRunState : null;
        runStateController.InitializeFromSave(gameSeedString, currentFloor, savedRun);
        RunModifierController.Active?.RestoreFromRunState();

        ResolveActiveThemeForCurrentFloor();
        if (!ValidateActiveThemeConfiguration(out string configError))
        {
            Debug.LogError($"[CoreGenerator] Configurazione tema non valida per il piano {currentFloor}: {configError}");
            return;
        }

        InitializePrefabLookup();
        activeNormalRoomCount = ResolveNormalRoomCount();

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
            playerStats?.SaveDungeonResumeCheckpoint(currentFloor, gameSeedString);
            playerStats?.SaveStatsImmediate();
            FloorGenerated?.Invoke(currentFloor);
        }
        else
        {
            Debug.LogError("CRITICO: Impossibile generare dungeon. Le regole di piazzamento sono troppo strette.");
        }
    }

    public void NextFloor(int targetFloorNumber = 0, string targetThemeId = null)
    {
        int destination = targetFloorNumber > 0 ? Mathf.Clamp(targetFloorNumber, 1, Mathf.Max(1, maxFloors)) : currentFloor + 1;
        if (destination > maxFloors || (targetFloorNumber <= 0 && currentFloor >= maxFloors))
        {
            CachePlayerStats();
            if (playerStats != null)
            {
                if (!playerStats.TryCompleteRun())
                {
                    Debug.LogError("[CoreGenerator] Completamento run fallito: loot non depositato.");
                    return;
                }
            }

            SceneManager.LoadScene(hubSceneName);
        }
        else
        {
            currentFloor = destination;
            pendingThemeOverrideId = string.IsNullOrWhiteSpace(targetThemeId) ? null : targetThemeId.Trim();
            Generate();
        }
    }

    private List<VirtualRoom> TryBuildVirtualLayout()
    {
        HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
        List<VirtualRoom> layout = new List<VirtualRoom>();

        // 1. START
        AddRoomToLayout(layout, occupiedCells, Vector2Int.zero, new Vector2Int(1, 1), RoomType.Start, GetStartRoomPrefab());

        // 2. CORPO CENTRALE
        List<VirtualRoom> expandableRooms = new List<VirtualRoom> { layout[0] };
        int normalCount = 0;
        int consecutiveFailedNormalPlacements = 0;
        int maxNormalPlacementFailures = Mathf.Max(50, activeNormalRoomCount * 8);
        while (normalCount < activeNormalRoomCount && expandableRooms.Count > 0)
        {
            VirtualRoom origin = expandableRooms[prng.Next(expandableRooms.Count)];
            Vector2Int dir = directions[prng.Next(directions.Length)];
            Vector2Int potentialAnchor = origin.anchorPos + dir;

            List<Vector2Int> sizesToTry = GetSizesToTry(normalBigRoomChance, RoomPoolKey.Normal);
            bool placed = false;

            foreach (var size in sizesToTry)
            {
                if (CanFit(potentialAnchor, size, occupiedCells, false))
                {
                    Room prefab = GetRandomPrefab(RoomPoolKey.Normal, size);
                    if (prefab != null)
                    {
                        LogRoomPlacement(RoomType.Normal, size, potentialAnchor);
                        VirtualRoom newRoom = AddRoomToLayout(layout, occupiedCells, potentialAnchor, size, RoomType.Normal, prefab);
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

        if (normalCount < activeNormalRoomCount)
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
        // Keep this order deterministic; Boss retains priority over optional categories.
        if (!PlaceSpecialRooms(RoomType.Shop, RoomPoolKey.Shop, ResolveSpecialRoomCount(RoomType.Shop), layout, occupiedCells, deadEndNormalRooms, freeSockets, -1, shopBigRoomChance, cellToRoomMap)) return null;
        if (!PlaceSpecialRooms(RoomType.Treasure, RoomPoolKey.Treasure, ResolveSpecialRoomCount(RoomType.Treasure), layout, occupiedCells, deadEndNormalRooms, freeSockets, -1, treasureBigRoomChance, cellToRoomMap)) return null;
        if (!PlaceSpecialRooms(RoomType.Boss, RoomPoolKey.Boss, ResolveSpecialRoomCount(RoomType.Boss), layout, occupiedCells, deadEndNormalRooms, freeSockets, minBossDistance, bossBigRoomChance, cellToRoomMap)) return null;
        if (!PlaceSpecialRooms(RoomType.Miniboss, RoomPoolKey.Miniboss, ResolveSpecialRoomCount(RoomType.Miniboss), layout, occupiedCells, deadEndNormalRooms, freeSockets, 0, minibossBigRoomChance, cellToRoomMap)) return null;
        if (!PlaceSpecialRooms(RoomType.Wave, RoomPoolKey.Wave, ResolveSpecialRoomCount(RoomType.Wave), layout, occupiedCells, deadEndNormalRooms, freeSockets, 0, waveBigRoomChance, cellToRoomMap)) return null;
        if (!PlaceSpecialRooms(RoomType.Challenge, RoomPoolKey.Challenge, ResolveSpecialRoomCount(RoomType.Challenge), layout, occupiedCells, deadEndNormalRooms, freeSockets, 0, challengeBigRoomChance, cellToRoomMap)) return null;
        if (!PlaceSpecialRooms(RoomType.Parkour, RoomPoolKey.Parkour, ResolveSpecialRoomCount(RoomType.Parkour), layout, occupiedCells, deadEndNormalRooms, freeSockets, 0, parkourBigRoomChance, cellToRoomMap)) return null;
        if (!PlaceSpecialRooms(RoomType.Narrative, RoomPoolKey.Narrative, ResolveSpecialRoomCount(RoomType.Narrative), layout, occupiedCells, deadEndNormalRooms, freeSockets, 0, narrativeBigRoomChance, cellToRoomMap)) return null;
        if (!PlaceSpecialRooms(RoomType.NpcEncounter, RoomPoolKey.NpcEncounter, ResolveSpecialRoomCount(RoomType.NpcEncounter), layout, occupiedCells, deadEndNormalRooms, freeSockets, 0, npcEncounterBigRoomChance, cellToRoomMap)) return null;
        if (!PlaceSpecialRooms(RoomType.SecretAccess, RoomPoolKey.SecretAccessSecret, ResolveSecretAccessRoomCount(false), layout, occupiedCells, deadEndNormalRooms, freeSockets, 0, secretAccessBigRoomChance, cellToRoomMap)) return null;
        if (!PlaceSpecialRooms(RoomType.SecretAccess, RoomPoolKey.SecretAccessSuperSecret, ResolveSecretAccessRoomCount(true), layout, occupiedCells, deadEndNormalRooms, freeSockets, 0, secretAccessBigRoomChance, cellToRoomMap)) return null;

        // Floor definitions explicitly control shrine categories. Legacy floors retain their morality-gated chance.
        if (activeFloorDefinition != null)
        {
            int curchCount = ResolveSpecialRoomCount(RoomType.Curch);
            int evilCurchCount = ResolveSpecialRoomCount(RoomType.EvilCurch);
            if (activeFloorDefinition.moralRoomPolicy == DungeonFloorDefinition.DungeonMoralRoomPolicy.AlignmentExclusive)
            {
                int blessed = playerStats != null ? playerStats.benedetto : 0;
                int evil = playerStats != null ? playerStats.malefico : 0;
                if (blessed > evil) evilCurchCount = 0;
                else if (evil > blessed) curchCount = 0;
                else { curchCount = 0; evilCurchCount = 0; }
            }
            if (!PlaceSpecialRooms(RoomType.Curch, RoomPoolKey.Curch, curchCount, layout, occupiedCells, deadEndNormalRooms, freeSockets, 0, curchBigRoomChance, cellToRoomMap)) return null;
            if (!PlaceSpecialRooms(RoomType.EvilCurch, RoomPoolKey.EvilCurch, evilCurchCount, layout, occupiedCells, deadEndNormalRooms, freeSockets, 0, evilCurchBigRoomChance, cellToRoomMap)) return null;
        }
        else if (playerStats != null && playerStats.benedetto != playerStats.malefico && prng.Next(0, 100) <= curchsRoomsChance)
        {
            RoomType curchType = playerStats.benedetto > playerStats.malefico ? RoomType.Curch : RoomType.EvilCurch;
            int curchChance = playerStats.benedetto > playerStats.malefico ? curchBigRoomChance : evilCurchBigRoomChance;
            PlaceSpecialRoom(curchType, curchType == RoomType.Curch ? RoomPoolKey.Curch : RoomPoolKey.EvilCurch, layout, occupiedCells, deadEndNormalRooms, freeSockets, 0, curchChance, cellToRoomMap);
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
        var normalRooms = layout.Where(r => r.roomType == RoomType.Normal).ToList();
        
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

    bool PlaceSpecialRoom(RoomType roomType, RoomPoolKey poolKey, List<VirtualRoom> layout, HashSet<Vector2Int> occupied, List<VirtualRoom> replacementCandidates, List<Vector2Int> freeSockets, int minDistance, int bigRoomChance, Dictionary<Vector2Int, VirtualRoom> cellToRoomMap)
    {
        var sizesToTry = GetSizesToTry(bigRoomChance, poolKey);

        // --- Strategia 1: Sostituzione di una stanza interna (vicolo cieco) ---
        foreach (var roomToReplace in replacementCandidates.ToList()) 
        {
            if (minDistance > 0 && GetManhattanDist(Vector2Int.zero, roomToReplace.anchorPos) < minDistance) continue;
            if (avoidBossTouchingSpecials && roomType == RoomType.Boss && IsTouchingRestrictedRoom(roomToReplace.anchorPos, roomToReplace.size, cellToRoomMap, roomToReplace)) continue;
            if (IsTouchingAnySpecialRoom(roomToReplace.anchorPos, roomToReplace.size, cellToRoomMap, roomToReplace)) continue;
            if (!sizesToTry.Contains(roomToReplace.size)) continue;
            
            Room prefab = GetRandomPrefab(poolKey, roomToReplace.size);
            if (prefab != null)
            {
                TemporarilyRemoveRoom(roomToReplace, layout, occupied, cellToRoomMap);
                LogRoomPlacement(roomType, roomToReplace.size, roomToReplace.anchorPos);
                AddRoomToLayout(layout, occupied, roomToReplace.anchorPos, roomToReplace.size, roomType, prefab, cellToRoomMap);
                replacementCandidates.Remove(roomToReplace);
                return true; 
            }
        }

        // --- Strategia 2: Aggiunta su un bordo esterno (fallback) ---
        bool isStrictDeadEnd = roomType == RoomType.Boss && bossMustBeDeadEnd;
        foreach (var spot in freeSockets.ToList())
        {
            if (minDistance > 0 && GetManhattanDist(Vector2Int.zero, spot) < minDistance) continue;

            foreach (var size in sizesToTry)
            {
                if (CanFit(spot, size, occupied, isStrictDeadEnd))
                {
                    if (avoidBossTouchingSpecials && roomType == RoomType.Boss && IsTouchingRestrictedRoom(spot, size, cellToRoomMap, null)) continue;
                    if (IsTouchingAnySpecialRoom(spot, size, cellToRoomMap, null)) continue;

                    Room prefab = GetRandomPrefab(poolKey, size);
                    if (prefab != null)
                    {
                        LogRoomPlacement(roomType, size, spot);
                        AddRoomToLayout(layout, occupied, spot, size, roomType, prefab, cellToRoomMap);
                        freeSockets.Remove(spot);
                        return true;
                    }
                }
            }
        }
        
        // Callers that intentionally treat a shrine as optional simply ignore this
        // result. A floor definition with a positive minimum must instead fail the
        // attempt so it can never silently generate fewer requested rooms.
        return false;
    }

    private bool PlaceSpecialRooms(RoomType roomType, RoomPoolKey poolKey, int count, List<VirtualRoom> layout, HashSet<Vector2Int> occupied, List<VirtualRoom> replacementCandidates, List<Vector2Int> freeSockets, int minDistance, int bigRoomChance, Dictionary<Vector2Int, VirtualRoom> cellToRoomMap)
    {
        for (int i = 0; i < count; i++)
            if (!PlaceSpecialRoom(roomType, poolKey, layout, occupied, replacementCandidates, freeSockets, minDistance, bigRoomChance, cellToRoomMap))
                return false;
        return true;
    }

    private static bool IsSpecialRoomType(RoomType roomType)
    {
        return roomType != RoomType.Start && roomType != RoomType.Normal;
    }

    bool IsTouchingAnySpecialRoom(Vector2Int anchor, Vector2Int size, Dictionary<Vector2Int, VirtualRoom> cellToRoomMap, VirtualRoom roomToIgnore)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int cell = anchor + new Vector2Int(x, y);
                foreach (var dir in directions)
                {
                    if (cellToRoomMap.TryGetValue(cell + dir, out var neighborRoom) && neighborRoom != roomToIgnore && IsSpecialRoomType(neighborRoom.roomType))
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
                    if (cellToRoomMap.TryGetValue(cell + dir, out var neighborRoom) && neighborRoom != roomToIgnore && (neighborRoom.roomType == RoomType.Shop || neighborRoom.roomType == RoomType.Treasure))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
    
    List<Vector2Int> GetSizesToTry(int chancePercent, RoomPoolKey poolKey)
    {
        var sizes = new List<Vector2Int>();
        bool forceBigOnly = chancePercent >= 100;
        bool tryBig = forceBigOnly || prng.Next(0, 100) < chancePercent;

        if (tryBig)
        {
            sizes.AddRange(bigSizes.OrderBy(x => prng.Next()));
        }

        if (!forceBigOnly)
            sizes.Add(new Vector2Int(1, 1));

        // The probability orders compatible authored sizes; it must not cause a
        // requested category to fail when another populated size is available.
        if (_prefabLookup != null && _prefabLookup.TryGetValue(poolKey, out var sizeMap))
        {
            foreach (Vector2Int size in new[] { new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 2), new Vector2Int(2, 2) })
            {
                if (!sizes.Contains(size) && sizeMap.TryGetValue(size, out var variants) && HasAnyVariant(variants))
                    sizes.Add(size);
            }
        }

        return sizes;
    }

    private void LogRoomPlacement(RoomType roomType, Vector2Int size, Vector2Int anchor)
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

    VirtualRoom AddRoomToLayout(List<VirtualRoom> layout, HashSet<Vector2Int> occupied, Vector2Int anchor, Vector2Int size, RoomType roomType, Room prefab, Dictionary<Vector2Int, VirtualRoom> cellToRoomMap = null)
    {
        var vr = new VirtualRoom { anchorPos = anchor, size = size, roomType = roomType, prefabReference = prefab };
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
    
    Room GetRandomPrefab(RoomPoolKey poolKey, Vector2Int size)
    {
        if (_prefabLookup.TryGetValue(poolKey, out var sizeMap) && sizeMap.TryGetValue(size, out var variants))
        {
            if (variants != null && variants.Length > 0)
            {
                int totalWeight = 0;
                for (int i = 0; i < variants.Length; i++)
                    if (variants[i] != null) totalWeight += Mathf.Max(1, variants[i].roomData != null ? variants[i].roomData.generationWeight : 1);
                if (totalWeight <= 0) return null;
                int roll = prng.Next(totalWeight);
                for (int i = 0; i < variants.Length; i++)
                {
                    Room candidate = variants[i];
                    if (candidate == null) continue;
                    roll -= Mathf.Max(1, candidate.roomData != null ? candidate.roomData.generationWeight : 1);
                    if (roll < 0) return candidate;
                }
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
        DungeonFloorThemeTable.FloorThemeEntry entry = floorThemeTable != null ? floorThemeTable.GetEntryForFloor(currentFloor) : null;
        activeFloorDefinition = activeThemeDefinition != null && activeThemeDefinition.roomComposition != null
            ? activeThemeDefinition.roomComposition
            : entry != null ? entry.floorDefinition : null;
        DungeonRoomSet overrideSet = PickFloorRoomSet(activeFloorDefinition);
        if (overrideSet != null) activeRoomSet = overrideSet;
        string themeLabel = ActiveThemeDisplayName;

        if (activeThemeDefinition != null && activeRoomSet != null)
        {
            string compositionLabel = activeFloorDefinition != null ? activeFloorDefinition.name : "Legacy";
            Debug.Log($"[CoreGenerator] Piano {currentFloor}: tema selezionato '{themeLabel}' | RoomSet: '{activeRoomSet.name}' | Composition: '{compositionLabel}'");
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

        if (ResolveSpecialRoomCount(RoomType.Boss) > 0 && !HasAnyVariant(activeRoomSet.boss1x1Variants, activeRoomSet.boss2x1Variants, activeRoomSet.boss1x2Variants, activeRoomSet.boss2x2Variants))
        {
            error = $"il room set '{activeRoomSet.name}' non ha varianti Boss.";
            return false;
        }

        if (ResolveSpecialRoomCount(RoomType.Shop) > 0 && !HasAnyVariant(activeRoomSet.shop1x1Variants, activeRoomSet.shop2x1Variants, activeRoomSet.shop1x2Variants, activeRoomSet.shop2x2Variants))
        {
            error = $"il room set '{activeRoomSet.name}' non ha varianti Shop.";
            return false;
        }

        if (ResolveSpecialRoomCount(RoomType.Treasure) > 0 && !HasAnyVariant(activeRoomSet.treasure1x1Variants, activeRoomSet.treasure2x1Variants, activeRoomSet.treasure1x2Variants, activeRoomSet.treasure2x2Variants))
        {
            error = $"il room set '{activeRoomSet.name}' non ha varianti Treasure.";
            return false;
        }

        if (ResolveSecretAccessRoomCount(false) > 0 && !HasAnyVariant(activeRoomSet.secretAccessSecret1x1Variants, activeRoomSet.secretAccessSecret2x1Variants, activeRoomSet.secretAccessSecret1x2Variants, activeRoomSet.secretAccessSecret2x2Variants))
        {
            error = $"il room set '{activeRoomSet.name}' non ha varianti SecretAccess/Secret.";
            return false;
        }

        if (ResolveSecretAccessRoomCount(true) > 0 && !HasAnyVariant(activeRoomSet.secretAccessSuperSecret1x1Variants, activeRoomSet.secretAccessSuperSecret2x1Variants, activeRoomSet.secretAccessSuperSecret1x2Variants, activeRoomSet.secretAccessSuperSecret2x2Variants))
        {
            error = $"il room set '{activeRoomSet.name}' non ha varianti SecretAccess/SuperSecret.";
            return false;
        }

        if (ResolveSpecialRoomCount(RoomType.Wave) > 0 && !HasAnyVariant(activeRoomSet.wave1x1Variants, activeRoomSet.wave2x1Variants, activeRoomSet.wave1x2Variants, activeRoomSet.wave2x2Variants)) { error=$"il room set '{activeRoomSet.name}' non ha varianti Wave."; return false; }
        if (ResolveSpecialRoomCount(RoomType.Challenge) > 0 && !HasAnyVariant(activeRoomSet.challenge1x1Variants, activeRoomSet.challenge2x1Variants, activeRoomSet.challenge1x2Variants, activeRoomSet.challenge2x2Variants)) { error=$"il room set '{activeRoomSet.name}' non ha varianti Challenge."; return false; }
        if (ResolveSpecialRoomCount(RoomType.Miniboss) > 0 && !HasAnyVariant(activeRoomSet.miniboss1x1Variants, activeRoomSet.miniboss2x1Variants, activeRoomSet.miniboss1x2Variants, activeRoomSet.miniboss2x2Variants)) { error=$"il room set '{activeRoomSet.name}' non ha varianti Miniboss."; return false; }
        if (ResolveSpecialRoomCount(RoomType.Parkour) > 0 && !HasAnyVariant(activeRoomSet.parkour1x1Variants, activeRoomSet.parkour2x1Variants, activeRoomSet.parkour1x2Variants, activeRoomSet.parkour2x2Variants)) { error=$"il room set '{activeRoomSet.name}' non ha varianti Parkour."; return false; }
        if (ResolveSpecialRoomCount(RoomType.Narrative) > 0 && !HasAnyVariant(activeRoomSet.narrative1x1Variants, activeRoomSet.narrative2x1Variants, activeRoomSet.narrative1x2Variants, activeRoomSet.narrative2x2Variants)) { error=$"il room set '{activeRoomSet.name}' non ha varianti Narrative."; return false; }
        if (ResolveSpecialRoomCount(RoomType.NpcEncounter) > 0 && !HasAnyVariant(activeRoomSet.npcEncounter1x1Variants, activeRoomSet.npcEncounter2x1Variants, activeRoomSet.npcEncounter1x2Variants, activeRoomSet.npcEncounter2x2Variants)) { error=$"il room set '{activeRoomSet.name}' non ha varianti NpcEncounter."; return false; }

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

        if (!string.IsNullOrWhiteSpace(pendingThemeOverrideId))
        {
            string requested = pendingThemeOverrideId;
            pendingThemeOverrideId = null; // explicitly one-floor only
            foreach (DungeonFloorThemeTable.ThemeChoice choice in entry.themes)
                if (choice != null && choice.theme != null && choice.theme.roomSet != null && string.Equals(choice.theme.themeId, requested, StringComparison.Ordinal))
                    return choice.theme;
            Debug.LogWarning($"[CoreGenerator] Theme override '{requested}' is not valid for floor {floor}; using weighted floor selection.");
        }

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

    private int ResolveNormalRoomCount()
    {
        if (activeFloorDefinition == null || activeFloorDefinition.normalRooms == null)
            return Mathf.Max(0, totalNormalRooms);
        return activeFloorDefinition.normalRooms.Resolve(DungeonDeterminism.Create(gameSeedString, currentFloor, "floor", "normal-count"));
    }

    private int ResolveSpecialRoomCount(RoomType roomType)
    {
        if (activeFloorDefinition == null)
            return roomType == RoomType.Curch || roomType == RoomType.EvilCurch || roomType == RoomType.Wave || roomType == RoomType.Challenge || roomType == RoomType.Miniboss || roomType == RoomType.Parkour || roomType == RoomType.Narrative || roomType == RoomType.NpcEncounter ? 0 : 1;
        DungeonFloorDefinition.RoomCount count = activeFloorDefinition.GetCount(roomType);
        return count == null ? 0 : count.Resolve(DungeonDeterminism.Create(gameSeedString, currentFloor, "floor", roomType + "-count"));
    }

    private int ResolveSecretAccessRoomCount(bool superSecret)
    {
        if (activeFloorDefinition == null)
            return 0;

        DungeonFloorDefinition.RoomCount count = activeFloorDefinition.GetSecretAccessCount(superSecret);
        string seedLabel = superSecret ? "super-secret-access-count" : "secret-access-count";
        return count == null ? 0 : count.Resolve(DungeonDeterminism.Create(gameSeedString, currentFloor, "floor", seedLabel));
    }

    private DungeonRoomSet PickFloorRoomSet(DungeonFloorDefinition definition)
    {
        if (definition == null || definition.allowedRoomSets == null) return null;
        int total = 0;
        foreach (DungeonFloorDefinition.RoomSetChoice choice in definition.allowedRoomSets)
            if (choice != null && choice.roomSet != null) total += Mathf.Max(1, choice.weight);
        if (total <= 0) return null;
        System.Random random = DungeonDeterminism.Create(gameSeedString, currentFloor, "floor", "room-set");
        int roll = random.Next(total);
        foreach (DungeonFloorDefinition.RoomSetChoice choice in definition.allowedRoomSets)
        {
            if (choice == null || choice.roomSet == null) continue;
            roll -= Mathf.Max(1, choice.weight);
            if (roll < 0) return choice.roomSet;
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
        _prefabLookup = new Dictionary<RoomPoolKey, Dictionary<Vector2Int, Room[]>>
        {
            [RoomPoolKey.Normal] = BuildSizeMap(
                activeRoomSet.normal1x1Variants,
                activeRoomSet.normal2x1Variants,
                activeRoomSet.normal1x2Variants,
                activeRoomSet.normal2x2Variants),
            [RoomPoolKey.Boss] = BuildSizeMap(
                activeRoomSet.boss1x1Variants,
                activeRoomSet.boss2x1Variants,
                activeRoomSet.boss1x2Variants,
                activeRoomSet.boss2x2Variants),
            [RoomPoolKey.Shop] = BuildSizeMap(
                activeRoomSet.shop1x1Variants,
                activeRoomSet.shop2x1Variants,
                activeRoomSet.shop1x2Variants,
                activeRoomSet.shop2x2Variants),
            [RoomPoolKey.Treasure] = BuildSizeMap(
                activeRoomSet.treasure1x1Variants,
                activeRoomSet.treasure2x1Variants,
                activeRoomSet.treasure1x2Variants,
                activeRoomSet.treasure2x2Variants),
            [RoomPoolKey.Curch] = new Dictionary<Vector2Int, Room[]>
            {
                [new Vector2Int(1, 1)] = activeRoomSet.curch1x1Variants,
                [new Vector2Int(2, 2)] = activeRoomSet.curch2x2Variants
            },
            [RoomPoolKey.EvilCurch] = new Dictionary<Vector2Int, Room[]>
            {
                [new Vector2Int(1, 1)] = activeRoomSet.evilCurch1x1Variants,
                [new Vector2Int(2, 2)] = activeRoomSet.evilCurch2x2Variants
            },
            [RoomPoolKey.SecretAccessSecret] = BuildSizeMap(
                activeRoomSet.secretAccessSecret1x1Variants,
                activeRoomSet.secretAccessSecret2x1Variants,
                activeRoomSet.secretAccessSecret1x2Variants,
                activeRoomSet.secretAccessSecret2x2Variants),
            [RoomPoolKey.SecretAccessSuperSecret] = BuildSizeMap(
                activeRoomSet.secretAccessSuperSecret1x1Variants,
                activeRoomSet.secretAccessSuperSecret2x1Variants,
                activeRoomSet.secretAccessSuperSecret1x2Variants,
                activeRoomSet.secretAccessSuperSecret2x2Variants),
            [RoomPoolKey.Wave] = BuildSizeMap(activeRoomSet.wave1x1Variants,activeRoomSet.wave2x1Variants,activeRoomSet.wave1x2Variants,activeRoomSet.wave2x2Variants),
            [RoomPoolKey.Challenge] = BuildSizeMap(activeRoomSet.challenge1x1Variants,activeRoomSet.challenge2x1Variants,activeRoomSet.challenge1x2Variants,activeRoomSet.challenge2x2Variants),
            [RoomPoolKey.Miniboss] = BuildSizeMap(activeRoomSet.miniboss1x1Variants,activeRoomSet.miniboss2x1Variants,activeRoomSet.miniboss1x2Variants,activeRoomSet.miniboss2x2Variants),
            [RoomPoolKey.Parkour] = BuildSizeMap(activeRoomSet.parkour1x1Variants,activeRoomSet.parkour2x1Variants,activeRoomSet.parkour1x2Variants,activeRoomSet.parkour2x2Variants),
            [RoomPoolKey.Narrative] = BuildSizeMap(activeRoomSet.narrative1x1Variants,activeRoomSet.narrative2x1Variants,activeRoomSet.narrative1x2Variants,activeRoomSet.narrative2x2Variants),
            [RoomPoolKey.NpcEncounter] = BuildSizeMap(activeRoomSet.npcEncounter1x1Variants,activeRoomSet.npcEncounter2x1Variants,activeRoomSet.npcEncounter1x2Variants,activeRoomSet.npcEncounter2x2Variants)
        };
    }

    void SpawnDungeon(List<VirtualRoom> layout)
    {
        foreach (var vr in layout)
        {
            Vector3 worldPos = new Vector3(vr.anchorPos.x * xOffset, 0, vr.anchorPos.y * zOffset);
            Room instance = Instantiate(vr.prefabReference, worldPos, Quaternion.identity);
            instance.transform.parent = transform;
            instance.name = $"{vr.roomType}_{vr.anchorPos}";
            string definitionId = instance.roomData != null && !string.IsNullOrWhiteSpace(instance.roomData.stableId)
                ? instance.roomData.stableId : vr.prefabReference.name;
            instance.ConfigureGeneratedInstance(
                DungeonDeterminism.RoomId(gameSeedString, currentFloor, vr.anchorPos, vr.roomType.ToString(), definitionId),
                vr.anchorPos, vr.size, currentFloor, vr.roomType);
            instance.InitializeGeneratedRuntime();
            if (vr.roomType == RoomType.Start) startRoomInstance = instance;

            activeRoomObjects.Add(instance);
        }
    }

    void ConnectDoors()
    {
        var gridLookup = new Dictionary<Vector2Int, Room>();
        foreach (Room r in activeRoomObjects)
        {
            Vector2Int anchor = r.GridAnchor;
            Vector2Int size = r.GridSize;
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                    gridLookup[anchor + new Vector2Int(x, y)] = r;
        }

        foreach (Room r in activeRoomObjects)
        {
            Vector2Int anchor = r.GridAnchor;
            Vector2Int size = r.GridSize;
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
        foreach (Room r in activeRoomObjects)
        {
            MinimapManager.instance.RegisterRoom(r.GridAnchor, r.roomData);
            if (runStateController != null && runStateController.TryGetRoom(r.RuntimeId, out SavedDungeonRoomState state) && state != null)
                MinimapManager.instance.RestoreRoomVisibility(r.GridAnchor, state.visited, state.revealed);
        }
    }

    int GetManhattanDist(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    Vector2Int GetGridPos(Vector3 pos) => new Vector2Int(Mathf.RoundToInt(pos.x / xOffset), Mathf.RoundToInt(pos.z / zOffset));

    void RespawnPlayerAtStart()
    {
        ResolvePlayerTransform();
        if (playerTransform == null) return;
        
        Room startRoom = startRoomInstance ?? activeRoomObjects.FirstOrDefault(r => r != null && r.PlacementRole == RoomType.Start);
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


