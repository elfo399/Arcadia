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
        // Seed deterministico basato su master seed e posizione stanza
        int masterSeed = (CoreGenerator.Instance != null) ? CoreGenerator.Instance.currentMasterSeed : 0;
        int localSeed = masterSeed + (int)(transform.position.x * 1000) + (int)(transform.position.z * 1000);
        System.Random prng = new System.Random(localSeed);

        if (prng.Next(0, 101) > spawnChance)
        {
            Destroy(gameObject); 
            return;
        }

        if (props.Length == 0) return;

        GameObject prefabToSpawn = props[prng.Next(0, props.Length)];
        Quaternion rotation = transform.rotation;
        if (randomRotation)
        {
            float angle = (float)(prng.NextDouble() * 360f);
            rotation = Quaternion.Euler(0, angle, 0);
        }

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
