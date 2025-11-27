using UnityEngine;
using System.Collections.Generic;

public class Room : MonoBehaviour
{
    [Header("Dati Stanza")]
    public RoomData roomData; // Legge la grandezza (Size) da qui

    [System.Serializable]
    public struct DoorEntry 
    {
        public string label;          // Es. "Nord Sinistra"
        public Vector2Int gridOffset; // Su quale mattonella si trova? (0,0), (1,0)...
        public Vector2Int direction;  // Dove guarda? (0,1)=Nord, (0,-1)=Sud, etc.
        public GameObject doorObject; // Il "Muro col Buco" (Disattivato di base)
        public GameObject wallObject; // Il "Muro Pieno" (Attivato di base)
    }

    [Header("Configurazione Porte")]
    public List<DoorEntry> doors = new List<DoorEntry>();

    // Funzione chiamata dal generatore
    public void OpenDoor(Vector2Int relativePos, Vector2Int direction)
    {
        // DEBUG: Vediamo chi bussa alla porta
        // Debug.Log($"Richiesta apertura: Offset {relativePos} - Direzione {direction} su {gameObject.name}");

        foreach (var d in doors)
        {
            if (d.gridOffset == relativePos && d.direction == direction)
            {
                if(d.wallObject != null) d.wallObject.SetActive(false);
                if(d.doorObject != null) d.doorObject.SetActive(true);
                return;
            }
        }
        
        // Se arriviamo qui, il generatore ha chiesto una porta che NON ESISTE nella lista
        Debug.LogWarning($"ATTENZIONE! Il generatore voleva aprire una porta a {direction} all'offset {relativePos} nella stanza {name}, ma non l'ha trovata nella lista!");
    }
}