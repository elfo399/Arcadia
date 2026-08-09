using UnityEngine;

public sealed class MerchantService : NpcServiceBehaviour
{
    [SerializeField] private ShopManager shopManager;

    private NpcServiceContext activeContext;

    public override bool Open(NpcServiceContext context)
    {
        if (shopManager == null || context == null || context.Player == null)
            return false;

        PlayerController controller = context.Player.GetComponent<PlayerController>();
        if (controller == null)
            controller = context.Player.GetComponentInParent<PlayerController>();
        if (controller == null)
            return false;

        activeContext = context;
        shopManager.Closed -= OnShopClosed;
        shopManager.Closed += OnShopClosed;

        if (shopManager.OpenShop(controller, ShopMode.Buy))
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
