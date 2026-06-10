using UnityEngine;

[RequireComponent(typeof(PlayerInventory))]
public class PlayerWeaponVisuals : MonoBehaviour
{
    [Header("Sockets")]
    [SerializeField] private Transform rightHandSocket;
    [SerializeField] private Transform leftHandSocket;
    [SerializeField] private bool autoResolveHumanoidHandBones = false;

    [Header("Runtime")]
    [SerializeField] private WeaponItem currentRightVisualWeapon;
    [SerializeField] private WeaponItem currentLeftVisualWeapon;

    private PlayerInventory inventory;
    private GameObject rightModelInstance;
    private GameObject leftModelInstance;
    private Animator cachedAnimator;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        ResolveSocketsIfNeeded();
        RefreshNow();
    }

    private void LateUpdate()
    {
        if (inventory == null) return;
        ResolveSocketsIfNeeded();

        WeaponItem right = inventory.GetWeaponForHand(Hand.Right);
        WeaponItem left = inventory.GetWeaponForHand(Hand.Left);

        if (right != currentRightVisualWeapon)
        {
            currentRightVisualWeapon = right;
            RebuildHandModel(Hand.Right, right);
        }

        if (left != currentLeftVisualWeapon)
        {
            currentLeftVisualWeapon = left;
            RebuildHandModel(Hand.Left, left);
        }
    }

    public void RefreshNow()
    {
        if (inventory == null) return;
        ResolveSocketsIfNeeded();

        currentRightVisualWeapon = inventory.GetWeaponForHand(Hand.Right);
        currentLeftVisualWeapon = inventory.GetWeaponForHand(Hand.Left);

        RebuildHandModel(Hand.Right, currentRightVisualWeapon);
        RebuildHandModel(Hand.Left, currentLeftVisualWeapon);
    }

    private void ResolveSocketsIfNeeded()
    {
        if (!autoResolveHumanoidHandBones) return;
        if (cachedAnimator == null)
            cachedAnimator = GetComponentInChildren<Animator>();
        if (cachedAnimator == null || !cachedAnimator.isHuman) return;
        if (rightHandSocket == null)
            rightHandSocket = cachedAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        if (leftHandSocket == null)
            leftHandSocket = cachedAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
    }

    private void RebuildHandModel(Hand hand, WeaponItem weapon)
    {
        Transform socket = hand == Hand.Right ? rightHandSocket : leftHandSocket;
        if (socket == null)
        {
            ClearInstance(hand);
            return;
        }

        ClearInstance(hand);
        if (weapon == null || weapon.modelPrefab == null) return;

        GameObject instance = Instantiate(weapon.modelPrefab, socket);
        instance.name = $"{weapon.name}_{hand}_Model";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        // Weapon visuals on hand should not drive collisions/physics.
        var colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) Destroy(colliders[i]);
        }
        var rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (rigidbodies[i] != null) Destroy(rigidbodies[i]);
        }

        if (hand == Hand.Right) rightModelInstance = instance;
        else leftModelInstance = instance;
    }

    private void ClearInstance(Hand hand)
    {
        if (hand == Hand.Right)
        {
            if (rightModelInstance != null) Destroy(rightModelInstance);
            rightModelInstance = null;
            return;
        }

        if (leftModelInstance != null) Destroy(leftModelInstance);
        leftModelInstance = null;
    }
}
