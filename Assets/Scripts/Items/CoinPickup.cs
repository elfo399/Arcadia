using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    [Header("Settings")]
    public int coinValue = 1;
    public float rotateSpeed = 80f; // Rotazione lenta ed elegante

    void Update()
    {
        // Ruota solo su se stessa, niente su e giù
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerStats stats = other.GetComponent<PlayerStats>();
            if (stats == null) stats = other.GetComponentInParent<PlayerStats>();

            if (stats != null)
            {
                stats.AddCoins(coinValue);
                Destroy(gameObject);
            }
        }
    }
}