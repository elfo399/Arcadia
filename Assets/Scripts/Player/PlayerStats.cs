using UnityEngine;
using TMPro; // NECESSARIO PER LA UI

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

    [Header("Economia (NUOVO)")]
    public int currentCoins = 0;      // I soldi attuali
    public TextMeshProUGUI coinText;  // Trascina qui il testo della UI

    [Header("UI Bars")]
    public DynamicBar healthBar;
    public DynamicBar staminaBar;
    public DynamicBar manaBar;

    [Header("UI Flask Counter")]
    public TextMeshProUGUI flaskCounterText;

    private float lastStaminaUseTime;
    private float flaskTimer;
    private Animator animator;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();

        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentMana = maxMana;
        currentFlasks = maxFlasks;

        UpdateAllBars();
        UpdateFlaskUI();
        UpdateCoinUI(); // Aggiorna la scritta a 0 all'inizio
    }

    void Update()
    {
        HandleStaminaRegen();

        if (flaskTimer > 0f)
            flaskTimer -= Time.deltaTime;
    }

    // --- NUOVE FUNZIONI PER I SOLDI ---
    public void AddCoins(int amount)
    {
        currentCoins += amount;
        UpdateCoinUI();
    }

    void UpdateCoinUI()
    {
        if (coinText != null)
        {
            coinText.text = currentCoins.ToString();
        }
    }
    // ----------------------------------

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
        if (currentStamina < 0f) currentStamina = 0f;

        lastStaminaUseTime = Time.time;
        UpdateStaminaBar();
    }

    public void SpendStaminaPerSecond(float amountPerSecond)
    {
        float amount = amountPerSecond * Time.deltaTime;
        SpendStamina(amount);
    }

    public void UseFlask()
    {
        if (currentFlasks <= 0) return;
        if (flaskTimer > 0f) return;

        currentFlasks--;
        flaskTimer = flaskUseCooldown;

        currentHealth += flaskHealAmount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        UpdateHealthBar();
        UpdateFlaskUI();

        if (animator != null) animator.SetTrigger("DrinkPotion");
    }

    void UpdateFlaskUI()
    {
        if (flaskCounterText != null) flaskCounterText.text = currentFlasks.ToString();
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(0f, currentHealth);

        UpdateHealthBar();
        
        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        Debug.Log("GAME OVER");
        // Qui puoi mettere il reload della scena o il menu di morte
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
        if (currentMana < amount) return false;

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

    void UpdateAllBars()
    {
        UpdateHealthBar();
        UpdateStaminaBar();
        UpdateManaBar();
    }

    void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.SetMax(maxHealth);
            healthBar.SetCurrent(currentHealth);
        }
    }

    void UpdateStaminaBar()
    {
        if (staminaBar != null)
        {
            staminaBar.SetMax(maxStamina);
            staminaBar.SetCurrent(currentStamina);
        }
    }

    void UpdateManaBar()
    {
        if (manaBar != null)
        {
            manaBar.SetMax(maxMana);
            manaBar.SetCurrent(currentMana);
        }
    }
}