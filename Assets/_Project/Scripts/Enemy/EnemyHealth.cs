using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public int maxHealth = 100;
    public int currentHealth;
    [Min(0)] public int experienceReward = 0;

    [Header("Quest Events")]
    [SerializeField] private string questTargetId;
    [SerializeField] private string questTargetTag = "enemy";

    [Header("UI")]
    public EnemyHealthBar healthBar;

    private bool isDead = false;

    void Start()
    {
        if (currentHealth <= 0) currentHealth = maxHealth;
        if (healthBar != null) healthBar.SetMaxHealth(maxHealth);

        // Spawners register encounter ownership before activating the enemy.
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

        var stats = PlayerStats.instance;
        if (stats != null && experienceReward > 0)
            stats.AddExperience(experienceReward);

        Room room = GetComponentInParent<Room>();
        if (room != null) room.EnemyDied(gameObject);

        QuestEvents.Raise(QuestObjectiveEventType.KillEnemy, ResolveQuestTargetId(), questTargetTag);
        Destroy(gameObject);
    }

    private string ResolveQuestTargetId()
    {
        return string.IsNullOrWhiteSpace(questTargetId) ? gameObject.name : questTargetId.Trim();
    }
}
