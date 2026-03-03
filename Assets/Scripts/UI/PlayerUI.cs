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

    [Header("Bar Sizing")]
    [SerializeField] private float healthBaseWidth = 140f;
    [SerializeField] private float staminaBaseWidth = 140f;
    [SerializeField] private float manaBaseWidth = 120f;
    [SerializeField] private float healthWidthPerPoint = 0.9f;
    [SerializeField] private float staminaWidthPerPoint = 0.7f;
    [SerializeField] private float manaWidthPerPoint = 0.9f;
    [SerializeField] private float minBarWidth = 100f;
    [SerializeField] private float maxBarWidth = 320f;
    [SerializeField] private float fillHorizontalPadding = 4f;

    [Header("Flasks")]
    // Text element showing current flask count
    public TextMeshProUGUI flaskCountText;

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
        playerStats = PlayerStats.instance != null ? PlayerStats.instance : FindObjectOfType<PlayerStats>(true);
        playerInventory = FindObjectOfType<PlayerInventory>(true);
        if (healthBarFrame == null && healthBarFill != null) healthBarFrame = healthBarFill.transform.parent as RectTransform;
        if (staminaBarFrame == null && staminaBarFill != null) staminaBarFrame = staminaBarFill.transform.parent as RectTransform;
        if (manaBarFrame == null && manaBarFill != null) manaBarFrame = manaBarFill.transform.parent as RectTransform;
    }

    // Refresh health, stamina, and mana bar fill amounts
    void UpdateBars()
    {
        if (!Mathf.Approximately(playerStats.maxHealth, lastHealthMax))
        {
            ResizeBar(healthBarFrame, healthBaseWidth, healthWidthPerPoint, playerStats.maxHealth);
            SyncFillToFrame(healthBarFill, healthBarFrame);
            lastHealthMax = playerStats.maxHealth;
        }

        if (healthBarFill != null)
        {
            float t = playerStats.currentHealth / playerStats.maxHealth;
            float v = Mathf.Clamp01(t);
            if (!Mathf.Approximately(v, lastHealthFill))
            {
                healthBarFill.fillAmount = v;
                lastHealthFill = v;
            }
        }

        if (!Mathf.Approximately(playerStats.maxStamina, lastStaminaMax))
        {
            ResizeBar(staminaBarFrame, staminaBaseWidth, staminaWidthPerPoint, playerStats.maxStamina);
            SyncFillToFrame(staminaBarFill, staminaBarFrame);
            lastStaminaMax = playerStats.maxStamina;
        }

        if (staminaBarFill != null)
        {
            float t = playerStats.currentStamina / playerStats.maxStamina;
            float v = Mathf.Clamp01(t);
            if (!Mathf.Approximately(v, lastStaminaFill))
            {
                staminaBarFill.fillAmount = v;
                lastStaminaFill = v;
            }
        }

        if (!Mathf.Approximately(playerStats.maxMana, lastManaMax))
        {
            ResizeBar(manaBarFrame, manaBaseWidth, manaWidthPerPoint, playerStats.maxMana);
            SyncFillToFrame(manaBarFill, manaBarFrame);
            lastManaMax = playerStats.maxMana;
        }

        if (manaBarFill != null)
        {
            float t = playerStats.currentMana / playerStats.maxMana;
            float v = Mathf.Clamp01(t);
            if (!Mathf.Approximately(v, lastManaFill))
            {
                manaBarFill.fillAmount = v;
                lastManaFill = v;
            }
        }
    }

    private void ResizeBar(RectTransform frame, float baseWidth, float widthPerPoint, float maxValue)
    {
        if (frame == null)
            return;

        float width = baseWidth + maxValue * widthPerPoint;
        width = Mathf.Clamp(width, minBarWidth, maxBarWidth);
        frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    private void SyncFillToFrame(Image fill, RectTransform frame)
    {
        if (fill == null || frame == null)
            return;

        RectTransform fillRect = fill.rectTransform;
        float targetWidth = Mathf.Max(0f, frame.rect.width - fillHorizontalPadding * 2f);
        fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        fillRect.anchoredPosition = new Vector2(fillHorizontalPadding, fillRect.anchoredPosition.y);
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
