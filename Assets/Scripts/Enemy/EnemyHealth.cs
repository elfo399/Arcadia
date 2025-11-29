using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("UI")]
    public EnemyHealthBar healthBar; 

    void Start()
    {
        currentHealth = maxHealth;
        if (healthBar != null) healthBar.SetMaxHealth(maxHealth);

        // --- FIX CRUCIALE: AUTO-REGISTRAZIONE ---
        // Cerca se sono dentro una stanza e mi registro da solo.
        // Questo serve se piazzi i nemici a mano senza usare gli spawner.
        Room room = GetComponentInParent<Room>();
        if (room != null)
        {
            // RegisterEnemy controlla già i duplicati, quindi non c'è rischio di doppia registrazione
            room.RegisterEnemy(gameObject);
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        
        if (healthBar != null) healthBar.SetHealth(currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        // Avvisa la stanza che sono morto
        Room room = GetComponentInParent<Room>();
        if (room != null)
        {
            room.EnemyDied(gameObject);
        }

        Destroy(gameObject);
    }
}