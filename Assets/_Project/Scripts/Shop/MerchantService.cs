using UnityEngine;

public sealed class MerchantService : NpcServiceBehaviour
{
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private ShopMode openMode = ShopMode.Buy;

    private NpcServiceContext activeContext;

    public override bool Open(NpcServiceContext context)
    {
        if (shopManager == null || context == null || context.Player == null)
            return false;

        NpcProfile npcProfile = context.Interactable != null ? context.Interactable.NpcProfile : null;
        MerchantData merchantData = npcProfile != null ? npcProfile.merchantData : null;
        if (merchantData == null)
        {
            Debug.LogWarning("[MerchantService] MerchantData mancante nel NpcProfile dell'NPC.", this);
            return false;
        }

        activeContext = context;
        shopManager.Closed -= OnShopClosed;
        shopManager.Closed += OnShopClosed;

        if (shopManager.OpenShop(openMode, merchantData))
            return true;

        shopManager.Closed -= OnShopClosed;
        activeContext = null;
        return false;
    }

    public override void Close()
    {
        if (shopManager != null)
            shopManager.CloseShop();
    }

    protected override void OnDisable()
    {
        if (shopManager != null)
        {
            shopManager.Closed -= OnShopClosed;
            if (shopManager.IsOpen)
                shopManager.CloseShop();
        }

        activeContext = null;
        base.OnDisable();
    }

    private void OnShopClosed()
    {
        shopManager.Closed -= OnShopClosed;

        NpcServiceContext context = activeContext;
        activeContext = null;
        if (context == null
            || context.DialogueManager == null
            || context.Interactable == null
            || context.Player == null
            || context.DialogueManager.IsDialogueActive)
        {
            return;
        }

        context.DialogueManager.TryStartDialogue(context.Interactable, context.Player);
    }
}
