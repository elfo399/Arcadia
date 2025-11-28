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
        public GameObject doorObject; 
        public GameObject wallObject; 
        
        [HideInInspector] public bool isConnected; 
    }

    [Header("Configurazione Porte")]
    public List<DoorEntry> doors = new List<DoorEntry>();

    [Header("Stato Battaglia")]
    public bool roomCleared = false; 
    public List<GameObject> activeEnemies = new List<GameObject>(); 

    [Header("Rewards")]
    public GameObject coinPrefab; 
    public int minCoins = 2;
    public int maxCoins = 5;

    private bool playerEntered = false;

    // --- SETUP PORTE ---
    public void OpenDoor(Vector2Int relativePos, Vector2Int direction)
    {
        for(int i = 0; i < doors.Count; i++)
        {
            if (doors[i].gridOffset == relativePos && doors[i].direction == direction)
            {
                if(doors[i].wallObject != null) doors[i].wallObject.SetActive(false);
                if(doors[i].doorObject != null) doors[i].doorObject.SetActive(true);
                
                var entry = doors[i];
                entry.isConnected = true; 
                doors[i] = entry; 
                return;
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
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
        }

        if (activeEnemies.Count == 0 && playerEntered && !roomCleared)
        {
            UnlockRoom();
        }
    }

    // --- LOGICA COMBATTIMENTO ---
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !playerEntered && !roomCleared)
        {
            if (activeEnemies.Count > 0)
            {
                LockRoom(); 
                WakeUpEnemies(); 
            }
            else
            {
                roomCleared = true; 
            }
            playerEntered = true;
        }
    }

    void LockRoom()
    {
        foreach (var d in doors)
        {
            if (d.wallObject != null) d.wallObject.SetActive(true);
        }
        Debug.Log("STANZA BLOCCATA! Uccidi i nemici.");
    }

    void UnlockRoom()
    {
        roomCleared = true;
        
        foreach (var d in doors)
        {
            if (d.isConnected)
            {
                if(d.wallObject != null) d.wallObject.SetActive(false);
            }
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
            Vector3 randomOffset = new Vector3(Random.Range(-2f, 2f), 0.01f, Random.Range(-2f, 2f));
            Vector3 spawnPos = transform.position + randomOffset;
            
            // --- MODIFICA QUI ---
            // Aggiunto 'transform' alla fine per rendere le monete figlie della stanza
            Instantiate(coinPrefab, spawnPos, Quaternion.identity, transform);
        }
    }
}