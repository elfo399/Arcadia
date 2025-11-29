using UnityEngine;

public interface IInteractable
{
    void Interact(GameObject player);
    string GetPrompt(); // Es. "Apri", "Parla", "Raccogli"
}