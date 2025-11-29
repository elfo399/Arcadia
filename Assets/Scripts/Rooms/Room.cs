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
        
        // Spawn delle ricompense basato sulla nuova lista
        if (roomData != null && roomData.rewards.Count > 0)
        {
            SpawnRewards();
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

    void SpawnRewards()
    {
        // Fase 1: Itera su ogni TIPO di ricompensa (es. Monete, Pozioni)
        foreach (var lootItem in roomData.rewards)
        {
            if (lootItem.itemPrefab == null || lootItem.quantityWeights.Count == 0) continue;

            // Tira il dado per vedere se questo TIPO di oggetto spawna
            float dropRoll = Random.Range(0f, 100f);
            if (dropRoll <= lootItem.dropChance)
            {
                // Fase 2: L'oggetto spawna. Ora, quale quantità scegliamo?
                // Calcoliamo il peso totale della sotto-lista delle quantità
                float totalWeight = 0;
                foreach (var quantity in lootItem.quantityWeights)
                {
                    totalWeight += quantity.chance;
                }

                // Scegliamo un punto casuale all'interno del peso totale
                float quantityRoll = Random.Range(0f, totalWeight);
                int chosenAmount = 0;

                // Troviamo a quale fascia di quantità corrisponde il punto scelto
                foreach (var quantity in lootItem.quantityWeights)
                {
                    if (quantityRoll <= quantity.chance)
                    {
                        chosenAmount = quantity.amount;
                        break; // Trovato! Usciamo dal ciclo.
                    }
                    else
                    {
                        quantityRoll -= quantity.chance;
                    }
                }
                
                // Se per qualche motivo non viene scelta una quantità (es. pesi a 0), non spawniamo nulla
                if(chosenAmount <= 0) continue;

                // Spawniamo la quantità scelta
                for (int i = 0; i < chosenAmount; i++)
                {
                    Vector3 randomOffset = new Vector3(Random.Range(-2f, 2f), 0.2f, Random.Range(-2f, 2f));
                    Vector3 spawnPos = transform.position + randomOffset;
                    Instantiate(lootItem.itemPrefab, spawnPos, Quaternion.identity, transform);
                }
            }
        }
    }
}