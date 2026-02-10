using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

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
    private GameData loadedDataCache;
    private bool loadedQuestStateApplied = false;
    private bool loadedInventoryStateApplied = false;

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
            ,
            bankGold = this.bankGold,
            bankSilver = this.bankSilver,
            bankCopper = this.bankCopper
        };

        var questManager = QuestManager.Instance != null ? QuestManager.Instance : FindObjectOfType<QuestManager>();
        if (questManager != null)
        {
            data.quests = SerializeQuests(questManager.GetQuestsSnapshot());
        }
        var playerInventory = FindObjectOfType<PlayerInventory>();
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
        GameData data = SaveSystem.LoadData();
        loadedDataCache = data;
        loadedQuestStateApplied = false;
        loadedInventoryStateApplied = false;
        if (data != null)
        {
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
        // Converte overflow di rame/argento in tagli superiori (100:1 di default)
        const int rate = 100;
        if (bankCopper >= rate)
        {
            bankSilver += bankCopper / rate;
            bankCopper = bankCopper % rate;
        }
        if (bankSilver >= rate)
        {
            bankGold += bankSilver / rate;
            bankSilver = bankSilver % rate;
        }
        // Nessun prestito: clamp a zero min
        bankGold = Mathf.Max(0, bankGold);
        bankSilver = Mathf.Max(0, bankSilver);
        bankCopper = Mathf.Max(0, bankCopper);
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
        const int rate = 100; // 100 rame = 1 argento, 100 argento = 1 oro

        if (runCopper >= rate)
        {
            runSilver += runCopper / rate;
            runCopper = runCopper % rate;
        }
        if (runSilver >= rate)
        {
            runGold += runSilver / rate;
            runSilver = runSilver % rate;
        }

        runGold = Mathf.Max(0, runGold);
        runSilver = Mathf.Max(0, runSilver);
        runCopper = Mathf.Max(0, runCopper);
    }

    private void NotifyRunWalletChanged()
    {
        OnRunWalletChanged?.Invoke(runGold, runSilver, runCopper);
    }

    private void ApplyLoadedQuestStateIfPossible()
    {
        if (loadedQuestStateApplied) return;
        if (loadedDataCache == null || loadedDataCache.quests == null) return;

        var questManager = QuestManager.Instance != null ? QuestManager.Instance : FindObjectOfType<QuestManager>();
        if (questManager == null) return;

        var mapped = DeserializeQuests(loadedDataCache.quests);
        questManager.ReplaceAllQuests(mapped);
        loadedQuestStateApplied = true;
    }

    private void ApplyLoadedInventoryStateIfPossible()
    {
        if (loadedInventoryStateApplied) return;
        if (loadedDataCache == null || loadedDataCache.playerInventory == null) return;

        var playerInventory = FindObjectOfType<PlayerInventory>();
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
}
