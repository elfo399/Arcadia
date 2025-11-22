using UnityEngine;
using TMPro; // per il testo UI

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float staminaRegenRate = 25f;
    public float staminaRegenDelay = 0.8f;

    [Header("Mana")]
    public float maxMana = 50f;
    public float currentMana = 50f;

    [Header("Flasks")]
    public int maxFlasks = 3;
    public int currentFlasks = 3;
    public float flaskHealAmount = 40f;
    public float flaskUseCooldown = 1f;

    // UI
    [Header("UI Bars (assegna da Inspector)")]
    public DynamicBar healthBar;
    public DynamicBar staminaBar;
    public DynamicBar manaBar;

    [Header("UI Flask Counter (TextMeshPro)")]
    public TextMeshProUGUI flaskCounterText;

    private float lastStaminaUseTime;
    private float flaskTimer;

    private Animator animator;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();

        currentHealth  = maxHealth;
        currentStamina = maxStamina;
        currentMana    = maxMana;
        currentFlasks  = maxFlasks;

        UpdateAllBars();
        UpdateFlaskUI();
    }

    void Update()
    {
        HandleStaminaRegen();

        // timer per il cooldown della flask
        if (flaskTimer > 0f)
            flaskTimer -= Time.deltaTime;
    }

    //──────────────── STAMINA ────────────────

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

    public bool HasStamina(float amount)
    {
        return currentStamina >= amount;
    }

    public void SpendStamina(float amount)
    {
        if (amount <= 0f) return;

        currentStamina -= amount;
        if (currentStamina < 0f)
            currentStamina = 0f;

        lastStaminaUseTime = Time.time;
        UpdateStaminaBar();
    }

    public void SpendStaminaPerSecond(float amountPerSecond)
    {
        float amount = amountPerSecond * Time.deltaTime;
        SpendStamina(amount);
    }

    //──────────────── FLASKS ────────────────

    public void UseFlask()
    {
        // niente flask se finite o in cooldown
        if (currentFlasks <= 0) return;
        if (flaskTimer > 0f) return;

        currentFlasks--;
        flaskTimer = flaskUseCooldown;

        // cura la vita
        currentHealth += flaskHealAmount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        UpdateHealthBar();
        UpdateFlaskUI();

        // animazione di bere pozione
        if (animator != null)
            animator.SetTrigger("DrinkPotion");
    }

    void UpdateFlaskUI()
    {
        if (flaskCounterText != null)
            flaskCounterText.text = currentFlasks.ToString();
    }

    //──────────────── DANNI / HEAL DIRETTI ────────────────

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(0f, currentHealth);

        UpdateHealthBar();
        // qui dopo potrai aggiungere morte ecc.
    }

    public void RestoreHealth(float amount)
    {
        if (amount <= 0f) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        UpdateHealthBar();
    }

    public bool UseMana(float amount)
    {
        if (amount <= 0f) return true;

        if (currentMana < amount)
            return false;

        currentMana -= amount;
        currentMana = Mathf.Max(0f, currentMana);

        UpdateManaBar();
        return true;
    }

    public void RestoreMana(float amount)
    {
        if (amount <= 0f) return;

        currentMana += amount;
        currentMana = Mathf.Min(currentMana, maxMana);

        UpdateManaBar();
    }

    //──────────────── AGGIORNAMENTO BARRE UI ────────────────

    void UpdateAllBars()
    {
        UpdateHealthBar();
        UpdateStaminaBar();
        UpdateManaBar();
    }

    void UpdateHealthBar()
    {
        if (healthBar == null) return;

        healthBar.SetMax(maxHealth);          // FRAME = maxHealth
        healthBar.SetCurrent(currentHealth);  // FILL  = currentHealth
    }

    void UpdateStaminaBar()
    {
        if (staminaBar == null) return;

        staminaBar.SetMax(maxStamina);
        staminaBar.SetCurrent(currentStamina);
    }

    void UpdateManaBar()
    {
        if (manaBar == null) return;

        manaBar.SetMax(maxMana);
        manaBar.SetCurrent(currentMana);
    }
}
