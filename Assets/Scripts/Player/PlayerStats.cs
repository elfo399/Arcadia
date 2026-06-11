using UnityEngine;
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
    [SerializeField] private float baseEquipLoad = 40f;
    [SerializeField] private float equipLoadPerEndurance = 2f;

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
    public event Action<int> OnKeysChanged;

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

    [Header("Combat Flags")]
    [SerializeField] private bool invulnerable;

    [Header("Armor Totals (Runtime)")]
    [SerializeField] private int totalArmorPhysicalDefense;
    [SerializeField] private int totalArmorMagicDefense;
    [SerializeField] private float totalArmorWeight;

    [Header("Equip Load")]
    [Range(0f, 1f)] [SerializeField] private float unequippedInventoryWeightMultiplier = 0.2f;

    private float lastStaminaUseTime;
    private float flaskTimer;
    private Animator animator;
    private GameData loadedDataCache;
    private string selectedCharacterId;
    private bool selectedCharacterStartApplied;
    private bool loadedQuestStateApplied = false;
    private bool loadedInventoryStateApplied = false;
    private float baseMaxHealth;
    private float baseMaxStamina;
    private float baseMaxMana;
    private QuestManager cachedQuestManager;
    private PlayerInventory cachedPlayerInventory;
    private PlayerCombat cachedPlayerCombat;
    [Header("Debug / Bootstrap")]
    [SerializeField] private bool forceStartDataIgnoreSave = false;
    [Header("Character Start")]
    [SerializeField] private PlayerCharacterData inspectorStartingCharacter;
    [SerializeField] private bool useInspectorStartingCharacter = true;
    [SerializeField] private bool resetSaveFromInspectorCharacterOnPlay = false;
    [Header("Save")]
    [SerializeField, Min(0f)] private float minSaveIntervalSeconds = 0.75f;
    private float lastSaveRealtime = -999f;
    private bool saveQueued = false;
    private Coroutine delayedUiRefreshRoutine;
    private bool inspectorStartingCharacterAppliedThisSession;

    public int TotalArmorPhysicalDefense => totalArmorPhysicalDefense;
    public int TotalArmorMagicDefense => totalArmorMagicDefense;
    public float TotalArmorWeight => totalArmorWeight;
    public string SelectedCharacterId => selectedCharacterId;
    public bool HasInspectorStartingCharacter => useInspectorStartingCharacter && inspectorStartingCharacter != null;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            MarkPersistentRoot();
        }
        else if (instance != this)
        {
            GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
            Destroy(duplicateRoot);
            return; 
        }

        animator = GetComponentInChildren<Animator>();
        cachedPlayerCombat = GetComponent<PlayerCombat>();

        baseMaxHealth = maxHealth;
        baseMaxStamina = maxStamina;
        baseMaxMana = maxMana;

        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentMana = maxMana;
        currentFlasks = maxFlasks;

        LoadStats();
        RecalculateDerivedStats(keepCurrentRatio: true);
        RefreshArmorTotals();
        UpdateAllUI();
        NotifyBankChanged();
        NotifyRunWalletChanged();
        NotifyKeysChanged();
    }

    private void MarkPersistentRoot()
    {
        Transform root = transform.root;
        if (root == null)
            return;

        DontDestroyOnLoad(root.gameObject);
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
        RefreshArmorTotals();
        UpdateAllUI();
        if (delayedUiRefreshRoutine != null)
            StopCoroutine(delayedUiRefreshRoutine);
        delayedUiRefreshRoutine = StartCoroutine(RefreshUiBindingsNextFrame());
        ApplyLoadedQuestStateIfPossible();
        ApplyLoadedInventoryStateIfPossible();

        // Ogni run parte senza monete portatili
        if (scene.name == "GameScene")
        {
            ResetRunWallet();
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
        FlushQueuedSaveIfDue();

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
        if (currentKeys < 0) currentKeys = 0;
        NotifyKeysChanged();
    }

    public bool UseKey()
    {
        if (currentKeys > 0)
        {
            currentKeys--;
            NotifyKeysChanged();
            return true;
        }
        return false;
    }

    // --- DANNO & VITA (IDamageable) ---
    public void TakeDamage(int amount)
    {
        TakeDamage((float)amount);
    }

    private System.Collections.IEnumerator RefreshUiBindingsNextFrame()
    {
        yield return null;
        UpdateAllUI();
        delayedUiRefreshRoutine = null;
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, WeaponItem.DamageType.Physical, null);
    }

    public void TakeDamage(float amount, WeaponItem.DamageType damageType, Vector3? sourcePosition, Transform attacker = null)
    {
        if (amount <= 0f) return;
        if (invulnerable) return;

        float incomingAmount = amount;

        if (cachedPlayerCombat == null)
            cachedPlayerCombat = GetComponent<PlayerCombat>();
        if (cachedPlayerCombat != null)
            cachedPlayerCombat.TryDefendIncomingDamage(ref amount, damageType, sourcePosition, attacker);
        if (amount <= 0f) return;

        float preArmorAmount = amount;
        RefreshArmorTotals();
        int armorDefense = GetArmorDefenseForDamageType(damageType);
        amount = ApplyArmorMitigation(amount, damageType);
        if (amount <= 0f) return;

        Debug.Log($"[PlayerStats] Damage taken -> incoming:{incomingAmount:0.##}, afterBlockParry:{preArmorAmount:0.##}, type:{damageType}, armorDef:{armorDefense}, armorPhy:{totalArmorPhysicalDefense}, armorMag:{totalArmorMagicDefense}, final:{amount:0.##}");

        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;

        if (currentHealth <= 0) Die();
    }

    public void RestoreHealth(float amount)
    {
        if (amount <= 0f) return;
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
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
        }
    }

    public bool UseMana(float amount)
    {
        if (currentMana < amount) return false;
        currentMana -= amount;
        return true;
    }

    public void RestoreMana(float amount)
    {
        if (amount <= 0f) return;
        currentMana += amount;
        currentMana = Mathf.Min(currentMana, maxMana);
    }

    // --- AGGIORNAMENTO GRAFICO ---

    void UpdateAllUI()
    {
        RefreshArmorTotals();
        UpdateFlaskUI();
    }

    void UpdateFlaskUI()
    {
    }

    // --- SALVATAGGIO E CARICAMENTO STATS ---
    public void SaveStats()
    {
        RequestSave(immediate: false);
    }

    public void SaveStatsImmediate()
    {
        RequestSave(immediate: true);
    }

    private void RequestSave(bool immediate)
    {
        if (immediate)
        {
            saveQueued = false;
            PerformSave();
            return;
        }

        float now = Time.unscaledTime;
        if (now >= lastSaveRealtime + Mathf.Max(0f, minSaveIntervalSeconds))
        {
            PerformSave();
            return;
        }

        saveQueued = true;
    }

    private void FlushQueuedSaveIfDue()
    {
        if (!saveQueued) return;
        float now = Time.unscaledTime;
        if (now < lastSaveRealtime + Mathf.Max(0f, minSaveIntervalSeconds)) return;

        saveQueued = false;
        PerformSave();
    }

    private void PerformSave()
    {
        GameData data = BuildGameDataSnapshot();
        SaveSystem.SaveData(data);
        loadedDataCache = data;
        loadedQuestStateApplied = true;
        loadedInventoryStateApplied = true;
        lastSaveRealtime = Time.unscaledTime;
    }

    private GameData BuildGameDataSnapshot()
    {
        GameData data = new GameData
        {
            selectedCharacterId = this.selectedCharacterId,
            selectedCharacterStartApplied = this.selectedCharacterStartApplied,
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
            malefico = this.malefico,
            bankGold = this.bankGold,
            bankSilver = this.bankSilver,
            bankCopper = this.bankCopper
        };

        var questManager = GetCachedQuestManager();
        if (questManager != null)
            data.quests = SerializeQuests(questManager.GetQuestsSnapshot());

        var playerInventory = GetCachedPlayerInventory();
        if (playerInventory != null)
            data.playerInventory = playerInventory.CreateSaveData();

        return data;
    }

    public void LoadStats()
    {
        if (forceStartDataIgnoreSave)
        {
            loadedDataCache = null;
            selectedCharacterId = string.Empty;
            selectedCharacterStartApplied = false;
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
            selectedCharacterId = data.selectedCharacterId;
            selectedCharacterStartApplied = data.selectedCharacterStartApplied;
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
            selectedCharacterId = string.Empty;
            selectedCharacterStartApplied = false;
            Debug.Log("Nessun file di salvataggio trovato. Verranno usati i valori correnti (da Inspector alla prima esecuzione).");
        }
    }

    public bool TryApplySelectedCharacterStart(PlayerCharacterDatabase database)
    {
        bool hasInspectorCharacter = HasInspectorStartingCharacter;
        if (database == null && !hasInspectorCharacter)
            return false;

        string pendingCharacterId = PlayerCharacterSelection.PendingNewCharacterId;
        bool hasPendingNewCharacter = !string.IsNullOrWhiteSpace(pendingCharacterId);
        bool hasLoadedSave = !forceStartDataIgnoreSave && loadedDataCache != null;
        bool shouldUseInspectorCharacter = hasInspectorCharacter
            && !hasPendingNewCharacter
            && !inspectorStartingCharacterAppliedThisSession
            && (forceStartDataIgnoreSave
                || resetSaveFromInspectorCharacterOnPlay
                || !hasLoadedSave
                || !loadedDataCache.selectedCharacterStartApplied
                || string.IsNullOrWhiteSpace(loadedDataCache.selectedCharacterId));

        PlayerCharacterData character = shouldUseInspectorCharacter ? inspectorStartingCharacter : null;
        string requestedCharacterId;
        if (hasPendingNewCharacter)
            requestedCharacterId = pendingCharacterId;
        else if (shouldUseInspectorCharacter)
            requestedCharacterId = character.GetCharacterId();
        else if (hasLoadedSave)
            requestedCharacterId = loadedDataCache.selectedCharacterId;
        else
            requestedCharacterId = PlayerCharacterSelection.GetSelectedCharacterId();

        bool shouldApply = hasPendingNewCharacter
            || shouldUseInspectorCharacter
            || !hasLoadedSave
            || (!loadedDataCache.selectedCharacterStartApplied && !string.IsNullOrWhiteSpace(requestedCharacterId));

        if (!shouldApply)
            return false;

        if (character == null)
            character = database != null ? database.GetById(requestedCharacterId) : null;
        if (character == null)
            return false;

        ApplyStartingCharacter(character);
        selectedCharacterId = character.GetCharacterId();
        selectedCharacterStartApplied = true;
        if (shouldUseInspectorCharacter)
            inspectorStartingCharacterAppliedThisSession = true;
        PlayerCharacterSelection.ClearPendingNewCharacter(selectedCharacterId);
        SaveStatsImmediate();
        return true;
    }

    public void ApplyStartingCharacter(PlayerCharacterData character)
    {
        if (character == null)
            return;

        selectedCharacterId = character.GetCharacterId();

        playerLevel = Mathf.Max(1, character.startingLevel);
        levelExperience = 0;
        experienceToNextLevel = Mathf.Max(1, character.experienceToNextLevel);
        unspentAttributePoints = 0;

        vigor = Mathf.Max(1, character.vigor);
        mind = Mathf.Max(1, character.mind);
        endurance = Mathf.Max(1, character.endurance);
        strength = Mathf.Max(1, character.strength);
        dexterity = Mathf.Max(1, character.dexterity);
        intelligence = Mathf.Max(1, character.intelligence);
        faith = Mathf.Max(1, character.faith);

        karma = character.karma;
        benedetto = character.benedetto;
        malefico = character.malefico;
        runGold = 0;
        runSilver = 0;
        runCopper = 0;
        bankGold = 0;
        bankSilver = 0;
        bankCopper = 0;
        currentKeys = 0;

        baseMaxHealth = Mathf.Max(1f, character.baseMaxHealth);
        baseMaxStamina = Mathf.Max(1f, character.baseMaxStamina);
        baseMaxMana = Mathf.Max(1f, character.baseMaxMana);
        maxFlasks = Mathf.Max(0, character.maxFlasks);
        currentFlasks = maxFlasks;
        flaskHealAmount = Mathf.Max(0f, character.flaskHealAmount);

        RecalculateDerivedStats(keepCurrentRatio: false);
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentMana = maxMana;

        PlayerInventory inventory = GetCachedPlayerInventory();
        if (inventory != null)
            character.ApplyStartingInventory(inventory);

        QuestManager questManager = GetCachedQuestManager();
        if (questManager != null)
            questManager.ResetToInitialQuests();

        RefreshArmorTotals();
        UpdateAllUI();
        NotifyRunWalletChanged();
        NotifyBankChanged();
        NotifyKeysChanged();
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

        RefreshArmorTotals();

        float load = 0f;
        var items = inventory.Items;
        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null) continue;

                int qty = Mathf.Max(1, it.amount);
                float unitWeight = GetInventoryEntryUnitWeight(it);
                if (unitWeight <= 0f)
                    continue;

                int equippedUnits = GetEquippedUnitsForLoad(it, inventory, qty);
                int unequippedUnits = Mathf.Max(0, qty - equippedUnits);

                load += unitWeight * equippedUnits;
                load += unitWeight * unequippedInventoryWeightMultiplier * unequippedUnits;
            }
        }

        load += totalArmorWeight;
        return load;
    }

    public void RefreshArmorTotals(bool logTotals = false, string reason = null)
    {
        int oldPhysical = totalArmorPhysicalDefense;
        int oldMagic = totalArmorMagicDefense;
        float oldWeight = totalArmorWeight;

        var inventory = GetCachedPlayerInventory();
        int physical = 0;
        int magic = 0;
        float weight = 0f;

        if (inventory != null && inventory.armorLoadout != null)
        {
            for (int i = 0; i < inventory.armorLoadout.Length; i++)
            {
                ArmorItemData armor = inventory.armorLoadout[i];
                if (armor == null)
                    continue;

                physical += Mathf.Max(0, armor.physicalDefense);
                magic += Mathf.Max(0, armor.magicDefense);
                weight += Mathf.Max(0f, armor.weight);
            }
        }

        totalArmorPhysicalDefense = physical;
        totalArmorMagicDefense = magic;
        totalArmorWeight = weight;

        if (logTotals)
        {
            string context = string.IsNullOrWhiteSpace(reason) ? "refresh" : reason;
            bool changed = oldPhysical != totalArmorPhysicalDefense
                || oldMagic != totalArmorMagicDefense
                || !Mathf.Approximately(oldWeight, totalArmorWeight);

            Debug.Log($"[PlayerStats] Armor totals ({context}) -> phyDef:{totalArmorPhysicalDefense}, magDef:{totalArmorMagicDefense}, weight:{totalArmorWeight:0.##}, changed:{changed}");
        }
    }

    private float GetInventoryEntryUnitWeight(InventoryItem item)
    {
        if (item == null)
            return 0f;

        if (item.weaponData != null)
            return Mathf.Max(0f, item.weaponData.weight);

        if (item.armorData != null)
            return Mathf.Max(0f, item.armorData.weight);

        if (item.usableData != null)
            return Mathf.Max(0f, item.usableData.weight);

        if (item.itemData != null)
            return Mathf.Max(0f, item.itemData.weight);

        return 0f;
    }

    private int GetEquippedUnitsForLoad(InventoryItem item, PlayerInventory inventory, int quantity)
    {
        if (item == null || inventory == null || quantity <= 0)
            return 0;

        if (item.armorData != null)
            return inventory.IsArmorInstanceEquipped(item.instanceId) ? 1 : 0;

        if (item.weaponData != null || item.usableData != null || item.magicData != null)
            return inventory.IsInstanceEquipped(item.instanceId) ? 1 : 0;

        return 0;
    }

    public float GetMaxEquipLoad()
    {
        float scaledLoad = baseEquipLoad + Mathf.Max(0, endurance - 1) * equipLoadPerEndurance;
        return Mathf.Max(1f, scaledLoad);
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

    private float ApplyArmorMitigation(float amount, WeaponItem.DamageType damageType)
    {
        if (amount <= 0f)
            return 0f;

        int defense = GetArmorDefenseForDamageType(damageType);

        if (defense <= 0)
            return amount;

        float multiplier = 100f / (100f + Mathf.Max(0f, defense));
        return Mathf.Max(0f, amount * multiplier);
    }

    private int GetArmorDefenseForDamageType(WeaponItem.DamageType damageType)
    {
        return damageType == WeaponItem.DamageType.Magic
            ? totalArmorMagicDefense
            : totalArmorPhysicalDefense;
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
        SaveStatsImmediate();
    }

    void Die()
    {
        SaveStatsImmediate();
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

    private void NotifyKeysChanged()
    {
        OnKeysChanged?.Invoke(currentKeys);
    }

    private void ApplyLoadedQuestStateIfPossible()
    {
        if (loadedQuestStateApplied) return;
        if (loadedDataCache == null || loadedDataCache.quests == null) return;

        var questManager = GetCachedQuestManager();
        if (questManager == null) return;

        if (loadedDataCache.quests.Length == 0)
        {
            loadedQuestStateApplied = true;
            return;
        }

        var mapped = MergeSavedQuestStateIntoDefinitions(
            questManager.GetInitialQuestsSnapshot(),
            DeserializeQuests(loadedDataCache.quests));
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
                rewardClaimed = q.rewardClaimed,
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
                type = ResolveSavedQuestRewardType(r),
                amount = r.amount,
                itemName = ResolveSavedQuestRewardItemName(r)
            };
        }

        return result;
    }

    private static List<QuestManager.QuestData> DeserializeQuests(SavedQuestData[] source)
    {
        var result = new List<QuestManager.QuestData>();
        if (source == null || source.Length == 0) return result;
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
                rewardClaimed = q.rewardClaimed || (q.completed && (q.rewards == null || q.rewards.Length == 0)),
                questTypeLabel = q.questTypeLabel,
                recommendedLabel = q.recommendedLabel,
                loreTitle = q.loreTitle,
                loreDescription = q.loreDescription,
                loreAuthor = q.loreAuthor,
                objectives = DeserializeObjectives(q.objectives),
                rewards = DeserializeRewards(q.rewards)
            });
        }

        return result;
    }

    private static List<QuestManager.QuestData> MergeSavedQuestStateIntoDefinitions(List<QuestManager.QuestData> definitions, List<QuestManager.QuestData> saved)
    {
        if (definitions == null || definitions.Count == 0)
            return saved ?? new List<QuestManager.QuestData>();
        if (saved == null || saved.Count == 0)
            return definitions;

        for (int i = 0; i < saved.Count; i++)
        {
            var savedQuest = saved[i];
            if (savedQuest == null || string.IsNullOrWhiteSpace(savedQuest.questId))
                continue;

            var definedQuest = FindQuestById(definitions, savedQuest.questId);
            if (definedQuest == null)
            {
                definitions.Add(savedQuest);
                continue;
            }

            definedQuest.completed = savedQuest.completed;
            definedQuest.rewardClaimed = savedQuest.rewardClaimed;
            ApplySavedObjectiveState(definedQuest.objectives, savedQuest.objectives);
        }

        return definitions;
    }

    private static QuestManager.QuestData FindQuestById(List<QuestManager.QuestData> quests, string questId)
    {
        if (quests == null || string.IsNullOrWhiteSpace(questId))
            return null;

        for (int i = 0; i < quests.Count; i++)
        {
            var quest = quests[i];
            if (quest == null) continue;
            if (string.Equals(quest.questId, questId, StringComparison.OrdinalIgnoreCase))
                return quest;
        }

        return null;
    }

    private static void ApplySavedObjectiveState(List<QuestManager.QuestObjectiveData> definitions, List<QuestManager.QuestObjectiveData> saved)
    {
        if (definitions == null || definitions.Count == 0 || saved == null || saved.Count == 0)
            return;

        int count = Mathf.Min(definitions.Count, saved.Count);
        for (int i = 0; i < count; i++)
        {
            if (definitions[i] == null || saved[i] == null)
                continue;
            definitions[i].completed = saved[i].completed;
        }
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

    private static List<QuestManager.QuestRewardData> DeserializeRewards(SavedQuestRewardData[] source)
    {
        var result = new List<QuestManager.QuestRewardData>();
        if (source == null || source.Length == 0) return result;

        for (int i = 0; i < source.Length; i++)
        {
            var r = source[i];
            if (r == null) continue;
            result.Add(new QuestManager.QuestRewardData
            {
                type = r.type,
                amount = r.amount,
                itemName = r.itemName,
                rewardType = ParseSavedQuestRewardType(r.type)
            });
        }

        return result;
    }

    private static QuestRewardType ParseSavedQuestRewardType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return QuestRewardType.Item;

        if (Enum.TryParse(raw.Trim(), true, out QuestRewardType parsed))
            return parsed;

        string lowered = raw.Trim().ToLowerInvariant();
        if (lowered.Contains("weapon")) return QuestRewardType.Weapon;
        if (lowered.Contains("usable") || lowered.Contains("consumable") || lowered.Contains("potion")) return QuestRewardType.Usable;
        if (lowered.Contains("magic") || lowered.Contains("spell") || lowered.Contains("magia")) return QuestRewardType.Magic;
        if (lowered.Contains("armor") || lowered.Contains("helmet") || lowered.Contains("chestplate") || lowered.Contains("leggings") || lowered.Contains("boots")) return QuestRewardType.Armor;
        if (lowered.Contains("experience") || lowered == "xp" || lowered.Contains("exp") || lowered.Contains("esperienza")) return QuestRewardType.Experience;
        return QuestRewardType.Item;
    }

    private static string ResolveSavedQuestRewardType(QuestManager.QuestRewardData reward)
    {
        if (reward == null)
            return QuestRewardType.Item.ToString();
        return !string.IsNullOrWhiteSpace(reward.type) ? reward.type : reward.rewardType.ToString();
    }

    private static string ResolveSavedQuestRewardItemName(QuestManager.QuestRewardData reward)
    {
        if (reward == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(reward.itemName))
            return reward.itemName;

        switch (reward.rewardType)
        {
            case QuestRewardType.Weapon: return reward.weaponAsset != null ? reward.weaponAsset.weaponName : string.Empty;
            case QuestRewardType.Usable: return reward.usableAsset != null ? reward.usableAsset.itemName : string.Empty;
            case QuestRewardType.Item: return reward.itemAsset != null ? reward.itemAsset.itemName : string.Empty;
            case QuestRewardType.Magic: return reward.magicAsset != null ? reward.magicAsset.magicName : string.Empty;
            case QuestRewardType.Armor: return reward.armorAsset != null ? reward.armorAsset.itemName : string.Empty;
            default: return string.Empty;
        }
    }
    private QuestManager GetCachedQuestManager()
    {
        if (cachedQuestManager != null) return cachedQuestManager;
        cachedQuestManager = QuestManager.Instance;
        return cachedQuestManager;
    }

    private PlayerInventory GetCachedPlayerInventory()
    {
        if (cachedPlayerInventory != null) return cachedPlayerInventory;
        cachedPlayerInventory = GetComponent<PlayerInventory>();
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


