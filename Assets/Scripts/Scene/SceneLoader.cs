using UnityEngine;
using UnityEngine.SceneManagement; // Fondamentale per cambiare scena

public class SceneLoader : MonoBehaviour
{
    [Header("Configurazione")]
    public string sceneToLoad = "GameScene"; // Il nome esatto della scena Dungeon
    public bool isExit = false; // Se vero, chiude il gioco (per un'altra porta magari)

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (isExit)
            {
                Debug.Log("Uscita dal Gioco");
                Application.Quit();
            }
            else
            {
                // Carica la scena del gioco. 
                // "Single" significa che chiude questa e apre l'altra.
                SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
            }
        }
    }
}