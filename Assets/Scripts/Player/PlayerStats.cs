using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

public class PlayerStats : MonoBehaviour, IDamageable
{
    public const int MaxAllocatableAttributeLevel = 99;
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

    [Header("Attribute Scaling")]
    [SerializeField] private float healthPerVigor = 5f;
    [SerializeField] private float manaPerMind = 3f;
    [SerializeField] private float staminaPerEndurance = 4f;

    [Header("Flasks")]
    public int maxFlasks = 3;
    public int currentFlasks = 3;
    public float flaskHealAmount = 40f;
    public float flaskUseCooldown = 1f;

    [Header("Economia (Run Corrente)")]
    public int runGold = 0;
    public int runSilver = 0;
    public int runCopper = 0;
    public event Action<int, int, int> OnRunWalletChanged;

    [Header("Banca (Persistente)")]
    public int bankGold = 0;
    public int bankSilver = 0;
    public int bankCopper = 0;
    public event Action<int, int, int> OnBankChanged;

    [Header("Chiavi")]
    public int currentKeys = 0;
    public TextMeshProUGUI keyText;

    [Header("Statistiche Persistenti")]
    public int playerLevel = 1;
    public int levelExperience = 0;
    public int experienceToNextLevel = 100;
    public int unspentAttributePoints = 0;
    public int vigor = 10;
    public int mind = 10;
    public int endurance = 10;
    public int strength = 10;
    public int dexterity = 10;
    public int intelligence = 10;
    public int faith = 10;
    public int karma = 0;
    public int benedetto = 0;
    public int malefico = 0;

    [Header("UI Bars (Sistema DynamicBar)")]
    public DynamicBar healthBar;
    public DynamicBar staminaBar;
    public DynamicBar manaBar;

    [Header("UI Flask Counter")]
    public TextMeshProUGUI flaskCounterText;

    [Header("Combat Flags")]
    [SerializeField] private bool invulnerable;

    private float lastStaminaUseTime;
    private float flaskTimer;
    private Animator animator;
    private GameData loadedDataCache;
    private bool loadedQuestStateApplied = false;
    private bool loadedInventoryStateApplied = false;
    private float baseMaxHealth;
    private float baseMaxStamina;
    private float baseMaxMana;
    private QuestManager cachedQuestManager;
    private PlayerInventory cachedPlayerInventory;
    [Header("Debug / Bootstrap")]
    [SerializeField] private bool forceStartDataIgnoreSave = false;

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

        baseMaxHealth = maxHealth;
        baseMaxStamina = maxStamina;
        baseMaxMana = maxMana;

        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentMana = maxMana;
        currentFlasks = maxFlasks;

        LoadStats();
        RecalculateDerivedStats(keepCurrentRatio: true);
        AssignUIElements();
        UpdateAllUI();
        NotifyBankChanged();
        NotifyRunWalletChanged();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // Dopo tutti gli Awake, applica eventuale stato caricato (inventario/quest).
        ApplyLoadedQuestStateIfPossible();
        ApplyLoadedInventoryStateIfPossible();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RecalculateDerivedStats(keepCurrentRatio: true);
        AssignUIElements();
        UpdateAllUI();
        ApplyLoadedQuestStateIfPossible();
        ApplyLoadedInventoryStateIfPossible();

        // Ogni run parte senza monete portatili
        if (scene.name == "GameScene")
        {
            ResetRunWallet();
        }
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
            if (text.CompareTag("KeyText")) keyText = text;
            else if (text.CompareTag("FlaskCounterText")) flaskCounterText = text;
        }
    }

    private void ResetRunWallet()
    {
        runGold = runSilver = runCopper = 0;
        NotifyRunWalletChanged();
    }


    void Update()
    {
        HandleStaminaRegen();

        if (flaskTimer > 0f)
            flaskTimer -= Time.deltaTime;
    }

    // --- GESTIONE MONETE ---
    public void AddCoins(int copperAmount)
    {
        // Tutte le monete raccolte sono espresse in rame, converti
        runCopper += Mathf.Max(0, copperAmount);
        NormalizeRunWallet();
        NotifyRunWalletChanged();
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
        if (invulnerable) return;

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

    public void SetInvulnerable(bool value)
    {
        invulnerable = value;
    }

    // --- FLASKS & STAMINA & MANA ---

    public bool IsUsableOnCooldown()
    {
        return flaskTimer > 0f;
    }

    public bool TryApplyUsableEffect(UsableItemData usable)
    {
        if (usable == null) return false;
        if (flaskTimer > 0f) return false;

        switch (usable.effectType)
        {
            case UsableItemData.UsableEffectType.Heal:
                RestoreHealth(usable.healAmount > 0 ? usable.healAmount : flaskHealAmount);
                break;
            case UsableItemData.UsableEffectType.Mana:
                RestoreMana(usable.manaRestore > 0 ? usable.manaRestore : 0f);
                break;
            case UsableItemData.UsableEffectType.Invisibility:
            case UsableItemData.UsableEffectType.Custom:
                // Placeholder: effetti custom da implementare nel sistema status/effects.
                break;
        }

        float configuredCooldown = usable.cooldownSeconds > 0f ? usable.cooldownSeconds : flaskUseCooldown;
        flaskTimer = Mathf.Max(0f, configuredCooldown);
        if (animator != null) animator.SetTrigger("DrinkPotion");
        return true;
    }

    public void SetFlaskCountVisual(int value)
    {
        currentFlasks = Mathf.Max(0, value);
        UpdateFlaskUI();
    }

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
            playerLevel = this.playerLevel,
            levelExperience = this.levelExperience,
            experienceToNextLevel = this.experienceToNextLevel,
            unspentAttributePoints = this.unspentAttributePoints,
            vigor = this.vigor,
            mind = this.mind,
            endurance = this.endurance,
            strength = this.strength,
            dexterity = this.dexterity,
            intelligence = this.intelligence,
            faith = this.faith,
            karma = this.karma,
            benedetto = this.benedetto,
            malefico = this.malefico
            ,
            bankGold = this.bankGold,
            bankSilver = this.bankSilver,
            bankCopper = this.bankCopper
        };

        var questManager = GetCachedQuestManager();
        if (questManager != null)
        {
            data.quests = SerializeQuests(questManager.GetQuestsSnapshot());
        }
        var playerInventory = GetCachedPlayerInventory();
        if (playerInventory != null)
        {
            data.playerInventory = playerInventory.CreateSaveData();
        }
        // Aggiungi qui altre statistiche che vuoi salvare

        SaveSystem.SaveData(data);
        loadedDataCache = data;
        loadedQuestStateApplied = true;
        loadedInventoryStateApplied = true;
    }

    public void LoadStats()
    {
        if (forceStartDataIgnoreSave)
        {
            loadedDataCache = null;
            loadedQuestStateApplied = true;
            loadedInventoryStateApplied = true;
            Debug.Log("ForceStartData attivo: caricamento save ignorato, uso dati iniziali da Inspector/StartingLoadout.");
            return;
        }

        GameData data = SaveSystem.LoadData();
        loadedDataCache = data;
        loadedQuestStateApplied = false;
        loadedInventoryStateApplied = false;
        if (data != null)
        {
            this.playerLevel = Mathf.Max(1, data.playerLevel > 0 ? data.playerLevel : this.playerLevel);
            this.levelExperience = Mathf.Max(0, data.levelExperience);
            this.experienceToNextLevel = Mathf.Max(1, data.experienceToNextLevel > 0 ? data.experienceToNextLevel : this.experienceToNextLevel);
            this.unspentAttributePoints = Mathf.Max(0, data.unspentAttributePoints);
            this.vigor = data.vigor > 0 ? data.vigor : this.vigor;
            this.mind = data.mind > 0 ? data.mind : this.mind;
            this.endurance = data.endurance > 0 ? data.endurance : this.endurance;
            this.strength = data.strength > 0 ? data.strength : this.strength;
            this.dexterity = data.dexterity > 0 ? data.dexterity : this.dexterity;
            this.intelligence = data.intelligence > 0 ? data.intelligence : this.intelligence;
            this.faith = data.faith > 0 ? data.faith : this.faith;
            this.karma = data.karma;
            this.benedetto = data.benedetto;
            this.malefico = data.malefico;
            this.bankGold = data.bankGold;
            this.bankSilver = data.bankSilver;
            this.bankCopper = data.bankCopper;
            // Non applicare qui quest/inventory: se avviene prima degli Awake degli altri componenti
            // (es. PlayerInventory), il loro Awake può sovrascrivere i dati caricati.
            // L'applicazione viene fatta in Start() e OnSceneLoaded().
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
        if (amount == 0) return;

        if (IsLevelBasedAttribute(statName))
        {
            // Gli attributi principali consumano punti livello (1 punto per click/livello).
            bool spent = TrySpendAttributePoint(statName);
            if (!spent)
            {
                Debug.Log("Nessun punto attributo disponibile.");
                return;
            }
            return;
        }

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

        RecalculateDerivedStats(keepCurrentRatio: true);
        UpdateAllUI();
        SaveStats();
    }

    public bool TrySpendAttributePoint(string statName)
    {
        if (!IsLevelBasedAttribute(statName)) return false;
        if (unspentAttributePoints <= 0) return false;

        switch (statName)
        {
            case "vigor":
                if (vigor >= MaxAllocatableAttributeLevel) return false;
                vigor = Mathf.Clamp(vigor + 1, 1, MaxAllocatableAttributeLevel);
                break;
            case "mind":
                if (mind >= MaxAllocatableAttributeLevel) return false;
                mind = Mathf.Clamp(mind + 1, 1, MaxAllocatableAttributeLevel);
                break;
            case "endurance":
                if (endurance >= MaxAllocatableAttributeLevel) return false;
                endurance = Mathf.Clamp(endurance + 1, 1, MaxAllocatableAttributeLevel);
                break;
            case "strength":
                if (strength >= MaxAllocatableAttributeLevel) return false;
                strength = Mathf.Clamp(strength + 1, 1, MaxAllocatableAttributeLevel);
                break;
            case "dexterity":
                if (dexterity >= MaxAllocatableAttributeLevel) return false;
                dexterity = Mathf.Clamp(dexterity + 1, 1, MaxAllocatableAttributeLevel);
                break;
            case "intelligence":
                if (intelligence >= MaxAllocatableAttributeLevel) return false;
                intelligence = Mathf.Clamp(intelligence + 1, 1, MaxAllocatableAttributeLevel);
                break;
            default: return false;
        }

        unspentAttributePoints = Mathf.Max(0, unspentAttributePoints - 1);
        RecalculateDerivedStats(keepCurrentRatio: true);
        UpdateAllUI();
        SaveStats();
        return true;
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0) return;

        levelExperience += amount;
        int guard = 0;
        while (levelExperience >= experienceToNextLevel && guard < 1000)
        {
            levelExperience -= experienceToNextLevel;
            playerLevel += 1;
            unspentAttributePoints += 1;
            experienceToNextLevel = Mathf.Max(1, Mathf.RoundToInt(experienceToNextLevel * 1.12f));
            guard++;
        }

        SaveStats();
    }

    public void GainLevels(int levels)
    {
        if (levels <= 0) return;
        playerLevel += levels;
        unspentAttributePoints += levels;
        SaveStats();
    }

    public float GetLevelProgress01()
    {
        if (experienceToNextLevel <= 0) return 0f;
        return Mathf.Clamp01((float)levelExperience / experienceToNextLevel);
    }

    private static bool IsLevelBasedAttribute(string statName)
    {
        switch (statName)
        {
            case "vigor":
            case "mind":
            case "endurance":
            case "strength":
            case "dexterity":
            case "intelligence":
                return true;
            default:
                return false;
        }
    }

    public int GetPersistentStat(string statName)
    {
        switch (statName)
        {
            case "vigor": return vigor;
            case "mind": return mind;
            case "endurance": return endurance;
            case "strength": return strength;
            case "dexterity": return dexterity;
            case "intelligence": return intelligence;
            case "faith": return faith;
            case "karma": return karma;
            case "evil":
            case "malefico": return malefico;
            case "benedetto": return benedetto;
            default: return 0;
        }
    }

    public int GetBasePhysicalDamage()
    {
        // Richiesta design:
        // Vigor + Strength -> base physical damage
        return Mathf.Max(1, strength + Mathf.RoundToInt(vigor * 0.5f));
    }

    public int GetBaseMagicDamage()
    {
        // Richiesta design:
        // Mind + Intelligence -> base magic damage
        return Mathf.Max(0, intelligence + Mathf.RoundToInt(mind * 0.5f));
    }

    public int GetBaseRangedDamage()
    {
        // Richiesta design:
        // Dexterity -> ranged base damage
        return Mathf.Max(1, dexterity);
    }

    public float GetCurrentEquipLoad()
    {
        var inventory = GetCachedPlayerInventory();
        if (inventory == null) return 0f;

        float load = 0f;
        var items = inventory.Items;
        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null) continue;

                int qty = Mathf.Max(1, it.amount);
                float unitWeight = 0f;

                if (it.weaponData != null)
                    unitWeight = Mathf.Max(0f, it.weaponData.weight);
                else if (it.armorData != null)
                    unitWeight = Mathf.Max(0f, it.armorData.weight);
                else if (it.usableData != null)
                    unitWeight = Mathf.Max(0f, it.usableData.weight);
                else if (it.itemData != null)
                    unitWeight = Mathf.Max(0f, it.itemData.weight);

                load += unitWeight * qty;
            }
        }

        return load;
    }

    public float GetMaxEquipLoad()
    {
        return Mathf.Max(1f, endurance * 2f);
    }

    public void RecalculateDerivedStats(bool keepCurrentRatio)
    {
        float oldMaxHealth = Mathf.Max(1f, maxHealth);
        float oldMaxMana = Mathf.Max(1f, maxMana);
        float oldMaxStamina = Mathf.Max(1f, maxStamina);

        float healthRatio = keepCurrentRatio ? Mathf.Clamp01(currentHealth / oldMaxHealth) : 1f;
        float manaRatio = keepCurrentRatio ? Mathf.Clamp01(currentMana / oldMaxMana) : 1f;
        float staminaRatio = keepCurrentRatio ? Mathf.Clamp01(currentStamina / oldMaxStamina) : 1f;

        maxHealth = Mathf.Max(1f, baseMaxHealth + Mathf.Max(0, vigor - 1) * healthPerVigor);
        maxMana = Mathf.Max(1f, baseMaxMana + Mathf.Max(0, mind - 1) * manaPerMind);
        maxStamina = Mathf.Max(1f, baseMaxStamina + Mathf.Max(0, endurance - 1) * staminaPerEndurance);

        currentHealth = Mathf.Clamp(maxHealth * healthRatio, 0f, maxHealth);
        currentMana = Mathf.Clamp(maxMana * manaRatio, 0f, maxMana);
        currentStamina = Mathf.Clamp(maxStamina * staminaRatio, 0f, maxStamina);
    }

    // --- BANCA PERSISTENTE ---
    public void Deposit(int gold, int silver, int copper)
    {
        // Preleva dal wallet di run e deposita in banca
        gold = Mathf.Max(0, gold);
        silver = Mathf.Max(0, silver);
        copper = Mathf.Max(0, copper);

        if (!SpendRunFunds(gold, silver, copper)) return;

        bankGold += gold;
        bankSilver += silver;
        bankCopper += copper;
        NormalizeBank();
        SaveStats();
        NotifyBankChanged();
        NotifyRunWalletChanged();
    }

    public bool Withdraw(int gold, int silver, int copper)
    {
        NormalizeBank();
        if (!HasBankFunds(gold, silver, copper)) return false;

        bankGold -= gold;
        bankSilver -= silver;
        bankCopper -= copper;
        NormalizeBank();
        // Aggiunge al wallet di run
        runGold += gold;
        runSilver += silver;
        runCopper += copper;
        NormalizeRunWallet();
        SaveStats();
        NotifyBankChanged();
        NotifyRunWalletChanged();
        return true;
    }

    public bool HasBankFunds(int gold, int silver, int copper)
    {
        NormalizeBank();
        // Semplificazione: confronta per valuta separata
        return bankGold >= gold && bankSilver >= silver && bankCopper >= copper;
    }

    private void NormalizeBank()
    {
        NormalizeWallet(ref bankGold, ref bankSilver, ref bankCopper);
    }

    private void NotifyBankChanged()
    {
        OnBankChanged?.Invoke(bankGold, bankSilver, bankCopper);
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

    // --- WALLET DI RUN ---
    public bool HasRunFunds(int gold, int silver, int copper)
    {
        NormalizeRunWallet();
        return runGold >= gold && runSilver >= silver && runCopper >= copper;
    }

    public bool SpendRunFunds(int gold, int silver, int copper)
    {
        if (!HasRunFunds(gold, silver, copper)) return false;
        runGold -= gold;
        runSilver -= silver;
        runCopper -= copper;
        NormalizeRunWallet();
        NotifyRunWalletChanged();
        return true;
    }

    private void NormalizeRunWallet()
    {
        NormalizeWallet(ref runGold, ref runSilver, ref runCopper);
    }

    private void NotifyRunWalletChanged()
    {
        OnRunWalletChanged?.Invoke(runGold, runSilver, runCopper);
    }

    private void ApplyLoadedQuestStateIfPossible()
    {
        if (loadedQuestStateApplied) return;
        if (loadedDataCache == null || loadedDataCache.quests == null) return;

        var questManager = GetCachedQuestManager();
        if (questManager == null) return;

        var mapped = DeserializeQuests(loadedDataCache.quests);
        questManager.ReplaceAllQuests(mapped);
        loadedQuestStateApplied = true;
    }

    private void ApplyLoadedInventoryStateIfPossible()
    {
        if (loadedInventoryStateApplied) return;
        if (loadedDataCache == null || loadedDataCache.playerInventory == null) return;

        var playerInventory = GetCachedPlayerInventory();
        if (playerInventory == null) return;

        playerInventory.ApplySaveData(loadedDataCache.playerInventory);
        loadedInventoryStateApplied = true;
    }

    private static SavedQuestData[] SerializeQuests(List<QuestManager.QuestData> source)
    {
        if (source == null || source.Count == 0) return Array.Empty<SavedQuestData>();

        var result = new SavedQuestData[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            var q = source[i];
            if (q == null) continue;

            result[i] = new SavedQuestData
            {
                questId = q.questId,
                title = q.title,
                location = q.location,
                completed = q.completed,
                questTypeLabel = q.questTypeLabel,
                recommendedLabel = q.recommendedLabel,
                loreTitle = q.loreTitle,
                loreDescription = q.loreDescription,
                loreAuthor = q.loreAuthor,
                objectives = SerializeObjectives(q.objectives),
                rewards = SerializeRewards(q.rewards)
            };
        }

        return result;
    }

    private static SavedQuestObjectiveData[] SerializeObjectives(List<QuestManager.QuestObjectiveData> source)
    {
        if (source == null || source.Count == 0) return Array.Empty<SavedQuestObjectiveData>();

        var result = new SavedQuestObjectiveData[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            var obj = source[i];
            if (obj == null) continue;
            result[i] = new SavedQuestObjectiveData
            {
                title = obj.title,
                description = obj.description,
                completed = obj.completed
            };
        }

        return result;
    }

    private static SavedQuestRewardData[] SerializeRewards(List<QuestManager.QuestRewardData> source)
    {
        if (source == null || source.Count == 0) return Array.Empty<SavedQuestRewardData>();

        var result = new SavedQuestRewardData[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            var r = source[i];
            if (r == null) continue;
            result[i] = new SavedQuestRewardData
            {
                type = r.type,
                amount = r.amount,
                itemName = r.itemName,
                iconName = r.icon != null ? r.icon.name : string.Empty
            };
        }

        return result;
    }

    private static List<QuestManager.QuestData> DeserializeQuests(SavedQuestData[] source)
    {
        var result = new List<QuestManager.QuestData>();
        if (source == null || source.Length == 0) return result;
        var iconLookup = BuildRewardIconLookup();

        for (int i = 0; i < source.Length; i++)
        {
            var q = source[i];
            if (q == null) continue;

            result.Add(new QuestManager.QuestData
            {
                questId = q.questId,
                title = q.title,
                location = q.location,
                completed = q.completed,
                questTypeLabel = q.questTypeLabel,
                recommendedLabel = q.recommendedLabel,
                loreTitle = q.loreTitle,
                loreDescription = q.loreDescription,
                loreAuthor = q.loreAuthor,
                objectives = DeserializeObjectives(q.objectives),
                rewards = DeserializeRewards(q.rewards, iconLookup)
            });
        }

        return result;
    }

    private static List<QuestManager.QuestObjectiveData> DeserializeObjectives(SavedQuestObjectiveData[] source)
    {
        var result = new List<QuestManager.QuestObjectiveData>();
        if (source == null || source.Length == 0) return result;

        for (int i = 0; i < source.Length; i++)
        {
            var obj = source[i];
            if (obj == null) continue;
            result.Add(new QuestManager.QuestObjectiveData
            {
                title = obj.title,
                description = obj.description,
                completed = obj.completed
            });
        }

        return result;
    }

    private static List<QuestManager.QuestRewardData> DeserializeRewards(SavedQuestRewardData[] source, Dictionary<string, Sprite> iconLookup)
    {
        var result = new List<QuestManager.QuestRewardData>();
        if (source == null || source.Length == 0) return result;

        for (int i = 0; i < source.Length; i++)
        {
            var r = source[i];
            if (r == null) continue;
            Sprite resolvedIcon = ResolveRewardIcon(r, iconLookup);
            result.Add(new QuestManager.QuestRewardData
            {
                type = r.type,
                amount = r.amount,
                itemName = r.itemName,
                icon = resolvedIcon
            });
        }

        return result;
    }

    private static Sprite ResolveRewardIcon(SavedQuestRewardData reward, Dictionary<string, Sprite> iconLookup)
    {
        if (reward == null || iconLookup == null || iconLookup.Count == 0) return null;

        string iconKey = NormalizeLookupKey(reward.iconName);
        if (!string.IsNullOrEmpty(iconKey) && iconLookup.TryGetValue(iconKey, out var iconByName))
            return iconByName;

        string itemKey = NormalizeLookupKey(reward.itemName);
        if (!string.IsNullOrEmpty(itemKey) && iconLookup.TryGetValue(itemKey, out var iconByItemName))
            return iconByItemName;

        return null;
    }

    private static Dictionary<string, Sprite> BuildRewardIconLookup()
    {
        var lookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        RegisterWeaponIcons(lookup, Resources.LoadAll<WeaponItem>(""));
        RegisterUsableIcons(lookup, Resources.LoadAll<UsableItemData>(""));
        RegisterItemIcons(lookup, Resources.LoadAll<ItemData>(""));

        // Fallback: include anche asset già caricati in memoria (scene/editor/runtime)
        RegisterWeaponIcons(lookup, Resources.FindObjectsOfTypeAll<WeaponItem>());
        RegisterUsableIcons(lookup, Resources.FindObjectsOfTypeAll<UsableItemData>());
        RegisterItemIcons(lookup, Resources.FindObjectsOfTypeAll<ItemData>());

        return lookup;
    }

    private static void RegisterWeaponIcons(Dictionary<string, Sprite> lookup, WeaponItem[] items)
    {
        if (lookup == null || items == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            var w = items[i];
            if (w == null) continue;
            RegisterIcon(lookup, w.icon, w.weaponName);
        }
    }

    private static void RegisterUsableIcons(Dictionary<string, Sprite> lookup, UsableItemData[] items)
    {
        if (lookup == null || items == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            var u = items[i];
            if (u == null) continue;
            RegisterIcon(lookup, u.icon, u.itemName);
        }
    }

    private static void RegisterItemIcons(Dictionary<string, Sprite> lookup, ItemData[] items)
    {
        if (lookup == null || items == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            if (it == null) continue;
            RegisterIcon(lookup, it.icon, it.itemName);
        }
    }

    private static void RegisterIcon(Dictionary<string, Sprite> lookup, Sprite icon, string itemName)
    {
        if (lookup == null || icon == null) return;

        string iconKey = NormalizeLookupKey(icon.name);
        if (!string.IsNullOrEmpty(iconKey) && !lookup.ContainsKey(iconKey))
            lookup.Add(iconKey, icon);

        string itemKey = NormalizeLookupKey(itemName);
        if (!string.IsNullOrEmpty(itemKey) && !lookup.ContainsKey(itemKey))
            lookup.Add(itemKey, icon);
    }

    private static string NormalizeLookupKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private QuestManager GetCachedQuestManager()
    {
        if (cachedQuestManager != null) return cachedQuestManager;
        cachedQuestManager = QuestManager.Instance != null ? QuestManager.Instance : FindObjectOfType<QuestManager>();
        return cachedQuestManager;
    }

    private PlayerInventory GetCachedPlayerInventory()
    {
        if (cachedPlayerInventory != null) return cachedPlayerInventory;
        cachedPlayerInventory = FindObjectOfType<PlayerInventory>();
        return cachedPlayerInventory;
    }

    private static void NormalizeWallet(ref int gold, ref int silver, ref int copper)
    {
        const int rate = 100; // 100 rame = 1 argento, 100 argento = 1 oro
        if (copper >= rate)
        {
            silver += copper / rate;
            copper %= rate;
        }
        if (silver >= rate)
        {
            gold += silver / rate;
            silver %= rate;
        }

        gold = Mathf.Max(0, gold);
        silver = Mathf.Max(0, silver);
        copper = Mathf.Max(0, copper);
    }
}
