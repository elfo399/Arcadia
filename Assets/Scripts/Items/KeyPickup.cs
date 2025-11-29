using UnityEngine;

public class KeyPickup : MonoBehaviour
{
    public float rotateSpeed = 100f;

    void Update()
    {
        // Rotazione estetica
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
                stats.AddKeys(1);
                // Qui puoi mettere un suono: AudioManager.Play("KeyPickup");
                Destroy(gameObject);
            }
        }
    }
}