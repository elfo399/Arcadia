using UnityEngine;
using TMPro;

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    // Maximum player health
    public float maxHealth = 100f;
    // Current player health
    public float currentHealth = 100f;

    [Header("Stamina")]
    // Maximum stamina available
    public float maxStamina = 100f;
    // Current stamina amount
    public float currentStamina = 100f;
    // Stamina regenerated per second
    public float staminaRegenRate = 25f;
    // Delay before stamina regeneration starts
    public float staminaRegenDelay = 0.8f;

    [Header("Mana")]
    // Maximum mana available
    public float maxMana = 50f;
    // Current mana amount
    public float currentMana = 50f;

    [Header("Flasks")]
    // Maximum number of flasks the player can carry
    public int maxFlasks = 3;
    // Current number of flasks
    public int currentFlasks = 3;
    // Health restored per flask use
    public float flaskHealAmount = 40f;
    // Cooldown time between flask uses
    public float flaskUseCooldown = 1f;

    [Header("UI Bars (assegna da Inspector)")]
    // UI bar showing health
    public DynamicBar healthBar;
    // UI bar showing stamina
    public DynamicBar staminaBar;
    // UI bar showing mana
    public DynamicBar manaBar;

    [Header("UI Flask Counter (TextMeshPro)")]
    // Text element displaying current flasks
    public TextMeshProUGUI flaskCounterText;

    // Timestamp of the last stamina expenditure
    private float lastStaminaUseTime;
    // Remaining cooldown time for flask use
    private float flaskTimer;

    // Animator used to trigger flask animations
    private Animator animator;

    // Initialize stats and UI at startup
    void Awake()
    {
        animator = GetComponentInChildren<Animator>();

        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentMana = maxMana;
        currentFlasks = maxFlasks;

        UpdateAllBars();
        UpdateFlaskUI();
    }

    // Handle passive updates such as stamina regeneration and flask cooldown
    void Update()
    {
        HandleStaminaRegen();

        if (flaskTimer > 0f)
            flaskTimer -= Time.deltaTime;
    }

    // Regenerate stamina after the configured delay
    void HandleStaminaRegen()
    {
        if (Time.time < lastStaminaUseTime + staminaRegenDelay)
            return;

        if (currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
            UpdateStaminaBar();
        }
    }

    // Check if the player has enough stamina for an action
    public bool HasStamina(float amount)
    {
        return currentStamina >= amount;
    }

    // Spend a specified amount of stamina immediately
    public void SpendStamina(float amount)
    {
        if (amount <= 0f)
            return;

        currentStamina -= amount;
        if (currentStamina < 0f)
            currentStamina = 0f;

        lastStaminaUseTime = Time.time;
        UpdateStaminaBar();
    }

    // Spend stamina scaled by delta time
    public void SpendStaminaPerSecond(float amountPerSecond)
    {
        float amount = amountPerSecond * Time.deltaTime;
        SpendStamina(amount);
    }

    // Consume a flask to restore health and trigger UI updates
    public void UseFlask()
    {
        if (currentFlasks <= 0)
            return;
        if (flaskTimer > 0f)
            return;

        currentFlasks--;
        flaskTimer = flaskUseCooldown;

        currentHealth += flaskHealAmount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        UpdateHealthBar();
        UpdateFlaskUI();

        if (animator != null)
            animator.SetTrigger("DrinkPotion");
    }

    // Refresh the flask counter text
    void UpdateFlaskUI()
    {
        if (flaskCounterText != null)
            flaskCounterText.text = currentFlasks.ToString();
    }

    // Apply incoming damage and update UI
    public void TakeDamage(float amount)
    {
        if (amount <= 0f)
            return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(0f, currentHealth);

        UpdateHealthBar();
    }

    // Restore health by a given amount
    public void RestoreHealth(float amount)
    {
        if (amount <= 0f)
            return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        UpdateHealthBar();
    }

    // Spend mana if available and update the UI
    public bool UseMana(float amount)
    {
        if (amount <= 0f)
            return true;

        if (currentMana < amount)
            return false;

        currentMana -= amount;
        currentMana = Mathf.Max(0f, currentMana);

        UpdateManaBar();
        return true;
    }

    // Restore mana by a given amount
    public void RestoreMana(float amount)
    {
        if (amount <= 0f)
            return;

        currentMana += amount;
        currentMana = Mathf.Min(currentMana, maxMana);

        UpdateManaBar();
    }

    // Update all resource bars in one call
    void UpdateAllBars()
    {
        UpdateHealthBar();
        UpdateStaminaBar();
        UpdateManaBar();
    }

    // Update the health bar based on current values
    void UpdateHealthBar()
    {
        if (healthBar == null)
            return;

        healthBar.SetMax(maxHealth);
        healthBar.SetCurrent(currentHealth);
    }

    // Update the stamina bar based on current values
    void UpdateStaminaBar()
    {
        if (staminaBar == null)
            return;

        staminaBar.SetMax(maxStamina);
        staminaBar.SetCurrent(currentStamina);
    }

    // Update the mana bar based on current values
    void UpdateManaBar()
    {
        if (manaBar == null)
            return;

        manaBar.SetMax(maxMana);
        manaBar.SetCurrent(currentMana);
    }
}
