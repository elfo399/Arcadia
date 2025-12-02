using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using System.Linq;

public class CoreGenerator : MonoBehaviour
{
    public static CoreGenerator Instance;

    #region --- Riferimenti & Configurazione ---

    [Header("Riferimenti")]
    public Transform playerTransform;
    public NavMeshSurface navMeshSurface;

    [Header("Configurazione Seed")]
    public string gameSeedString = "";
    public bool useRandomSeed = true;
    [HideInInspector] public int currentMasterSeed;

    [Header("Generazione")]
    public int totalNormalRooms = 15;
    public int xOffset = 50;
    public int zOffset = 50;

    [Header("Probabilità Big Room (Normali)")]
    [Range(0, 100)] public int normalBigRoomChance = 30;

    [Header("Probabilità Big Room (Speciali)")]
    [Range(0, 100)] public int bossBigRoomChance = 100;
    [Range(0, 100)] public int shopBigRoomChance = 50;
    [Range(0, 100)] public int treasureBigRoomChance = 50;

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
    private System.Random prng;

    private readonly Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    
    private readonly Vector2Int[] bigSizes = { 
        new Vector2Int(2, 2), 
        new Vector2Int(2, 1), 
        new Vector2Int(1, 2) 
    };

    #endregion

    #region --- Unity Lifecycle ---

    void Awake() { Instance = this; }
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
        if (useRandomSeed) gameSeedString = System.Guid.NewGuid().ToString().Substring(0, 8);
        currentMasterSeed = gameSeedString.GetHashCode();

        CleanupScene();

        List<VirtualRoom> finalLayout = null;
        int attempts = 0;
        bool success = false;

        // Aumentiamo i tentativi perché le regole sono molto strette
        while (!success && attempts < 300)
        {
            prng = new System.Random(currentMasterSeed + attempts);
            
            if(showRngLogs && attempts == 0) Debug.Log("--- INIZIO GENERAZIONE ---");

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
        }
        else
        {
            Debug.LogError("CRITICO: Impossibile generare dungeon. Regole troppo strette (Distanza Boss + Dead End).");
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
                // Passiamo FALSE come "strictOneDoor" per le stanze normali
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

        // 3. SOCKETS
        List<Vector2Int> freeSockets = new List<Vector2Int>();
        foreach (var cell in occupiedCells)
        {
            foreach (var dir in directions)
            {
                Vector2Int neighbor = cell + dir;
                if (!occupiedCells.Contains(neighbor) && !freeSockets.Contains(neighbor))
                    freeSockets.Add(neighbor);
            }
        }
        freeSockets = freeSockets.OrderBy(x => prng.Next()).ToList();

        // 4. SPECIALI
        if (!TryPlaceSpecialRoom(layout, occupiedCells, freeSockets, "Shop", -1, shopBigRoomChance)) return null;
        if (!TryPlaceSpecialRoom(layout, occupiedCells, freeSockets, "Treasure", -1, treasureBigRoomChance)) return null;
        
        // Per il Boss attiviamo il flag "bossMustBeDeadEnd" se richiesto
        if (!TryPlaceSpecialRoom(layout, occupiedCells, freeSockets, "Boss", minBossDistance, bossBigRoomChance)) return null;

        return layout;
    }

    bool TryPlaceSpecialRoom(List<VirtualRoom> layout, HashSet<Vector2Int> occupied, List<Vector2Int> sockets, string type, int minDistance, int chance)
    {
        List<Vector2Int> sizesAttemptOrder = GetSizesToTry(chance, type);
        
        // Flag specifico: se è Boss e vogliamo una sola porta, attiviamo la modalità "strict"
        bool isStrictDeadEnd = (type == "Boss" && bossMustBeDeadEnd);

        for (int i = 0; i < sockets.Count; i++)
        {
            Vector2Int spot = sockets[i];

            if (minDistance > 0 && GetManhattanDist(Vector2Int.zero, spot) < minDistance) continue;

            foreach (var size in sizesAttemptOrder)
            {
                // Qui passiamo il flag strictOneDoor
                if (CanFit(spot, size, occupied, isStrictDeadEnd))
                {
                    // Controllo adiacenza speciale per il Boss (Shop/Treasure)
                    if (avoidBossTouchingSpecials && type == "Boss")
                    {
                        if (IsTouchingRestrictedRoom(spot, size, layout)) continue; 
                    }

                    Room prefab = GetRandomPrefab(type, size);
                    if (prefab != null)
                    {
                        AddRoomToLayout(layout, occupied, spot, size, type, prefab);
                        sockets.RemoveAt(i);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    // --- Helper Logic ---

    List<Vector2Int> GetSizesToTry(int chancePercent, string roomType)
    {
        List<Vector2Int> sizes = new List<Vector2Int>();
        int roll = prng.Next(0, 100);
        bool tryBig = roll < chancePercent;

        if (showRngLogs)
        {
            string color = tryBig ? "green" : "grey";
            string resultText = tryBig ? "BIG" : "Small";
            Debug.Log($"[RNG] <b>{roomType}</b>: Roll <color=yellow>{roll}</color> (Chance < {chancePercent}%) -> <color={color}>{resultText}</color>");
        }

        if (tryBig)
        {
            sizes.AddRange(bigSizes.OrderBy(x => prng.Next()));
            sizes.Add(new Vector2Int(1, 1));
        }
        else
        {
            sizes.Add(new Vector2Int(1, 1));
        }
        return sizes;
    }

    // --- NUOVA LOGICA CANFIT: Supporta "Solo 1 Entrata" ---
    bool CanFit(Vector2Int anchor, Vector2Int size, HashSet<Vector2Int> occupied, bool strictOneDoor)
    {
        int connectionsCount = 0;

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int cell = anchor + new Vector2Int(x, y);

                // 1. Controllo Collisione: La cella deve essere libera
                if (occupied.Contains(cell)) return false;

                // 2. Conta le connessioni (solo se stiamo controllando per strictOneDoor)
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

        // 3. Regola Boss: Deve avere ESATTAMENTE 1 connessione in totale (Vicolo Cieco)
        // Se ha 0 connessioni è isolata (impossibile con questo algo, ma ok).
        // Se ha > 1 connessioni, vuol dire che tocca il dungeon in due punti -> Bocciata.
        if (strictOneDoor && connectionsCount != 1) return false;

        return true;
    }

    bool IsTouchingRestrictedRoom(Vector2Int anchor, Vector2Int size, List<VirtualRoom> currentLayout)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int cell = anchor + new Vector2Int(x, y);
                foreach (var dir in directions)
                {
                    Vector2Int neighborCheck = cell + dir;
                    foreach (var existingRoom in currentLayout)
                    {
                        if (existingRoom.Contains(neighborCheck))
                        {
                            if (existingRoom.type == "Shop" || existingRoom.type == "Treasure") return true; 
                        }
                    }
                }
            }
        }
        return false;
    }

    VirtualRoom AddRoomToLayout(List<VirtualRoom> layout, HashSet<Vector2Int> occupied, Vector2Int anchor, Vector2Int size, string type, Room prefab)
    {
        VirtualRoom vr = new VirtualRoom { anchorPos = anchor, size = size, type = type, prefabReference = prefab };
        layout.Add(vr);
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                occupied.Add(anchor + new Vector2Int(x, y));
        return vr;
    }

    Room GetRandomPrefab(string type, Vector2Int size)
    {
        Room[] source = null;
        if (type == "Normal")
        {
            if (size == new Vector2Int(1, 1)) source = normal1x1Variants;
            else if (size == new Vector2Int(2, 1)) source = normal2x1Variants;
            else if (size == new Vector2Int(1, 2)) source = normal1x2Variants;
            else if (size == new Vector2Int(2, 2)) source = normal2x2Variants;
        }
        else if (type == "Boss")
        {
            if (size == new Vector2Int(1, 1)) source = boss1x1Variants;
            else if (size == new Vector2Int(2, 1)) source = boss2x1Variants;
            else if (size == new Vector2Int(1, 2)) source = boss1x2Variants;
            else if (size == new Vector2Int(2, 2)) source = boss2x2Variants;
        }
        else if (type == "Shop")
        {
            if (size == new Vector2Int(1, 1)) source = shop1x1Variants;
            else if (size == new Vector2Int(2, 1)) source = shop2x1Variants;
            else if (size == new Vector2Int(1, 2)) source = shop1x2Variants;
            else if (size == new Vector2Int(2, 2)) source = shop2x2Variants;
        }
        else if (type == "Treasure")
        {
            if (size == new Vector2Int(1, 1)) source = treasure1x1Variants;
            else if (size == new Vector2Int(2, 1)) source = treasure2x1Variants;
            else if (size == new Vector2Int(1, 2)) source = treasure1x2Variants;
            else if (size == new Vector2Int(2, 2)) source = treasure2x2Variants;
        }
        return (source != null && source.Length > 0) ? source[prng.Next(source.Length)] : null;
    }

    #endregion

    #region --- Costruzione Fisica ---

    void SpawnDungeon(List<VirtualRoom> layout)
    {
        foreach (var vr in layout)
        {
            Vector3 worldPos = new Vector3(vr.anchorPos.x * xOffset, 0, vr.anchorPos.y * zOffset);
            Room instance = Instantiate(vr.prefabReference, worldPos, Quaternion.identity);
            instance.transform.parent = transform;
            instance.name = $"{vr.type}_{vr.anchorPos}";

            instance.roomData.size = vr.size;
            instance.roomData.isStartRoom = (vr.type == "Start");
            instance.roomData.isBossRoom = (vr.type == "Boss");
            instance.roomData.isShopRoom = (vr.type == "Shop");
            instance.roomData.isTreasureRoom = (vr.type == "Treasure");

            activeRoomObjects.Add(instance);
        }
    }

    void ConnectDoors()
    {
        Dictionary<Vector2Int, Room> gridLookup = new Dictionary<Vector2Int, Room>();
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

    #endregion
}