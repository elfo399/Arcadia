using UnityEngine;

public class RandomProp : MonoBehaviour
{
    [Header("Cosa spawnare")]
    public GameObject[] props; 

    [Header("Probabilità")]
    [Range(0, 100)] public int spawnChance = 50; 
    public bool randomRotation = true; 

    void Start()
    {
        if (Random.Range(0, 100) > spawnChance)
        {
            Destroy(gameObject); 
            return;
        }

        if (props.Length == 0) return;

        GameObject prefabToSpawn = props[Random.Range(0, props.Length)];
        Quaternion rotation = transform.rotation;
        if (randomRotation) rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        GameObject spawnedProp = Instantiate(prefabToSpawn, transform.position, rotation, transform);

        // --- MODIFICA CRUCIALE ---
        // Se l'oggetto ha il tag "Enemy", lo registriamo nella stanza
        if (spawnedProp.CompareTag("Enemy"))
        {
            Room roomScript = GetComponentInParent<Room>();
            if (roomScript != null)
            {
                roomScript.RegisterEnemy(spawnedProp);
            }
        }
    }
}