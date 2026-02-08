using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    }

    // Refresh health, stamina, and mana bar fill amounts
    void UpdateBars()
    {
        if (healthBarFill != null)
        {
            float t = playerStats.currentHealth / playerStats.maxHealth;
            healthBarFill.fillAmount = Mathf.Clamp01(t);
        }

        if (staminaBarFill != null)
        {
            float t = playerStats.currentStamina / playerStats.maxStamina;
            staminaBarFill.fillAmount = Mathf.Clamp01(t);
        }

        if (manaBarFill != null)
        {
            float t = playerStats.currentMana / playerStats.maxMana;
            manaBarFill.fillAmount = Mathf.Clamp01(t);
        }
    }

    // Update the flask counter display
    void UpdateFlasks()
    {
        if (flaskCountText != null)
        {
            flaskCountText.text = "x" + playerStats.currentFlasks.ToString();
        }
    }

    // Display the icons for equipped left and right weapons
    void UpdateWeaponSlots()
    {
        WeaponItem rightWeapon = playerInventory.GetWeaponForHand(Hand.Right);
        WeaponItem leftWeapon = playerInventory.GetWeaponForHand(Hand.Left);

        UpdateFrontSlot(rightFrontSlot, slotRightIcon, rightWeapon);
        UpdateFrontSlot(leftFrontSlot, slotLeftIcon, leftWeapon);
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
