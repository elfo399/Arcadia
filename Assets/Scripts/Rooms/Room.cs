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
        public Vector2Int gridOffset; // (0,0) o (1,0) ecc.
        public Vector2Int direction;  // (0,1) Nord, (0,-1) Sud, ecc.
        public GameObject doorObject; // Il varco (Cornice)
        public GameObject wallObject; // Il muro solido
        
        // Memoria: Questa porta era aperta prima del combattimento?
        [HideInInspector] public bool isConnected; 
    }

    [Header("Configurazione Porte")]
    public List<DoorEntry> doors = new List<DoorEntry>();

    [Header("Stato Battaglia")]
    public bool roomCleared = false; // Già completata?
    public List<GameObject> activeEnemies = new List<GameObject>(); // Lista nemici vivi

    [Header("Rewards")]
    public GameObject coinPrefab; // Trascina qui il prefab della Moneta
    public int minCoins = 2;
    public int maxCoins = 5;

    private bool playerEntered = false;

    // --- SETUP PORTE (Chiamata dal Generatore) ---
    public void OpenDoor(Vector2Int relativePos, Vector2Int direction)
    {
        for(int i = 0; i < doors.Count; i++)
        {
            if (doors[i].gridOffset == relativePos && doors[i].direction == direction)
            {
                // 1. Apri visivamente subito
                if(doors[i].wallObject != null) doors[i].wallObject.SetActive(false);
                if(doors[i].doorObject != null) doors[i].doorObject.SetActive(true);
                
                // 2. Ricordatelo per dopo! (IMPORTANTE per riaprire dopo il lock)
                var entry = doors[i];
                entry.isConnected = true; 
                doors[i] = entry; // Salviamo la modifica nella struct
                return;
            }
        }
    }

    // --- GESTIONE NEMICI ---
    
    // Chiamata dallo spawner (RandomProp) quando crea uno zombie
    public void RegisterEnemy(GameObject enemy)
    {
        if (!activeEnemies.Contains(enemy))
        {
            activeEnemies.Add(enemy);
            // Opzionale: Disattiva i nemici finché non entri (Optimization)
            enemy.SetActive(false); 
        }
    }

    // Chiamata dal nemico quando muore
    public void EnemyDied(GameObject enemy)
    {
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
        }

        // Se la lista è vuota e il player è dentro -> VITTORIA
        if (activeEnemies.Count == 0 && playerEntered && !roomCleared)
        {
            UnlockRoom();
        }
    }

    // --- LOGICA COMBATTIMENTO ---

    void OnTriggerEnter(Collider other)
    {
        // Se entra il Player, la stanza non è pulita e ci sono nemici
        if (other.CompareTag("Player") && !playerEntered && !roomCleared)
        {
            if (activeEnemies.Count > 0)
            {
                LockRoom(); // CHIUDI TUTTO!
                WakeUpEnemies(); // SVEGLIA ZOMBIE!
            }
            else
            {
                roomCleared = true; // Stanza vuota, segnala come fatta
            }
            playerEntered = true;
        }
    }

    void LockRoom()
    {
        // Riattiva i muri solidi su TUTTE le porte (anche quelle connesse)
        foreach (var d in doors)
        {
            if (d.wallObject != null) d.wallObject.SetActive(true);
        }
        Debug.Log("STANZA BLOCCATA! Uccidi i nemici.");
    }

    void UnlockRoom()
    {
        roomCleared = true;
        
        // Riapri SOLO le porte che erano connesse (usando il bool isConnected salvato prima)
        foreach (var d in doors)
        {
            if (d.isConnected)
            {
                if(d.wallObject != null) d.wallObject.SetActive(false);
            }
        }
        
        // SPAWN MONETE
        if (coinPrefab != null)
        {
            SpawnReward();
        }
        
        Debug.Log("STANZA PULITA! Porte aperte.");
    }

    void WakeUpEnemies()
    {
        foreach (var enemy in activeEnemies)
        {
            if(enemy != null) enemy.SetActive(true);
        }
    }

    void SpawnReward()
    {
        int amount = Random.Range(minCoins, maxCoins + 1);
        
        for (int i = 0; i < amount; i++)
        {
            // Spawna al centro della stanza con un piccolo offset casuale
            Vector3 randomOffset = new Vector3(Random.Range(-2f, 2f), 1f, Random.Range(-2f, 2f));
            Vector3 spawnPos = transform.position + randomOffset;
            
            Instantiate(coinPrefab, spawnPos, Quaternion.identity);
        }
    }
}