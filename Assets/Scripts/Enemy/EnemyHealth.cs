using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public int maxHealth = 100;
    public int currentHealth;
    [Min(0)] public int experienceReward = 0;

    [Header("UI")]
    public EnemyHealthBar healthBar;

    private bool isDead = false;

    void Start()
    {
        if (currentHealth <= 0) currentHealth = maxHealth;
        if (healthBar != null) healthBar.SetMaxHealth(maxHealth);

        Room room = GetComponentInParent<Room>();
        if (room != null) room.RegisterEnemy(gameObject);
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        if (healthBar != null) healthBar.SetHealth(currentHealth);
        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        var stats = PlayerStats.instance != null ? PlayerStats.instance : FindObjectOfType<PlayerStats>();
        if (stats != null && experienceReward > 0)
            stats.AddExperience(experienceReward);

        Room room = GetComponentInParent<Room>();
        if (room != null) room.EnemyDied(gameObject);
        Destroy(gameObject);
    }
}
