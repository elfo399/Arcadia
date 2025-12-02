using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public int maxHealth = 100;
    public int currentHealth; // ORA È PUBBLICO

    [Header("UI")]
    public EnemyHealthBar healthBar; 

    void Start()
    {
        if (currentHealth == 0) currentHealth = maxHealth; // Inizializza se non settato
        if (healthBar != null) healthBar.SetMaxHealth(maxHealth);

        Room room = GetComponentInParent<Room>();
        if (room != null) room.RegisterEnemy(gameObject);
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (healthBar != null) healthBar.SetHealth(currentHealth);
        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        Room room = GetComponentInParent<Room>();
        if (room != null) room.EnemyDied(gameObject);
        Destroy(gameObject);
    }
}