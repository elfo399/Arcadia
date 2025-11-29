using UnityEngine;
using TMPro; 

// Aggiungiamo IDamageable così i nemici possono colpirti
public class PlayerStats : MonoBehaviour, IDamageable 
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
    public int currentCoins = 0;
    public TextMeshProUGUI coinText; 

    [Header("Chiavi (NUOVO)")]
    public int currentKeys = 0;
    public TextMeshProUGUI keyText;

    [Header("UI Bars (Il tuo sistema DynamicBar)")]
    public DynamicBar healthBar;   // Trascina qui l'oggetto con lo script DynamicBar
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

        // Inizializza tutto
        UpdateAllBars();
        UpdateFlaskUI();
        UpdateCoinUI();
        UpdateKeyUI();
    }

    void Update()
    {
        HandleStaminaRegen();

        if (flaskTimer > 0f)
            flaskTimer -= Time.deltaTime;
    }

    // --- GESTIONE BARRE (DynamicBar) ---
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

    // --- GESTIONE MONETE ---
    public void AddCoins(int amount)
    {
        currentCoins += amount;
        UpdateCoinUI();
    }

    void UpdateCoinUI()
    {
        if (coinText != null) coinText.text = currentCoins.ToString();
    }

    // --- GESTIONE CHIAVI ---
    public void AddKeys(int amount)
    {
        currentKeys += amount;
        UpdateKeyUI();
    }

    public bool UseKey()
    {
        if (currentKeys > 0)
        {
            currentKeys--;
            UpdateKeyUI();
            return true;
        }
        return false;
    }

    void UpdateKeyUI()
    {
        if (keyText != null) keyText.text = "x" + currentKeys.ToString();
    }

    // --- DANNO (Interfaccia IDamageable) ---
    // Questa versione accetta int (dai nemici)
    public void TakeDamage(int amount)
    {
        TakeDamage((float)amount);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;

        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;

        UpdateHealthBar();
        
        if (currentHealth <= 0) Die();
    }

    // --- GESTIONE RISORSE ---

    public void UseFlask()
    {
        if (currentFlasks <= 0 || flaskTimer > 0f) return;

        currentFlasks--;
        flaskTimer = flaskUseCooldown;

        currentHealth += flaskHealAmount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        UpdateHealthBar();
        UpdateFlaskUI();

        if (animator != null) animator.SetTrigger("DrinkPotion");
    }

    public void SpendStamina(float amount)
    {
        if (amount <= 0f) return;

        currentStamina -= amount;
        if (currentStamina < 0) currentStamina = 0;

        lastStaminaUseTime = Time.time;
        UpdateStaminaBar();
    }

    public bool HasStamina(float amount)
    {
        return currentStamina >= amount;
    }

    public void SpendStaminaPerSecond(float amountPerSecond)
    {
        SpendStamina(amountPerSecond * Time.deltaTime);
    }

    void HandleStaminaRegen()
    {
        if (Time.time < lastStaminaUseTime + staminaRegenDelay) return;

        if (currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            if (currentStamina > maxStamina) currentStamina = maxStamina;
            UpdateStaminaBar();
        }
    }

    public bool UseMana(float amount)
    {
        if (currentMana < amount) return false;
        currentMana -= amount;
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

    void UpdateFlaskUI()
    {
        if (flaskCounterText != null) flaskCounterText.text = currentFlasks.ToString();
    }

    void Die()
    {
        Debug.Log("GAME OVER");
        // Qui ricaricheremo la scena
    }
}