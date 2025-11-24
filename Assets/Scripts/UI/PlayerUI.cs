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
        if (slotRightIcon != null)
        {
            WeaponItem rightWeapon = playerInventory.rightHandWeapon != null
                ? playerInventory.rightHandWeapon
                : playerInventory.unarmedRight;

            if (rightWeapon != null && rightWeapon.icon != null)
            {
                slotRightIcon.enabled = true;
                slotRightIcon.sprite  = rightWeapon.icon;
            }
            else
            {
                slotRightIcon.enabled = false;
            }
        }

        if (slotLeftIcon != null)
        {
            WeaponItem leftWeapon = playerInventory.leftHandWeapon != null
                ? playerInventory.leftHandWeapon
                : playerInventory.unarmedLeft;

            if (leftWeapon != null && leftWeapon.icon != null)
            {
                slotLeftIcon.enabled = true;
                slotLeftIcon.sprite  = leftWeapon.icon;
            }
            else
            {
                slotLeftIcon.enabled = false;
            }
        }
    }
}
