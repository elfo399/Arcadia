using UnityEngine;

public class InteractableDoor : MonoBehaviour, IInteractable
{
    private Room parentRoom;
    private bool isOpened = false;

    void Start()
    {
        parentRoom = GetComponentInParent<Room>();
    }

    // Questa funzione viene chiamata dal Player quando preme E / Triangolo
    public void Interact(GameObject player)
    {
        if (isOpened) return;

        // Controlliamo se serve la chiave
        if (parentRoom != null && parentRoom.isLocked)
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();
            
            if (stats != null && stats.UseKey())
            {
                Debug.Log("Interazione: Porta Aperta!");
                OpenLogic();
            }
            else
            {
                Debug.Log("Interazione: Ti serve una chiave!");
                // Qui puoi mettere un suono "Porta Bloccata" o un testo a schermo
            }
        }
        else
        {
            // Se non è bloccata a chiave (es. porta normale chiusa), apri e basta
            OpenLogic();
        }
    }

    void OpenLogic()
    {
        isOpened = true;
        if (parentRoom != null) parentRoom.UnlockSpecialRoom();
        
        // Disattiva il collider o l'oggetto stesso per far passare il player
        gameObject.SetActive(false); 
    }

    public string GetPrompt()
    {
        return "Apri";
    }
}