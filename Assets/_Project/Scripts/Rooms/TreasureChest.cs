using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TreasureChest : MonoBehaviour, IInteractable
{
    [Header("Loot")]
    [SerializeField] private TreasureChestLootTable lootTable;

    [Header("Interaction")]
    [SerializeField] private string prompt = "Apri cassa";
    [SerializeField] private bool consumeOnlyOnce = true;

    [Header("Quest Events")]
    [SerializeField] private string questTargetId;
    [SerializeField] private string questTargetTag = "chest";

    [Header("Visuals")]
    [SerializeField] private Animator animator;
    [SerializeField] private string openTriggerName = "Open";
    [SerializeField] private string openStateName = "Open";
    [SerializeField] private string closedStateName = "Closed";
    [SerializeField] private bool enableAnimatorOnlyWhenOpened = true;
    [SerializeField, Min(0f)] private float rewardDelaySeconds = 0.9f;
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openedVisual;

    [Header("Debug")]
    [SerializeField] private bool opened;
    [SerializeField] private bool openingInProgress;

    private PlayerInventory pendingInventory;
    private Coroutine openRoutine;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        ResetAnimatorToClosedState();
        ApplyInitialVisualState();
    }

    private void Start()
    {
        ResetAnimatorToClosedState();
        ApplyInitialVisualState();
    }

    public void Interact(GameObject player)
    {
        if ((consumeOnlyOnce && opened) || openingInProgress)
            return;

        PlayerInventory inventory = ResolveInventory(player);
        if (inventory == null)
            return;

        openingInProgress = true;
        pendingInventory = inventory;
        if (openRoutine != null)
            StopCoroutine(openRoutine);
        openRoutine = StartCoroutine(OpenSequence());
    }

    public string GetPrompt()
    {
        return opened ? string.Empty : prompt;
    }

    private void GiveLoot(PlayerInventory inventory)
    {
        if (inventory == null || lootTable == null)
            return;

        List<TreasureChestLootTable.LootResult> rewards = lootTable.RollLoot();
        for (int i = 0; i < rewards.Count; i++)
        {
            TreasureChestLootTable.LootResult reward = rewards[i];
            ApplyReward(inventory, reward);
        }
    }

    private static PlayerInventory ResolveInventory(GameObject player)
    {
        if (player == null)
            return null;

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null)
            inventory = player.GetComponentInParent<PlayerInventory>();
        return inventory;
    }

    private void ApplyReward(PlayerInventory inventory, TreasureChestLootTable.LootResult reward)
    {
        switch (reward.rewardType)
        {
            case TreasureChestLootTable.RewardType.Item:
                inventory.AddGenericItemLoot(reward.item, reward.amount);
                break;
            case TreasureChestLootTable.RewardType.Usable:
                inventory.AddUsableLoot(reward.usable, reward.amount);
                break;
            case TreasureChestLootTable.RewardType.Magic:
                inventory.AddMagicLoot(reward.magic, reward.amount);
                break;
            case TreasureChestLootTable.RewardType.Armor:
                inventory.AddArmorLoot(reward.armor, reward.amount);
                break;
            case TreasureChestLootTable.RewardType.Weapon:
                inventory.AddWeaponLoot(reward.weapon, reward.amount);
                break;
        }

        Debug.Log($"[TreasureChest] Reward -> {reward.rewardType} '{reward.label}' x{reward.amount}");
    }

    private void OpenChestVisuals()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (closedVisual != null)
            closedVisual.SetActive(false);
        if (openedVisual != null)
            openedVisual.SetActive(true);
    }

    private void FinishOpeningAndGiveLoot()
    {
        if (opened && consumeOnlyOnce)
            return;

        if (pendingInventory != null)
            GiveLoot(pendingInventory);

        opened = true;
        openingInProgress = false;
        pendingInventory = null;
        openRoutine = null;
        QuestEvents.Raise(QuestObjectiveEventType.OpenChest, ResolveQuestTargetId(), questTargetTag);
    }

    private string ResolveQuestTargetId()
    {
        return string.IsNullOrWhiteSpace(questTargetId) ? gameObject.name : questTargetId.Trim();
    }

    private void ApplyInitialVisualState()
    {
        if (closedVisual != null)
            closedVisual.SetActive(!opened);
        if (openedVisual != null)
            openedVisual.SetActive(opened);

        if (animator != null && enableAnimatorOnlyWhenOpened && !opened)
            animator.enabled = false;
    }

    private void ResetAnimatorToClosedState()
    {
        if (animator == null || opened || animator.runtimeAnimatorController == null)
            return;
        if (!animator.gameObject.activeInHierarchy)
            return;

        bool wasEnabled = animator.enabled;
        if (!animator.enabled)
            animator.enabled = true;

        animator.Rebind();
        animator.Update(0f);

        if (!string.IsNullOrWhiteSpace(closedStateName))
        {
            animator.Play(closedStateName, 0, 0f);
            animator.Update(0f);
        }

        if (enableAnimatorOnlyWhenOpened)
            animator.enabled = false;
        else if (!wasEnabled)
            animator.enabled = true;
    }

    private bool PlayOpenAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;
        if (!animator.gameObject.activeInHierarchy)
            return false;

        animator.enabled = true;
        animator.speed = 1f;
        animator.Rebind();
        animator.Update(0f);

        if (!string.IsNullOrWhiteSpace(openStateName))
        {
            int openStateHash = Animator.StringToHash(openStateName);
            if (animator.HasState(0, openStateHash))
            {
                animator.Play(openStateHash, 0, 0f);
                animator.Update(0f);
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(openTriggerName) && HasAnimatorTrigger(openTriggerName))
        {
            animator.SetTrigger(openTriggerName);
            animator.Update(0f);
            return true;
        }

        return false;
    }

    private System.Collections.IEnumerator OpenSequence()
    {
        OpenChestVisuals();

        bool animationStarted = false;
        if (animator != null)
        {
            if (enableAnimatorOnlyWhenOpened)
                animator.enabled = true;

            // Dopo il re-enable lasciamo un frame al controller per riallinearsi
            // prima di forzare lo stato Open.
            yield return null;
            animationStarted = PlayOpenAnimation();
        }

        if (animationStarted)
            yield return new WaitForSeconds(Mathf.Max(0f, rewardDelaySeconds));

        FinishOpeningAndGiveLoot();
    }

    private bool HasAnimatorTrigger(string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
                return true;
        }

        return false;
    }
}
