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
        public GameObject doorObject; // Cornice aperta
        public GameObject wallObject; // Muro chiuso
        public GameObject lockObject; // <--- LUCC HETTO / CANCELLO
        
        [HideInInspector] public bool isConnected; 
    }

    [Header("Configurazione Porte")]
    public List<DoorEntry> doors = new List<DoorEntry>();

    [Header("Stato Stanza")]
    public bool isLocked = false; // Serve la chiave?
    public bool roomCleared = false; 
    public List<GameObject> activeEnemies = new List<GameObject>(); 

    [Header("Rewards")]
    public GameObject coinPrefab; 
    public int minCoins = 2;
    public int maxCoins = 5;

    private bool playerEntered = false;

    void Start()
    {
        // Se è Shop o Treasure (e non è Start), blocca la stanza
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

    // --- SBLOCCO CON CHIAVE ---
    public void UnlockSpecialRoom()
    {
        isLocked = false; // Ora è aperta per sempre

        // Rimuovi TUTTI i cancelli da TUTTE le porte connesse
        foreach (var d in doors)
        {
            if (d.isConnected && d.lockObject != null)
            {
                d.lockObject.SetActive(false);
            }
        }
    }

    // --- GESTIONE NEMICI ---
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

    // --- LOGICA COMBATTIMENTO ---
    void OnTriggerEnter(Collider other)
    {
        // Entra solo se non è bloccata a chiave (devi prima aprire il cancello)
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
            if (d.wallObject != null) d.wallObject.SetActive(true);
        }
        Debug.Log("STANZA BLOCCATA! Uccidi i nemici.");
    }

    void UnlockRoomBattle()
    {
        roomCleared = true;
        
        foreach (var d in doors)
        {
            if (d.isConnected && d.wallObject != null) d.wallObject.SetActive(false);
        }
        
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
            // Offset casuale
            Vector3 randomOffset = new Vector3(Random.Range(-2f, 2f), 0.2f, Random.Range(-2f, 2f));
            Vector3 spawnPos = transform.position + randomOffset;
            
            // Spawn come figlio della stanza (pulizia hierarchy)
            Instantiate(coinPrefab, spawnPos, Quaternion.identity, transform);
        }
    }
}