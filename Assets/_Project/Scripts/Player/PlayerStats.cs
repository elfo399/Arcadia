using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;

public class PlayerStats : MonoBehaviour, IDamageable
{
    public const int BronzeCoinValue = 1;
    public const int SilverCoinValue = 5;
    public const int GoldCoinValue = 10;
    public const int MaxAllocatableAttributeLevel = 99;
    private const int FirstAttributeSoftCap = 40;
    private const int SecondAttributeSoftCap = 60;
    private const float MidAttributeGrowthMultiplier = 0.5f;
    private const float HighAttributeGrowthMultiplier = 0.25f;
    private const float PhysicalDefensePerEndurance = 0.5f;
    private const float MagicDefensePerMind = 0.5f;
    public static PlayerStats instance;
    public static Func<SavedMerchantStockData[]> MerchantStockSnapshotProvider;

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
    [SerializeField] private float staminaPerEndurance = 3f;
    [SerializeField] private float baseEquipLoad = 20f;
    [SerializeField] private float equipLoadPerEndurance = 1.5f;

    [Header("Flasks")]
    public int maxFlasks = 3;
    public int currentFlasks = 3;
    public float flaskHealAmount = 40f;
    public float flaskUseCooldown = 1f;

    [Header("Economia (Run Corrente)")]
    public int runCoins = 0;
    [HideInInspector] public int runGold = 0;
    [HideInInspector] public int runSilver = 0;
    [HideInInspector] public int runCopper = 0;

    [Header("Banca (Persistente)")]
    public int bankCoins = 0;
    [HideInInspector] public int bankGold = 0;
    [HideInInspector] public int bankSilver = 0;
    [HideInInspector] public int bankCopper = 0;

    [Header("Chiavi")]
    public int currentKeys = 0;

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
    private string playerId;
    private string playerName;
    private string selectedClassId;
    private bool startingClassApplied;
    private bool dungeonCheckpointActive;
    private int dungeonCheckpointFloor = 1;
    private string dungeonCheckpointSeed = string.Empty;
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
    [SerializeField, FormerlySerializedAs("playerCharacterDatabase")] private PlayerClassDatabase playerClassDatabase;
    [SerializeField, FormerlySerializedAs("inspectorStartingCharacter")] private PlayerClassData inspectorStartingClass;
    [SerializeField, FormerlySerializedAs("useInspectorStartingCharacter")] private bool useInspectorStartingClass = true;
    [SerializeField, FormerlySerializedAs("resetSaveFromInspectorCharacterOnPlay")] private bool resetSaveFromInspectorClassOnPlay = false;
    [Header("Save")]
    [SerializeField, Min(0f)] private float minSaveIntervalSeconds = 0.75f;
    private float lastSaveRealtime = -999f;
    private bool saveQueued = false;
    private Coroutine delayedUiRefreshRoutine;
    private bool inspectorStartingClassAppliedThisSession;
    private int temporaryVigorBonus;
    private int temporaryMindBonus;
    private int temporaryEnduranceBonus;
    private int temporaryStrengthBonus;
    private int temporaryDexterityBonus;
    private int temporaryIntelligenceBonus;
    private int temporaryFaithBonus;
    private readonly HashSet<string> storyFlags = new(StringComparer.OrdinalIgnoreCase);
    private readonly DialogueHistory dialogueHistory = new();

    public int TotalArmorPhysicalDefense => totalArmorPhysicalDefense;
    public int TotalArmorMagicDefense => totalArmorMagicDefense;
    public float TotalArmorWeight => totalArmorWeight;
    public string PlayerId => playerId;
    public string PlayerName => ResolvePlayerName();
    public Sprite PlayerPortrait
    {
        get
        {
            PlayerClassData playerClass = ResolveClassDataById(selectedClassId);
            return playerClass != null ? playerClass.portrait : null;
        }
    }
    public string SelectedClassId => selectedClassId;
    public bool HasInspectorStartingClass => useInspectorStartingClass && inspectorStartingClass != null;
    public PlayerClassDatabase ClassDatabase => playerClassDatabase;
    public bool HasActiveDungeonCheckpoint => dungeonCheckpointActive;
    public DialogueHistory DialogueHistory => dialogueHistory;
    /// <summary>
    /// True after deferred quest and inventory data can no longer overwrite
    /// gameplay mutations. Missing legacy save sections use scene defaults and
    /// are considered applied.
    /// </summary>
    public bool IsPersistentStateReady => loadedQuestStateApplied && loadedInventoryStateApplied;
    public GameData LoadedDataSnapshot => loadedDataCache;
    public int EffectiveVigor => Mathf.Max(1, vigor + temporaryVigorBonus);
    public int EffectiveMind => Mathf.Max(1, mind + temporaryMindBonus);
    public int EffectiveEndurance => Mathf.Max(1, endurance + temporaryEnduranceBonus);
    public int EffectiveStrength => Mathf.Max(1, strength + temporaryStrengthBonus);
    public int EffectiveDexterity => Mathf.Max(1, dexterity + temporaryDexterityBonus);
    public int EffectiveIntelligence => Mathf.Max(1, intelligence + temporaryIntelligenceBonus);
    public int EffectiveFaith => Mathf.Max(1, faith + temporaryFaithBonus);

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            MarkPersistentRoot();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
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
        MigrateSerializedWalletsIfNeeded();
        RecalculateDerivedStats(keepCurrentRatio: true);
        RefreshArmorTotals();
        UpdateAllUI();
    }

    private void MarkPersistentRoot()
    {
        if (transform.parent != null)
            transform.SetParent(null, true);

        DontDestroyOnLoad(gameObject);
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

        SyncLegacyWalletFields();
    }

    private void ResetRunWallet()
    {
        runCoins = 0;
        SyncLegacyWalletFields();
    }


    void Update()
    {
        HandleStaminaRegen();
        FlushQueuedSaveIfDue();

        if (flaskTimer > 0f)
            flaskTimer -= Time.deltaTime;
    }

    // --- GESTIONE MONETE ---
    public void AddCoins(int coinAmount, bool save = true)
    {
        int amount = Mathf.Max(0, coinAmount);
        if (amount <= 0)
            return;

        int updated = SaturatingAdd(runCoins, amount);
        if (updated == runCoins)
            return;

        runCoins = updated;
        SyncLegacyWalletFields();
        if (save)
            SaveStats();
    }

    public bool HasCoins(int amount)
    {
        return amount <= 0 || runCoins >= amount;
    }

    public bool TryRemoveCoins(int amount, bool save = true)
    {
        if (amount <= 0 || runCoins < amount)
            return false;

        runCoins -= amount;
        SyncLegacyWalletFields();
        if (save)
            SaveStats();
        return true;
    }

    public bool ModifyKarma(int amount, bool save = true)
    {
        return TryModifyPersistentValue(ref karma, amount, save);
    }

    public bool ModifyBenedetto(int amount, bool save = true)
    {
        return TryModifyPersistentValue(ref benedetto, amount, save);
    }

    public bool ModifyMalefico(int amount, bool save = true)
    {
        return TryModifyPersistentValue(ref malefico, amount, save);
    }

    public bool AddAttributePoints(int amount, bool save = true)
    {
        if (amount <= 0)
            return false;

        int current = Mathf.Max(0, unspentAttributePoints);
        int updated = SaturatingAdd(current, amount);
        if (updated == unspentAttributePoints)
            return false;

        unspentAttributePoints = updated;
        if (save)
            SaveStats();
        return true;
    }

    // --- GESTIONE CHIAVI ---
    public void AddKeys(int amount)
    {
        currentKeys += amount;
        if (currentKeys < 0) currentKeys = 0;
    }

    public bool UseKey()
    {
        if (currentKeys > 0)
        {
            currentKeys--;
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
        int effectiveDefense = GetDefenseForDamageType(damageType);
        amount = ApplyArmorMitigation(amount, damageType);
        if (amount <= 0f) return;

        Debug.Log($"[PlayerStats] Damage taken -> incoming:{incomingAmount:0.##}, afterBlockParry:{preArmorAmount:0.##}, type:{damageType}, defense:{effectiveDefense}, armorPhy:{totalArmorPhysicalDefense}, armorMag:{totalArmorMagicDefense}, final:{amount:0.##}");

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

    public void RestoreFlasks(int amount)
    {
        if (amount <= 0)
            return;

        int maximum = Mathf.Max(0, maxFlasks);
        int current = Mathf.Clamp(currentFlasks, 0, maximum);
        currentFlasks = Mathf.Min(maximum, SaturatingAdd(current, amount));
        UpdateFlaskUI();
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

    public void RestoreStamina(float amount)
    {
        if (amount <= 0f) return;
        currentStamina += amount;
        currentStamina = Mathf.Min(currentStamina, maxStamina);
    }

    public bool TryApplyMagicEffect(MagicItemData magic)
    {
        if (magic == null) return false;

        switch (magic.effectType)
        {
            case MagicItemData.MagicEffectType.HealHealth:
            {
                float amount = Mathf.Max(0f, magic.healAmount);
                if (amount <= 0f) return false;
                RestoreHealth(amount);
                UpdateAllUI();
                Debug.Log($"[PlayerStats] Magic heal -> {magic.magicName} | Health:+{amount:0.##}");
                return true;
            }
            case MagicItemData.MagicEffectType.RestoreMana:
            {
                float amount = Mathf.Max(0f, magic.healAmount);
                if (amount <= 0f) return false;
                RestoreMana(amount);
                UpdateAllUI();
                Debug.Log($"[PlayerStats] Magic mana restore -> {magic.magicName} | Mana:+{amount:0.##}");
                return true;
            }
            case MagicItemData.MagicEffectType.BoostAttribute:
            {
                int amount = magic.boostAmount;
                float duration = Mathf.Max(0f, magic.boostDurationSeconds);
                if (amount == 0 || magic.boostAttribute == MagicItemData.BoostAttribute.None) return false;

                if (duration <= 0f)
                {
                    AddTemporaryMagicBoost(magic.boostAttribute, amount);
                    RecalculateDerivedStats(keepCurrentRatio: true);
                    UpdateAllUI();
                    Debug.Log($"[PlayerStats] Magic boost permanent-runtime -> {magic.magicName} | {magic.boostAttribute}:{amount:+#;-#;0}");
                    return true;
                }

                StartCoroutine(TemporaryMagicBoostRoutine(magic.boostAttribute, amount, duration, magic.magicName));
                return true;
            }
            default:
                return false;
        }
    }

    private IEnumerator TemporaryMagicBoostRoutine(MagicItemData.BoostAttribute attribute, int amount, float duration, string sourceName)
    {
        AddTemporaryMagicBoost(attribute, amount);
        RecalculateDerivedStats(keepCurrentRatio: true);
        UpdateAllUI();
        Debug.Log($"[PlayerStats] Magic boost start -> {sourceName} | {attribute}:{amount:+#;-#;0} | {duration:0.##}s");

        yield return new WaitForSeconds(duration);

        AddTemporaryMagicBoost(attribute, -amount);
        RecalculateDerivedStats(keepCurrentRatio: true);
        UpdateAllUI();
        Debug.Log($"[PlayerStats] Magic boost end -> {sourceName} | {attribute}");
    }

    private void AddTemporaryMagicBoost(MagicItemData.BoostAttribute attribute, int amount)
    {
        switch (attribute)
        {
            case MagicItemData.BoostAttribute.Vigor: temporaryVigorBonus += amount; break;
            case MagicItemData.BoostAttribute.Mind: temporaryMindBonus += amount; break;
            case MagicItemData.BoostAttribute.Endurance: temporaryEnduranceBonus += amount; break;
            case MagicItemData.BoostAttribute.Strength: temporaryStrengthBonus += amount; break;
            case MagicItemData.BoostAttribute.Dexterity: temporaryDexterityBonus += amount; break;
            case MagicItemData.BoostAttribute.Intelligence: temporaryIntelligenceBonus += amount; break;
            case MagicItemData.BoostAttribute.Faith: temporaryFaithBonus += amount; break;
        }
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

    public void SetDungeonCheckpoint(int floor, string seed)
    {
        dungeonCheckpointActive = true;
        dungeonCheckpointFloor = Mathf.Max(1, floor);
        dungeonCheckpointSeed = seed ?? string.Empty;
    }

    public bool TryGetDungeonCheckpoint(out int floor, out string seed)
    {
        floor = Mathf.Max(1, dungeonCheckpointFloor);
        seed = dungeonCheckpointSeed ?? string.Empty;
        return dungeonCheckpointActive;
    }

    public void ClearDungeonCheckpoint()
    {
        dungeonCheckpointActive = false;
        dungeonCheckpointFloor = 1;
        dungeonCheckpointSeed = string.Empty;
    }

    public bool HasStoryFlag(string flagId)
    {
        string normalized = NormalizeStoryFlagId(flagId);
        return normalized.Length > 0 && storyFlags.Contains(normalized);
    }

    public bool SetStoryFlag(string flagId, bool save = true)
    {
        string normalized = NormalizeStoryFlagId(flagId);
        if (normalized.Length == 0 || !storyFlags.Add(normalized))
            return false;

        if (save)
            SaveStats();
        return true;
    }

    public bool ClearStoryFlag(string flagId, bool save = true)
    {
        string normalized = NormalizeStoryFlagId(flagId);
        if (normalized.Length == 0 || !storyFlags.Remove(normalized))
            return false;

        if (save)
            SaveStats();
        return true;
    }

    public string[] GetStoryFlagsSnapshot()
    {
        if (storyFlags.Count == 0)
            return Array.Empty<string>();

        var result = new string[storyFlags.Count];
        storyFlags.CopyTo(result);
        Array.Sort(result, StringComparer.OrdinalIgnoreCase);
        return result;
    }

    public bool HasReadDialogueNode(string conversationId, string nodeId)
    {
        return dialogueHistory.HasReadNode(conversationId, nodeId);
    }

    public bool MarkDialogueNodeRead(string conversationId, string nodeId, bool save = true)
    {
        if (!dialogueHistory.MarkNodeRead(conversationId, nodeId))
            return false;

        if (save)
            SaveStats();
        return true;
    }

    public bool HasSelectedDialogueChoice(string conversationId, string nodeId, string choiceId)
    {
        return dialogueHistory.HasSelectedChoice(conversationId, nodeId, choiceId);
    }

    public bool MarkDialogueChoiceSelected(
        string conversationId,
        string nodeId,
        string choiceId,
        bool save = true)
    {
        if (!dialogueHistory.MarkChoiceSelected(conversationId, nodeId, choiceId))
            return false;

        if (save)
            SaveStats();
        return true;
    }

    public bool ResetNarrativeProgress(bool save = true)
    {
        bool changed = storyFlags.Count > 0
                       || dialogueHistory.ReadNodeCount > 0
                       || dialogueHistory.SelectedChoiceCount > 0;

        storyFlags.Clear();
        dialogueHistory.Clear();

        if (changed && save)
            SaveStats();
        return changed;
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
        bool questStateWasApplied = loadedQuestStateApplied;
        bool inventoryStateWasApplied = loadedInventoryStateApplied;
        GameData data = BuildGameDataSnapshot();
        SaveSystem.SaveData(data);
        loadedDataCache = data;
        loadedQuestStateApplied = questStateWasApplied;
        loadedInventoryStateApplied = inventoryStateWasApplied;
        lastSaveRealtime = Time.unscaledTime;
    }

    private GameData BuildGameDataSnapshot()
    {
        GameData data = new GameData
        {
            saveVersion = SaveSystem.CurrentSaveVersion,
            playerId = SaveSystem.SinglePlayerId,
            playerName = ResolvePlayerName(),
            selectedClassId = selectedClassId,
            startingClassApplied = this.startingClassApplied,
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
            usesUnifiedCoins = true,
            bankCoins = this.bankCoins,
            runCoins = this.runCoins,
            dungeonCheckpointActive = this.dungeonCheckpointActive,
            dungeonFloor = this.dungeonCheckpointFloor,
            dungeonSeed = this.dungeonCheckpointSeed,
            bankGold = this.bankGold,
            bankSilver = this.bankSilver,
            bankCopper = this.bankCopper,
            storyFlags = GetStoryFlagsSnapshot(),
            dialogueHistory = new SavedDialogueHistoryData
            {
                readNodeKeys = dialogueHistory.ExportReadNodeKeys(),
                selectedChoiceKeys = dialogueHistory.ExportSelectedChoiceKeys()
            }
        };
        if (MerchantStockSnapshotProvider != null)
            data.merchantStocks = MerchantStockSnapshotProvider();

        var questManager = GetCachedQuestManager();
        if (questManager != null)
        {
            data.quests = !loadedQuestStateApplied && loadedDataCache != null && loadedDataCache.quests != null
                ? loadedDataCache.quests
                : SerializeQuests(questManager.GetQuestsSnapshot());
        }
        else if (loadedDataCache != null && loadedDataCache.quests != null)
        {
            data.quests = loadedDataCache.quests;
        }

        var playerInventory = GetCachedPlayerInventory();
        if (playerInventory != null)
        {
            data.playerInventory = !loadedInventoryStateApplied && loadedDataCache != null && loadedDataCache.playerInventory != null
                ? loadedDataCache.playerInventory
                : playerInventory.CreateSaveData();
        }
        else if (loadedDataCache != null && loadedDataCache.playerInventory != null)
        {
            data.playerInventory = loadedDataCache.playerInventory;
        }

        return data;
    }

    public void LoadStats()
    {
        string requestedPlayerId = ResolveRequestedPlayerIdForLoad(out _);
        GameData data = SaveSystem.LoadData(requestedPlayerId, allowLegacyFallback: true);

        if (forceStartDataIgnoreSave && data == null)
        {
            loadedDataCache = null;
            ApplySavedNarrativeState(null);
            playerId = SaveSystem.SinglePlayerId;
            playerName = SaveSystem.DefaultPlayerName;
            selectedClassId = string.Empty;
            startingClassApplied = false;
            loadedQuestStateApplied = true;
            loadedInventoryStateApplied = true;
            Debug.Log("ForceStartData attivo e nessun salvataggio trovato: uso dati iniziali da Inspector/StartingLoadout.");
            return;
        }

        if (forceStartDataIgnoreSave)
            Debug.LogWarning("ForceStartData attivo, ma esiste un salvataggio: carico il salvataggio per evitare reset involontari.");

        loadedDataCache = data;
        ApplySavedNarrativeState(data);
        loadedQuestStateApplied = data == null || data.quests == null;
        loadedInventoryStateApplied = data == null || data.playerInventory == null;
        if (data != null)
        {
            playerId = SaveSystem.SinglePlayerId;
            playerName = ResolveLoadedPlayerName(data.playerName, playerId);
            selectedClassId = data.selectedClassId ?? string.Empty;
            startingClassApplied = data.startingClassApplied;
            EnsureSelectedClassIdFallback();
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
            this.runCoins = Mathf.Max(0, data.runCoins);
            this.bankCoins = ResolveSavedBankCoins(data);
            ApplyLoadedDungeonCheckpoint(data);
            ApplyClassBaseResources();
            SyncLegacyWalletFields();
            // Non applicare qui quest/inventory: se avviene prima degli Awake degli altri componenti
            // (es. PlayerInventory), il loro Awake può sovrascrivere i dati caricati.
            // L'applicazione viene fatta in Start() e OnSceneLoaded().
            // Aggiungi qui altre statistiche che vuoi caricare

            Debug.Log("Dati persistenti caricati da file!");
        }
        else
        {
            playerId = SaveSystem.SinglePlayerId;
            playerName = SaveSystem.DefaultPlayerName;
            selectedClassId = string.Empty;
            startingClassApplied = false;
            ClearDungeonCheckpoint();
            Debug.Log("Nessun file di salvataggio trovato. Verranno usati i valori correnti (da Inspector alla prima esecuzione).");
        }
    }

    private string ResolveRequestedPlayerIdForLoad(out bool inspectorClassSelectsSlot)
    {
        inspectorClassSelectsSlot = false;
        return SaveSystem.SinglePlayerId;
    }

    private void ApplyLoadedPlayerData(GameData data, string fallbackPlayerId)
    {
        if (data == null)
            return;

        loadedDataCache = data;
        ApplySavedNarrativeState(data);
        loadedQuestStateApplied = data.quests == null;
        loadedInventoryStateApplied = data.playerInventory == null;

        playerId = SaveSystem.SinglePlayerId;
        playerName = ResolveLoadedPlayerName(data.playerName, playerId);
        selectedClassId = data.selectedClassId ?? string.Empty;
        startingClassApplied = data.startingClassApplied;
        EnsureSelectedClassIdFallback();
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
        this.runCoins = Mathf.Max(0, data.runCoins);
        this.bankCoins = ResolveSavedBankCoins(data);
        ApplyLoadedDungeonCheckpoint(data);
        ApplyClassBaseResources();
        SyncLegacyWalletFields();
    }

    private void ApplyLoadedDungeonCheckpoint(GameData data)
    {
        if (data == null || !data.dungeonCheckpointActive)
        {
            ClearDungeonCheckpoint();
            return;
        }

        dungeonCheckpointActive = true;
        dungeonCheckpointFloor = Mathf.Max(1, data.dungeonFloor);
        dungeonCheckpointSeed = data.dungeonSeed ?? string.Empty;
    }

    public bool TryApplySelectedClassStart(PlayerClassDatabase database)
    {
        bool hasInspectorClass = HasInspectorStartingClass;
        if (database == null && !hasInspectorClass)
            return false;

        string pendingPlayerId = PlayerClassSelection.PendingNewPlayerId;
        bool hasPendingNewPlayer = !string.IsNullOrWhiteSpace(pendingPlayerId);

        bool hasLoadedSave = loadedDataCache != null;
        bool forceInspectorStart = forceStartDataIgnoreSave && !hasLoadedSave;
        bool shouldUseInspectorClass = hasInspectorClass
            && !hasPendingNewPlayer
            && !inspectorStartingClassAppliedThisSession
            && (forceInspectorStart
                || resetSaveFromInspectorClassOnPlay
                || !hasLoadedSave
                || !loadedDataCache.startingClassApplied);

        PlayerClassData playerClass = hasPendingNewPlayer
            ? ResolvePendingStartingClass(database)
            : shouldUseInspectorClass ? inspectorStartingClass : null;

        bool shouldApply = hasPendingNewPlayer
            || shouldUseInspectorClass
            || !hasLoadedSave
            || !loadedDataCache.startingClassApplied;

        if (!shouldApply)
            return false;

        if (playerClass == null)
            playerClass = ResolveDefaultStartingClass(database);
        if (playerClass == null)
            return false;

        ApplyStartingClass(playerClass);
        playerId = SaveSystem.SinglePlayerId;
        selectedClassId = playerClass.GetClassId();
        startingClassApplied = true;
        if (shouldUseInspectorClass)
            inspectorStartingClassAppliedThisSession = true;
        PlayerClassSelection.ClearPendingNewPlayer();
        SaveStatsImmediate();
        return true;
    }

    public void ApplyStartingClass(PlayerClassData playerClass)
    {
        if (playerClass == null)
            return;

        ResetNarrativeProgress(save: false);

        playerId = SaveSystem.SinglePlayerId;
        selectedClassId = playerClass.GetClassId();
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = SaveSystem.DefaultPlayerName;

        playerLevel = Mathf.Max(1, playerClass.startingLevel);
        levelExperience = 0;
        experienceToNextLevel = Mathf.Max(1, playerClass.experienceToNextLevel);
        unspentAttributePoints = 0;

        vigor = Mathf.Max(1, playerClass.vigor);
        mind = Mathf.Max(1, playerClass.mind);
        endurance = Mathf.Max(1, playerClass.endurance);
        strength = Mathf.Max(1, playerClass.strength);
        dexterity = Mathf.Max(1, playerClass.dexterity);
        intelligence = Mathf.Max(1, playerClass.intelligence);
        faith = Mathf.Max(1, playerClass.faith);

        karma = playerClass.karma;
        benedetto = playerClass.benedetto;
        malefico = playerClass.malefico;
        runCoins = 0;
        bankCoins = 0;
        SyncLegacyWalletFields();
        currentKeys = 0;

        baseMaxHealth = Mathf.Max(1f, playerClass.baseMaxHealth);
        baseMaxStamina = Mathf.Max(1f, playerClass.baseMaxStamina);
        baseMaxMana = Mathf.Max(1f, playerClass.baseMaxMana);
        maxFlasks = Mathf.Max(0, playerClass.maxFlasks);
        currentFlasks = maxFlasks;
        flaskHealAmount = Mathf.Max(0f, playerClass.flaskHealAmount);

        RecalculateDerivedStats(keepCurrentRatio: false);
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentMana = maxMana;

        PlayerInventory inventory = GetCachedPlayerInventory();
        if (inventory != null)
            playerClass.ApplyStartingInventory(inventory);

        QuestManager questManager = GetCachedQuestManager();
        if (questManager != null)
            questManager.ResetToInitialQuests();

        RefreshArmorTotals();
        UpdateAllUI();
    }

    public void SetPlayerName(string value, bool save = true)
    {
        string resolvedName = string.IsNullOrWhiteSpace(value) ? ResolvePlayerName() : value.Trim();
        if (string.Equals(playerName, resolvedName, StringComparison.Ordinal))
            return;

        playerName = resolvedName;
        if (save)
            SaveStats();
    }

    private string ResolvePlayerName()
    {
        if (!string.IsNullOrWhiteSpace(playerName))
            return playerName.Trim();

        return ResolveLoadedPlayerName(string.Empty, playerId);
    }

    private string ResolveLoadedPlayerName(string savedName, string fallbackPlayerId)
    {
        if (!string.IsNullOrWhiteSpace(savedName))
            return savedName.Trim();

        return SaveSystem.DefaultPlayerName;
    }

    private static string ResolveClassDisplayName(PlayerClassData playerClass)
    {
        if (playerClass == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(playerClass.displayName))
            return playerClass.displayName.Trim();

        return playerClass.GetClassId();
    }

    private PlayerClassData ResolveClassDataById(string classId)
    {
        return playerClassDatabase != null ? playerClassDatabase.GetById(classId) : null;
    }

    private PlayerClassData ResolvePendingStartingClass(PlayerClassDatabase database = null)
    {
        PlayerClassData pendingClass = PlayerClassSelection.PendingStartingClass;
        if (pendingClass != null)
            return pendingClass;

        string pendingClassId = PlayerClassSelection.PendingStartingClassId;
        if (!string.IsNullOrWhiteSpace(pendingClassId))
        {
            if (database != null)
            {
                PlayerClassData fromOverride = database.GetById(pendingClassId);
                if (fromOverride != null)
                    return fromOverride;
            }

            PlayerClassData fromInspectorDatabase = ResolveClassDataById(pendingClassId);
            if (fromInspectorDatabase != null)
                return fromInspectorDatabase;
        }

        return ResolveDefaultStartingClass(database);
    }

    private PlayerClassData ResolveDefaultStartingClass(PlayerClassDatabase database = null)
    {
        if (database != null && database.DefaultClass != null)
            return database.DefaultClass;

        if (playerClassDatabase != null && playerClassDatabase.DefaultClass != null)
            return playerClassDatabase.DefaultClass;

        return inspectorStartingClass;
    }

    private void EnsureSelectedClassIdFallback()
    {
        if (!string.IsNullOrWhiteSpace(selectedClassId))
            return;

        PlayerClassData fallbackClass = ResolveDefaultStartingClass();
        if (fallbackClass != null)
            selectedClassId = fallbackClass.GetClassId();
    }

    private void ApplyClassBaseResources()
    {
        PlayerClassData playerClass = ResolveDefaultStartingClass();
        if (playerClass == null)
            return;

        baseMaxHealth = Mathf.Max(1f, playerClass.baseMaxHealth);
        baseMaxStamina = Mathf.Max(1f, playerClass.baseMaxStamina);
        baseMaxMana = Mathf.Max(1f, playerClass.baseMaxMana);
        maxFlasks = Mathf.Max(0, playerClass.maxFlasks);
        currentFlasks = maxFlasks;
        flaskHealAmount = Mathf.Max(0f, playerClass.flaskHealAmount);
    }


    public bool TrySpendAttributePoints(IDictionary<string, int> allocation)
    {
        if (allocation == null || allocation.Count == 0) return false;

        var normalizedAllocation = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int totalLevels = 0;

        foreach (var entry in allocation)
        {
            string statName = string.IsNullOrWhiteSpace(entry.Key)
                ? string.Empty
                : entry.Key.Trim().ToLowerInvariant();
            int levels = entry.Value;

            if (!IsLevelBasedAttribute(statName) || levels <= 0) return false;
            if (totalLevels > int.MaxValue - levels) return false;

            normalizedAllocation.TryGetValue(statName, out int existingLevels);
            if (existingLevels > int.MaxValue - levels) return false;
            normalizedAllocation[statName] = existingLevels + levels;
            totalLevels += levels;
        }

        if (totalLevels <= 0 || totalLevels > unspentAttributePoints) return false;

        foreach (var entry in normalizedAllocation)
        {
            int currentValue = GetPersistentStat(entry.Key);
            if (currentValue < 1 || entry.Value > MaxAllocatableAttributeLevel - currentValue)
                return false;
        }

        foreach (var entry in normalizedAllocation)
        {
            switch (entry.Key.ToLowerInvariant())
            {
                case "vigor": vigor += entry.Value; break;
                case "mind": mind += entry.Value; break;
                case "endurance": endurance += entry.Value; break;
                case "strength": strength += entry.Value; break;
                case "dexterity": dexterity += entry.Value; break;
                case "intelligence": intelligence += entry.Value; break;
            }
        }

        unspentAttributePoints -= totalLevels;
        RecalculateDerivedStats(keepCurrentRatio: true);
        UpdateAllUI();
        SaveStatsImmediate();
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
        return GetBasePhysicalDamage(EffectiveStrength);
    }

    public int GetBasePhysicalDamage(int strengthValue)
    {
        return Mathf.Max(1, Mathf.FloorToInt(GetEffectiveAttributeValue(strengthValue)));
    }

    public int GetBaseMagicDamage()
    {
        return GetBaseMagicDamage(EffectiveIntelligence);
    }

    public int GetBaseMagicDamage(int intelligenceValue)
    {
        return Mathf.Max(0, Mathf.FloorToInt(GetEffectiveAttributeValue(intelligenceValue)));
    }

    public int GetBaseRangedDamage()
    {
        return GetBaseRangedDamage(EffectiveDexterity);
    }

    public int GetBaseRangedDamage(int dexterityValue)
    {
        return Mathf.Max(1, Mathf.FloorToInt(GetEffectiveAttributeValue(dexterityValue)));
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
        return GetMaxEquipLoad(EffectiveEndurance);
    }

    public float GetMaxEquipLoad(int enduranceValue)
    {
        float scaledLoad = baseEquipLoad + GetSoftCappedAttributeGrowth(enduranceValue) * equipLoadPerEndurance;
        return Mathf.Max(1f, scaledLoad);
    }

    public float GetMaxHealth(int vigorValue)
    {
        return Mathf.Max(1f, baseMaxHealth + GetSoftCappedAttributeGrowth(vigorValue) * healthPerVigor);
    }

    public float GetMaxMana(int mindValue)
    {
        return Mathf.Max(1f, baseMaxMana + GetSoftCappedAttributeGrowth(mindValue) * manaPerMind);
    }

    public float GetMaxStamina(int enduranceValue)
    {
        return Mathf.Max(1f, baseMaxStamina + GetSoftCappedAttributeGrowth(enduranceValue) * staminaPerEndurance);
    }

    public static float GetEffectiveAttributeValue(int attributeValue)
    {
        return 1f + GetSoftCappedAttributeGrowth(attributeValue);
    }

    private static float GetSoftCappedAttributeGrowth(int attributeValue)
    {
        int rawGrowth = Mathf.Max(0, attributeValue - 1);
        int fullGrowthLimit = FirstAttributeSoftCap - 1;
        int fullGrowth = Mathf.Min(rawGrowth, fullGrowthLimit);
        int midGrowth = Mathf.Min(
            Mathf.Max(0, rawGrowth - fullGrowthLimit),
            SecondAttributeSoftCap - FirstAttributeSoftCap);
        int highGrowth = Mathf.Max(0, rawGrowth - fullGrowthLimit - midGrowth);

        return fullGrowth
               + midGrowth * MidAttributeGrowthMultiplier
               + highGrowth * HighAttributeGrowthMultiplier;
    }

    public int GetPhysicalDefense()
    {
        return GetPhysicalDefense(EffectiveEndurance);
    }

    public int GetPhysicalDefense(int enduranceValue)
    {
        int attributeDefense = Mathf.FloorToInt(GetEffectiveAttributeValue(enduranceValue) * PhysicalDefensePerEndurance);
        return Mathf.Max(0, totalArmorPhysicalDefense + attributeDefense);
    }

    public int GetMagicDefense()
    {
        return GetMagicDefense(EffectiveMind);
    }

    public int GetMagicDefense(int mindValue)
    {
        int mindDefense = Mathf.FloorToInt(GetEffectiveAttributeValue(mindValue) * MagicDefensePerMind);
        int faithDefense = Mathf.FloorToInt(GetEffectiveAttributeValue(EffectiveFaith));
        return Mathf.Max(0, totalArmorMagicDefense + mindDefense + faithDefense);
    }

    public void RecalculateDerivedStats(bool keepCurrentRatio)
    {
        float oldMaxHealth = Mathf.Max(1f, maxHealth);
        float oldMaxMana = Mathf.Max(1f, maxMana);
        float oldMaxStamina = Mathf.Max(1f, maxStamina);

        float healthRatio = keepCurrentRatio ? Mathf.Clamp01(currentHealth / oldMaxHealth) : 1f;
        float manaRatio = keepCurrentRatio ? Mathf.Clamp01(currentMana / oldMaxMana) : 1f;
        float staminaRatio = keepCurrentRatio ? Mathf.Clamp01(currentStamina / oldMaxStamina) : 1f;

        maxHealth = GetMaxHealth(EffectiveVigor);
        maxMana = GetMaxMana(EffectiveMind);
        maxStamina = GetMaxStamina(EffectiveEndurance);

        currentHealth = Mathf.Clamp(maxHealth * healthRatio, 0f, maxHealth);
        currentMana = Mathf.Clamp(maxMana * manaRatio, 0f, maxMana);
        currentStamina = Mathf.Clamp(maxStamina * staminaRatio, 0f, maxStamina);
    }

    private float ApplyArmorMitigation(float amount, WeaponItem.DamageType damageType)
    {
        if (amount <= 0f)
            return 0f;

        int defense = GetDefenseForDamageType(damageType);

        if (defense <= 0)
            return amount;

        float multiplier = 100f / (100f + Mathf.Max(0f, defense));
        return Mathf.Max(0f, amount * multiplier);
    }

    private int GetDefenseForDamageType(WeaponItem.DamageType damageType)
    {
        return damageType == WeaponItem.DamageType.Magic
            ? GetMagicDefense()
            : GetPhysicalDefense();
    }

    void OnApplicationQuit()
    {
        SaveStatsImmediate();
    }

    void Die()
    {
        ResetRunWallet();
        ClearDungeonCheckpoint();
        SaveStatsImmediate();
        Debug.Log("SEI MORTO! Ritorno all'Hub...");
        SceneManager.LoadScene("HubScene");
    }

    private void ApplyLoadedQuestStateIfPossible()
    {
        if (loadedQuestStateApplied) return;
        if (loadedDataCache == null || loadedDataCache.quests == null)
        {
            loadedQuestStateApplied = true;
            return;
        }

        var questManager = GetCachedQuestManager();
        if (questManager == null) return;

        if (loadedDataCache.quests.Length == 0)
        {
            loadedQuestStateApplied = true;
            return;
        }

        var savedQuests = DeserializeQuests(loadedDataCache.quests);
        var mapped = MergeSavedQuestStateIntoDefinitions(
            questManager.GetQuestLoadDefinitionsSnapshot(savedQuests),
            savedQuests);
        questManager.ReplaceAllQuests(mapped);
        loadedQuestStateApplied = true;
    }

    /// <summary>
    /// Applies deferred quest save data before an external system mutates the
    /// QuestManager. This prevents an early Start/OnEnable mutation from being
    /// overwritten when PlayerStats performs its normal deferred load.
    /// </summary>
    public bool EnsureLoadedQuestStateApplied()
    {
        if (loadedQuestStateApplied)
            return true;

        ApplyLoadedQuestStateIfPossible();
        return loadedQuestStateApplied;
    }

    /// <summary>
    /// Retries both deferred save sections and reports whether gameplay systems
    /// may safely mutate quest and inventory state.
    /// </summary>
    public bool TryEnsurePersistentStateReady()
    {
        EnsureLoadedQuestStateApplied();
        ApplyLoadedInventoryStateIfPossible();
        return IsPersistentStateReady;
    }

    private void ApplyLoadedInventoryStateIfPossible()
    {
        if (loadedInventoryStateApplied) return;
        if (loadedDataCache == null || loadedDataCache.playerInventory == null)
        {
            loadedInventoryStateApplied = true;
            return;
        }

        var playerInventory = GetCachedPlayerInventory();
        if (playerInventory == null || !playerInventory.IsInitialized) return;

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
                phase = Mathf.Max(1, obj.phase),
                title = obj.title,
                description = obj.description,
                eventType = obj.eventType.ToString(),
                targetId = obj.targetId,
                targetTag = obj.targetTag,
                requiredAmount = Mathf.Max(1, obj.requiredAmount),
                currentAmount = Mathf.Max(0, obj.currentAmount),
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
            definitions[i].currentAmount = Mathf.Max(0, saved[i].currentAmount);
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
                phase = Mathf.Max(1, obj.phase),
                title = obj.title,
                description = obj.description,
                eventType = ParseSavedQuestObjectiveEventType(obj.eventType),
                targetId = obj.targetId,
                targetTag = obj.targetTag,
                requiredAmount = Mathf.Max(1, obj.requiredAmount),
                currentAmount = Mathf.Max(0, obj.currentAmount),
                completed = obj.completed
            });
        }

        return result;
    }

    private static QuestObjectiveEventType ParseSavedQuestObjectiveEventType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return QuestObjectiveEventType.None;

        return Enum.TryParse(raw.Trim(), true, out QuestObjectiveEventType parsed)
            ? parsed
            : QuestObjectiveEventType.None;
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

    private void ApplySavedNarrativeState(GameData data)
    {
        storyFlags.Clear();
        string[] savedFlags = data != null ? data.storyFlags : null;
        if (savedFlags != null)
        {
            for (int i = 0; i < savedFlags.Length; i++)
            {
                string normalized = NormalizeStoryFlagId(savedFlags[i]);
                if (normalized.Length > 0)
                    storyFlags.Add(normalized);
            }
        }

        SavedDialogueHistoryData savedHistory = data != null ? data.dialogueHistory : null;
        dialogueHistory.Import(
            savedHistory != null ? savedHistory.readNodeKeys : null,
            savedHistory != null ? savedHistory.selectedChoiceKeys : null);
    }

    private bool TryModifyPersistentValue(ref int currentValue, int amount, bool save)
    {
        if (amount == 0)
            return false;

        int updated = SaturatingAdd(currentValue, amount);
        if (updated == currentValue)
            return false;

        currentValue = updated;
        if (save)
            SaveStats();
        return true;
    }

    private static int SaturatingAdd(int currentValue, int amount)
    {
        long result = (long)currentValue + amount;
        if (result > int.MaxValue)
            return int.MaxValue;
        if (result < int.MinValue)
            return int.MinValue;
        return (int)result;
    }

    private static string NormalizeStoryFlagId(string flagId)
    {
        return string.IsNullOrWhiteSpace(flagId) ? string.Empty : flagId.Trim();
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

    private void MigrateSerializedWalletsIfNeeded()
    {
        if (runCoins <= 0 && HasLegacyWalletValue(runGold, runSilver, runCopper))
            runCoins = ConvertCoinTripletToCoins(runGold, runSilver, runCopper);

        if (bankCoins <= 0 && HasLegacyWalletValue(bankGold, bankSilver, bankCopper))
            bankCoins = ConvertCoinTripletToCoins(bankGold, bankSilver, bankCopper);

        runCoins = Mathf.Max(0, runCoins);
        bankCoins = Mathf.Max(0, bankCoins);
        SyncLegacyWalletFields();
    }

    private void SyncLegacyWalletFields()
    {
        runCoins = Mathf.Max(0, runCoins);
        bankCoins = Mathf.Max(0, bankCoins);

        runGold = 0;
        runSilver = 0;
        runCopper = runCoins;

        bankGold = 0;
        bankSilver = 0;
        bankCopper = bankCoins;
    }

    private static int ResolveSavedBankCoins(GameData data)
    {
        if (data == null)
            return 0;

        if (data.usesUnifiedCoins)
            return Mathf.Max(0, data.bankCoins);

        return ConvertCoinTripletToCoins(data.bankGold, data.bankSilver, data.bankCopper);
    }

    private static bool HasLegacyWalletValue(int gold, int silver, int copper)
    {
        return gold > 0 || silver > 0 || copper > 0;
    }

    private static int ConvertCoinTripletToCoins(int gold, int silver, int copper)
    {
        long total = 0;
        total += (long)Mathf.Max(0, gold) * GoldCoinValue;
        total += (long)Mathf.Max(0, silver) * SilverCoinValue;
        total += (long)Mathf.Max(0, copper) * BronzeCoinValue;
        return total > int.MaxValue ? int.MaxValue : (int)total;
    }
}


