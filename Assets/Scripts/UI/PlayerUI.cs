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

    [Header("Counters")]
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
    private int lastKeyCount = int.MinValue;

    // Update UI elements each frame
    void Update()
    {
        if (playerStats != null)
        {
            UpdateCounters();
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
    }

    private void UpdateCounters()
    {
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
