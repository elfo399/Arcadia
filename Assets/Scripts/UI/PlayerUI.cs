using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerUI : MonoBehaviour
{
    [Header("References")]
    public PlayerStats playerStats;
    public PlayerInventory playerInventory;   // 👈 NUOVO

    [Header("Bars")]
    public Image healthBarFill;
    public Image staminaBarFill;
    public Image manaBarFill;

    [Header("Flasks")]
    public TextMeshProUGUI flaskCountText;

    [Header("Bottom Weapon Slots (D-Pad)")]
    public Image slotLeftIcon;    // mano sinistra
    public Image slotRightIcon;   // mano destra
    // (se vuoi in futuro puoi aggiungere anche slotUpIcon / slotDownIcon)

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

    void UpdateFlasks()
    {
        if (flaskCountText != null)
        {
            flaskCountText.text = "x" + playerStats.currentFlasks.ToString();
        }
    }

    // ─────────────────────────────────────────────
    // SLOT DI SOTTO: LEFT = mano sinistra, RIGHT = mano destra
    // ─────────────────────────────────────────────
    void UpdateWeaponSlots()
    {
        // MANO DESTRA → slotRightIcon
        if (slotRightIcon != null)
        {
            WeaponItem rightWeapon = playerInventory.rightHandWeapon != null
                ? playerInventory.rightHandWeapon
                : playerInventory.unarmedRight;      // pugno destro di default

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

        // MANO SINISTRA → slotLeftIcon
        if (slotLeftIcon != null)
        {
            WeaponItem leftWeapon = playerInventory.leftHandWeapon != null
                ? playerInventory.leftHandWeapon
                : playerInventory.unarmedLeft;       // pugno sinistro di default

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
