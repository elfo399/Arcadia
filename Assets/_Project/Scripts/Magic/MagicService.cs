using UnityEngine;

public sealed class MagicService : NpcServiceBehaviour
{
    [SerializeField] private MagicManager magicManager;
    private NpcServiceContext activeContext;

    public override bool Open(NpcServiceContext context)
    {
        if (magicManager == null || context == null || context.Player == null)
            return false;

        activeContext = context;
        magicManager.Closed -= OnMagicClosed;
        magicManager.Closed += OnMagicClosed;
        if (magicManager.OpenMagic(context)) return true;

        magicManager.Closed -= OnMagicClosed;
        activeContext = null;
        return false;
    }

    public override void Close()
    {
        if (magicManager != null) magicManager.CloseMagic();
    }

    protected override void OnDisable()
    {
        if (magicManager != null)
        {
            magicManager.Closed -= OnMagicClosed;
            if (magicManager.IsOpen) magicManager.CloseMagic();
        }
        activeContext = null;
        base.OnDisable();
    }

    private void OnMagicClosed()
    {
        if (magicManager != null) magicManager.Closed -= OnMagicClosed;
        NpcServiceContext context = activeContext;
        activeContext = null;
        if (context == null || context.DialogueManager == null || context.Interactable == null
            || context.Player == null || context.DialogueManager.IsDialogueActive)
            return;
        context.DialogueManager.TryStartDialogue(context.Interactable, context.Player);
    }
}
