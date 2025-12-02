using UnityEngine;
using System.Collections.Generic;

public class Room : MonoBehaviour
{
    [Header("Dati Stanza")]
    public RoomData roomData;
    [HideInInspector] public string internalRoomType = "Normal";

    [System.Serializable]
    public struct DoorEntry 
    {
        public string label;          
        public Vector2Int gridOffset; 
        public Vector2Int direction;  
        public GameObject doorObject; // Cornice
        public GameObject wallObject; // Muro Pieno
        public GameObject lockObject; // Cancello
        
        [HideInInspector] public bool isConnected; 
    }

    [Header("Configurazione Porte")]
    public List<DoorEntry> doors = new List<DoorEntry>();

    [Header("Stato")]
    public bool isLocked = false; // Chiave
    public bool roomCleared = false; 
    public List<GameObject> activeEnemies = new List<GameObject>(); 

    [Header("Rewards")]
    // (Non usare coinPrefab se usi il sistema RoomData.rewards)
    public GameObject coinPrefab; // Fallback vecchio
    public int minCoins = 2;
    public int maxCoins = 5;

    private bool playerEntered = false;

    void Start()
    {
        // Lock iniziale per Shop/Treasure
        if (roomData != null)
        {
            if ((roomData.isShopRoom || roomData.isTreasureRoom) && !roomData.isStartRoom)
            {
                isLocked = true;
            }
        }
    }

    // --- SETUP PORTE (Chiamata dal Generatore) ---
    public void OpenDoor(Vector2Int relativePos, Vector2Int direction)
    {
        for(int i = 0; i < doors.Count; i++)
        {
            if (doors[i].gridOffset == relativePos && doors[i].direction == direction)
            {
                // 1. Togli il muro pieno
                if(doors[i].wallObject != null) doors[i].wallObject.SetActive(false);
                
                // 2. Attiva la cornice della porta
                if(doors[i].doorObject != null) doors[i].doorObject.SetActive(true);

                // 3. Se la stanza è bloccata a chiave, attiva il cancello
                if (isLocked && doors[i].lockObject != null)
                {
                    doors[i].lockObject.SetActive(true);
                }
                
                var entry = doors[i];
                entry.isConnected = true; 
                doors[i] = entry; 
                return;
            }
        }
    }

    // --- SBLOCCO CHIAVE ---
    public void UnlockSpecialRoom()
    {
        isLocked = false; 
        foreach (var d in doors)
        {
            if (d.isConnected && d.lockObject != null) d.lockObject.SetActive(false);
        }
    }

    // --- NEMICI ---
    public void RegisterEnemy(GameObject enemy)
    {
        if (!activeEnemies.Contains(enemy))
        {
            activeEnemies.Add(enemy);
            enemy.SetActive(false); 
        }
    }

    public void EnemyDied(GameObject enemy)
    {
        if (activeEnemies.Contains(enemy)) activeEnemies.Remove(enemy);

        if (activeEnemies.Count == 0 && playerEntered && !roomCleared)
        {
            UnlockRoomBattle();
        }
    }

    // --- BATTLE LOGIC ---
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !playerEntered && !roomCleared && !isLocked)
        {
            if (activeEnemies.Count > 0)
            {
                LockRoomBattle(); 
                WakeUpEnemies(); 
            }
            else
            {
                roomCleared = true; 
            }
            playerEntered = true;
        }
    }

    void LockRoomBattle()
    {
        foreach (var d in doors)
        {
            // Chiudi solo le porte che erano aperte
            if (d.isConnected)
            {
                // PRIORITÀ 1: Sbarre
                if (d.lockObject != null) d.lockObject.SetActive(true);
                // PRIORITÀ 2: Muro (se non ci sono sbarre)
                else if (d.wallObject != null) 
                {
                    d.wallObject.SetActive(true);
                    // Se rimetti il muro, spegni la cornice per pulizia
                    if(d.doorObject != null) d.doorObject.SetActive(false);
                }
            }
        }
        Debug.Log("STANZA BLOCCATA!");
    }

    void UnlockRoomBattle()
    {
        roomCleared = true;
        
        foreach (var d in doors)
        {
            if (d.isConnected)
            {
                // Sblocca Sbarre
                if (d.lockObject != null) d.lockObject.SetActive(false);
                
                // Sblocca Muro (se usato come fallback)
                if (d.wallObject != null) d.wallObject.SetActive(false);
                
                // Riaccendi Cornice
                if (d.doorObject != null) d.doorObject.SetActive(true);
            }
        }
        
        // LOOT SYSTEM
        if (roomData != null && roomData.rewards.Count > 0)
        {
            SpawnRewards();
        }
        else if (coinPrefab != null) // Fallback vecchio
        {
            // SpawnLegacyCoin();
        }
        
        Debug.Log("STANZA PULITA!");
    }

    void WakeUpEnemies()
    {
        foreach (var enemy in activeEnemies) if(enemy!=null) enemy.SetActive(true);
    }

    // --- LOOT SYSTEM DETERMINISTICO ---
    void SpawnRewards()
    {
        // 1. Calcolo Seed Locale (basato su MasterSeed e Posizione Stanza)
        int masterSeed = (CoreGenerator.Instance != null) ? CoreGenerator.Instance.currentMasterSeed : 0;
        int localSeed = masterSeed + (int)(transform.position.x * 100) + (int)(transform.position.z * 100);
        
        System.Random prng = new System.Random(localSeed);

        // 2. Itero su tutti i possibili premi
        foreach (var loot in roomData.rewards)
        {
            if (loot.itemPrefab == null) continue;

            // A. Roll drop chance
            int dropRoll = prng.Next(0, 101);
            
            if (dropRoll <= loot.dropChance)
            {
                // B. Calcolo Quantità Ponderata
                int amountToSpawn = 0;
                
                if (loot.quantityWeights.Count > 0)
                {
                    float totalWeight = 0;
                    foreach(var qw in loot.quantityWeights) totalWeight += qw.chance;

                    double weightRoll = prng.NextDouble() * totalWeight;
                    float currentWeight = 0;

                    foreach(var qw in loot.quantityWeights)
                    {
                        currentWeight += qw.chance;
                        if (weightRoll <= currentWeight)
                        {
                            amountToSpawn = qw.amount;
                            break;
                        }
                    }
                }
                else amountToSpawn = 1; // Default

                // C. Spawn fisico
                for (int i = 0; i < amountToSpawn; i++)
                {
                    float rx = (float)(prng.NextDouble() * 4 - 2);
                    float rz = (float)(prng.NextDouble() * 4 - 2);
                    Vector3 spawnPos = transform.position + new Vector3(rx, 0.2f, rz); // Altezza terra
                    
                    Instantiate(loot.itemPrefab, spawnPos, Quaternion.identity, transform);
                }
            }
        }
    }
}