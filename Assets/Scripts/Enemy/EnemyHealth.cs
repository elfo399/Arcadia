using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public int maxHealth = 100;
    private int currentHealth;

    [Header("UI")]
    public EnemyHealthBar healthBar; // Trascina qui il componente della barra

    void Start()
    {
        currentHealth = maxHealth;
        
        if (healthBar != null) 
            healthBar.SetMaxHealth(maxHealth);
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        
        // Aggiorna la UI
        if (healthBar != null) 
            healthBar.SetHealth(currentHealth);

        // Effetto (Opzionale): Feedback visivo o sonoro
        // Debug.Log("Colpito! HP: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        // Avvisa la stanza (IMPORTANTISSIMO PER APRIRE LE PORTE)
        Room room = GetComponentInParent<Room>();
        if (room != null)
        {
            room.EnemyDied(gameObject);
        }

        // Distruggi
        Destroy(gameObject);
    }
}