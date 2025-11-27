using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    [Header("Settings")]
    public int coinValue = 1;      // Quanto vale questa moneta?
    public float rotateSpeed = 100f; // Velocità di rotazione estetica

    void Update()
    {
        // La moneta gira su se stessa per essere più carina
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        // Se tocca il giocatore
        if (other.CompareTag("Player"))
        {
            // Cerca il portafoglio (PlayerStats)
            PlayerStats stats = other.GetComponent<PlayerStats>();
            
            // Se non lo trova sull'oggetto colpito, prova sul padre (caso comune)
            if (stats == null) stats = other.GetComponentInParent<PlayerStats>();

            if (stats != null)
            {
                stats.AddCoins(coinValue);
                
                // Qui puoi aggiungere un suono: AudioSource.PlayClipAtPoint(sound, transform.position);
                
                Destroy(gameObject); // La moneta sparisce
            }
        }
    }
}