using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class PlayerUI : MonoBehaviour
{
    [Header("References")]
    // Stats source used to fill UI values
    public PlayerStats playerStats;
    // Inventory source used to display equipped weapons
    public PlayerInventory playerInventory;

    [Header("Bars")]
    // Fill image for the health bar
    public Image healthBarFill;
    // Fill image for the stamina bar
    public Image staminaBarFill;
    // Fill image for the mana bar
    public Image manaBarFill;

    [Header("Bar Frames")]
    public RectTransform healthBarFrame;
    public RectTransform staminaBarFrame;
    public RectTransform manaBarFrame;

    [Header("Dynamic Bars")]
    public DynamicBar healthBar;
    public DynamicBar staminaBar;
    public DynamicBar manaBar;

    [Header("Bar Sizing")]
    [SerializeField] private float healthBaseWidth = 140f;
    [SerializeField] private float staminaBaseWidth = 140f;
    [SerializeField] private float manaBaseWidth = 120f;
    [SerializeField] private float healthWidthPerPoint = 0.9f;
    [SerializeField] private float staminaWidthPerPoint = 0.7f;
    [SerializeField] private float manaWidthPerPoint = 0.9f;
    [SerializeField] private float barWidthScale = 0.82f;
    [SerializeField] private float minBarWidth = 100f;
    [SerializeField] private float maxBarWidth = 320f;
    [SerializeField] private float fillHorizontalPadding = 4f;

    [Header("Flasks")]
    // Text element showing current flask count
    public TextMeshProUGUI flaskCountText;
    public TextMeshProUGUI keyCountText;

    [Header("Bottom Weapon Slots (D-Pad)")]
    // Icon shown for the left-hand weapon
    public Image slotLeftIcon;
    // Icon shown for the right-hand weapon
    public Image slotRightIcon;
    private InventorySlot leftFrontSlot;
    private InventorySlot rightFrontSlot;
    private Sprite lastLeftWeaponIcon;
    private Sprite lastRightWeaponIcon;
    private float lastHealthFill = -1f;
    private float lastStaminaFill = -1f;
    private float lastManaFill = -1f;
    private float lastHealthMax = -1f;
    private float lastStaminaMax = -1f;
    private float lastManaMax = -1f;
    private int lastFlaskCount = int.MinValue;
    private int lastKeyCount = int.MinValue;

    // Update UI elements each frame
    void Update()
    {
        if (playerStats != null)
        {
            UpdateBars();
            UpdateFlasks();
        }

        if (playerInventory != null)
        {
            UpdateWeaponSlots();
        }
    }

    void Awake()
    {
        leftFrontSlot = ResolveFrontSlot(slotLeftIcon);
        rightFrontSlot = ResolveFrontSlot(slotRightIcon);
        ResolveReferences();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResolveReferences();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveReferences();
        leftFrontSlot = ResolveFrontSlot(slotLeftIcon);
        rightFrontSlot = ResolveFrontSlot(slotRightIcon);
    }

    private void ResolveReferences()
    {
        if (playerStats == null)
            playerStats = PlayerStats.instance;
        healthBarFrame = ResolveBarFrame(healthBarFill, healthBarFrame);
        staminaBarFrame = ResolveBarFrame(staminaBarFill, staminaBarFrame);
        manaBarFrame = ResolveBarFrame(manaBarFill, manaBarFrame);

        healthBar = ResolveDynamicBar(healthBarFill, healthBar);
        staminaBar = ResolveDynamicBar(staminaBarFill, staminaBar);
        manaBar = ResolveDynamicBar(manaBarFill, manaBar);
    }

    private RectTransform ResolveBarFrame(Image fill, RectTransform currentFrame)
    {
        if (fill == null)
            return currentFrame;

        RectTransform parentFrame = fill.transform.parent as RectTransform;
        if (parentFrame != null)
            return parentFrame;

        return currentFrame;
    }

    private DynamicBar ResolveDynamicBar(Image fill, DynamicBar currentBar)
    {
        if (currentBar != null)
            return currentBar;

        if (fill == null)
            return null;

        return fill.GetComponentInParent<DynamicBar>();
    }

    // Refresh health, stamina, and mana bar fill amounts
    void UpdateBars()
    {
        bool useHealthDynamicBar = ShouldUseDynamicBar(healthBar);
        bool useStaminaDynamicBar = ShouldUseDynamicBar(staminaBar);
        bool useManaDynamicBar = ShouldUseDynamicBar(manaBar);

        if (!Mathf.Approximately(playerStats.maxHealth, lastHealthMax))
        {
            if (useHealthDynamicBar)
                healthBar.SetMax(playerStats.maxHealth, CalculateBarWidth(healthBaseWidth, healthWidthPerPoint, playerStats.maxHealth));
            else
            {
                ResizeBar(healthBarFrame, healthBaseWidth, healthWidthPerPoint, playerStats.maxHealth);
                SyncFillToFrame(healthBarFill, healthBarFrame);
            }
            lastHealthMax = playerStats.maxHealth;
        }

        if (useHealthDynamicBar || healthBarFill != null)
        {
            float t = playerStats.currentHealth / playerStats.maxHealth;
            float v = Mathf.Clamp01(t);
            if (!Mathf.Approximately(v, lastHealthFill))
            {
                if (useHealthDynamicBar)
                    healthBar.SetCurrent(playerStats.currentHealth);
                else
                    healthBarFill.fillAmount = v;
                lastHealthFill = v;
            }
        }

        if (!Mathf.Approximately(playerStats.maxStamina, lastStaminaMax))
        {
            if (useStaminaDynamicBar)
                staminaBar.SetMax(playerStats.maxStamina, CalculateBarWidth(staminaBaseWidth, staminaWidthPerPoint, playerStats.maxStamina));
            else
            {
                ResizeBar(staminaBarFrame, staminaBaseWidth, staminaWidthPerPoint, playerStats.maxStamina);
                SyncFillToFrame(staminaBarFill, staminaBarFrame);
            }
            lastStaminaMax = playerStats.maxStamina;
        }

        if (useStaminaDynamicBar || staminaBarFill != null)
        {
            float t = playerStats.currentStamina / playerStats.maxStamina;
            float v = Mathf.Clamp01(t);
            if (!Mathf.Approximately(v, lastStaminaFill))
            {
                if (useStaminaDynamicBar)
                    staminaBar.SetCurrent(playerStats.currentStamina);
                else
                    staminaBarFill.fillAmount = v;
                lastStaminaFill = v;
            }
        }

        if (!Mathf.Approximately(playerStats.maxMana, lastManaMax))
        {
            if (useManaDynamicBar)
                manaBar.SetMax(playerStats.maxMana, CalculateBarWidth(manaBaseWidth, manaWidthPerPoint, playerStats.maxMana));
            else
            {
                ResizeBar(manaBarFrame, manaBaseWidth, manaWidthPerPoint, playerStats.maxMana);
                SyncFillToFrame(manaBarFill, manaBarFrame);
            }
            lastManaMax = playerStats.maxMana;
        }

        if (useManaDynamicBar || manaBarFill != null)
        {
            float t = playerStats.currentMana / playerStats.maxMana;
            float v = Mathf.Clamp01(t);
            if (!Mathf.Approximately(v, lastManaFill))
            {
                if (useManaDynamicBar)
                    manaBar.SetCurrent(playerStats.currentMana);
                else
                    manaBarFill.fillAmount = v;
                lastManaFill = v;
            }
        }
    }

    private static bool ShouldUseDynamicBar(DynamicBar bar)
    {
        return bar != null && bar.UsesSegmentedLayout;
    }

    private void ResizeBar(RectTransform frame, float baseWidth, float widthPerPoint, float maxValue)
    {
        if (frame == null)
            return;

        float width = CalculateBarWidth(baseWidth, widthPerPoint, maxValue);
        frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    private float CalculateBarWidth(float baseWidth, float widthPerPoint, float maxValue)
    {
        float width = baseWidth + maxValue * widthPerPoint;
        width *= Mathf.Max(0.1f, barWidthScale);
        return Mathf.Clamp(width, minBarWidth, maxBarWidth);
    }

    private void SyncFillToFrame(Image fill, RectTransform frame)
    {
        if (fill == null || frame == null)
            return;

        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.offsetMin = new Vector2(fillHorizontalPadding, 4f);
        fillRect.offsetMax = new Vector2(-fillHorizontalPadding, -4f);
    }

    // Update the flask counter display
    void UpdateFlasks()
    {
        if (flaskCountText != null)
        {
            if (playerStats.currentFlasks != lastFlaskCount)
            {
                flaskCountText.text = "x" + playerStats.currentFlasks.ToString();
                lastFlaskCount = playerStats.currentFlasks;
            }
        }

        if (keyCountText != null)
        {
            if (playerStats.currentKeys != lastKeyCount)
            {
                keyCountText.text = "x" + playerStats.currentKeys.ToString();
                lastKeyCount = playerStats.currentKeys;
            }
        }
    }

    // Display the icons for equipped left and right weapons
    void UpdateWeaponSlots()
    {
        WeaponItem rightWeapon = playerInventory.GetWeaponForHand(Hand.Right);
        WeaponItem leftWeapon = playerInventory.GetWeaponForHand(Hand.Left);
        Sprite rightIcon = rightWeapon != null ? rightWeapon.icon : null;
        Sprite leftIcon = leftWeapon != null ? leftWeapon.icon : null;

        if (rightIcon != lastRightWeaponIcon)
        {
            UpdateFrontSlot(rightFrontSlot, slotRightIcon, rightWeapon);
            lastRightWeaponIcon = rightIcon;
        }

        if (leftIcon != lastLeftWeaponIcon)
        {
            UpdateFrontSlot(leftFrontSlot, slotLeftIcon, leftWeapon);
            lastLeftWeaponIcon = leftIcon;
        }
    }

    private static InventorySlot ResolveFrontSlot(Image parentImage)
    {
        if (parentImage == null) return null;
        return parentImage.GetComponentInChildren<InventorySlot>(true);
    }

    private static void UpdateFrontSlot(InventorySlot frontSlot, Image parentImage, WeaponItem weapon)
    {
        Sprite icon = weapon != null ? weapon.icon : null;
        if (frontSlot != null)
        {
            if (icon != null) frontSlot.Setup(icon, 1);
            else frontSlot.Clear();
            return;
        }

        // Se manca il child slot, non scrivere l'icona sul parent.
        if (parentImage == null) return;
        parentImage.sprite = null;
        parentImage.enabled = false;
    }

}
