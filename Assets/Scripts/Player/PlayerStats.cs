using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class PlayerStats : MonoBehaviour, IDamageable
{
    public static PlayerStats instance;

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

    [Header("Economia")]
    public int currentCoins = 0;
    public TextMeshProUGUI coinText;

    [Header("Chiavi")]
    public int currentKeys = 0;
    public TextMeshProUGUI keyText;

    [Header("Statistiche Persistenti")]
    public int karma = 0;
    public int benedetto = 0;
    public int malefico = 0;

    [Header("UI Bars (Sistema DynamicBar)")]
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
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return; 
        }

        animator = GetComponentInChildren<Animator>();

        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentMana = maxMana;
        currentFlasks = maxFlasks;

        LoadStats();
        AssignUIElements();
        UpdateAllUI();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AssignUIElements();
        UpdateAllUI();
    }

    void AssignUIElements()
    {
        // Trova le barre UI tramite il loro tag o un componente specifico
        // È consigliabile dare un tag univoco ai GameObject delle barre nel prefab della UI
        var uiBars = FindObjectsOfType<DynamicBar>();
        foreach (var bar in uiBars)
        {
            if (bar.CompareTag("HealthBar")) healthBar = bar;
            else if (bar.CompareTag("StaminaBar")) staminaBar = bar;
            else if (bar.CompareTag("ManaBar")) manaBar = bar;
        }

        // Trova i testi tramite tag
        var textElements = FindObjectsOfType<TextMeshProUGUI>();
        foreach (var text in textElements)
        {
            if (text.CompareTag("CoinText")) coinText = text;
            else if (text.CompareTag("KeyText")) keyText = text;
            else if (text.CompareTag("FlaskCounterText")) flaskCounterText = text;
        }
    }


    void Update()
    {
        HandleStaminaRegen();

        if (flaskTimer > 0f)
            flaskTimer -= Time.deltaTime;
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

    // --- DANNO & VITA (IDamageable) ---
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

    public void RestoreHealth(float amount)
    {
        if (amount <= 0f) return;
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        UpdateHealthBar();
    }

    // --- FLASKS & STAMINA & MANA ---

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
        // Se siamo nell'Hub, non consumare stamina (Opzionale)
        if (SceneManager.GetActiveScene().name == "HubScene") return;

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

    // --- AGGIORNAMENTO GRAFICO ---

    void UpdateAllUI()
    {
        UpdateAllBars();
        UpdateFlaskUI();
        UpdateCoinUI();
        UpdateKeyUI();
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

    void UpdateFlaskUI()
    {
        if (flaskCounterText != null) flaskCounterText.text = currentFlasks.ToString();
    }

    // --- SALVATAGGIO E CARICAMENTO STATS ---
    public void SaveStats()
    {
        GameData data = new GameData
        {
            karma = this.karma,
            benedetto = this.benedetto,
            malefico = this.malefico
        };
        // Aggiungi qui altre statistiche che vuoi salvare

        SaveSystem.SaveData(data);
    }

    public void LoadStats()
    {
        GameData data = SaveSystem.LoadData();
        if (data != null)
        {
            this.karma = data.karma;
            this.benedetto = data.benedetto;
            this.malefico = data.malefico;
            // Aggiungi qui altre statistiche che vuoi caricare

            Debug.Log("Dati persistenti caricati da file!");
        }
        else
        {
            Debug.Log("Nessun file di salvataggio trovato. Verranno usati i valori correnti (da Inspector alla prima esecuzione).");
        }
    }


    public void AddPersistentStat(string statName, int amount)
    {
        switch (statName)
        {
            case "karma":
                karma += amount;
                Debug.Log($"Karma modificato di {amount}. Nuovo valore: {karma}");
                break;
            case "benedetto":
                benedetto += amount;
                Debug.Log($"Benedetto modificato di {amount}. Nuovo valore: {benedetto}");
                break;
            case "malefico":
                malefico += amount;
                Debug.Log($"Malefico modificato di {amount}. Nuovo valore: {malefico}");
                break;
            default:
                Debug.LogWarning($"Statistica persistente '{statName}' non trovata.");
                return;
        }
        SaveStats();
    }

    void OnApplicationQuit()
    {
        SaveStats();
    }

    void Die()
    {
        SaveStats();
        Debug.Log("SEI MORTO! Ritorno all'Hub...");
        SceneManager.LoadScene("HubScene");
    }
}