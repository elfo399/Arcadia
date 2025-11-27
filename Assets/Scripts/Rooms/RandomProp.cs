using UnityEngine;

public class RandomProp : MonoBehaviour
{
    [Header("Cosa spawnare")]
    // Trascina qui i prefab (Vaso, Sasso, Teschio, oppure Enemy_Zombie)
    public GameObject[] props; 

    [Header("Probabilità")]
    [Range(0, 100)] 
    public int spawnChance = 50; // 50% di probabilità che appaia qualcosa

    [Header("Rotazione Casuale")]
    public bool randomRotation = true; // Se vero, ruota l'oggetto a caso sull'asse Y

    void Start()
    {
        // 1. Lancio del dado: Spawniamo o no?
        if (Random.Range(0, 100) > spawnChance)
        {
            Destroy(gameObject); // Niente spawn, puliamo la scena cancellando questo punto
            return;
        }

        // 2. Controllo di sicurezza (Lista vuota)
        if (props.Length == 0) return;

        // 3. Scelta dell'oggetto
        GameObject prefabToSpawn = props[Random.Range(0, props.Length)];

        // 4. Calcolo Rotazione
        Quaternion rotation = transform.rotation;
        if (randomRotation)
        {
            float randomY = Random.Range(0f, 360f);
            rotation = Quaternion.Euler(0, randomY, 0);
        }

        // 5. Spawn
        // Usiamo Instantiate come figlio di questo oggetto per mantenere ordine nella Hierarchy
        Instantiate(prefabToSpawn, transform.position, rotation, transform);
    }
}