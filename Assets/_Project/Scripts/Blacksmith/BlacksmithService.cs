using UnityEngine;

public sealed class BlacksmithService : NpcServiceBehaviour
{
    [SerializeField] private BlacksmithManager blacksmithManager;
    [SerializeField] private BlacksmithMode openMode = BlacksmithMode.Upgrade;
    private NpcServiceContext activeContext;

    public override bool Open(NpcServiceContext context)
    {
        if (blacksmithManager == null || context == null || context.Player == null)
            return false;

        activeContext = context;
        blacksmithManager.Closed -= OnBlacksmithClosed;
        blacksmithManager.Closed += OnBlacksmithClosed;
        if (blacksmithManager.OpenBlacksmith(openMode, context))
            return true;

        blacksmithManager.Closed -= OnBlacksmithClosed;
        activeContext = null;
        return false;
    }

    public override void Close()
    {
        if (blacksmithManager != null)
            blacksmithManager.CloseBlacksmith();
    }

    protected override void OnDisable()
    {
        if (blacksmithManager != null)
        {
            blacksmithManager.Closed -= OnBlacksmithClosed;
            if (blacksmithManager.IsOpen)
                blacksmithManager.CloseBlacksmith();
        }
        activeContext = null;
        base.OnDisable();
    }

    private void OnBlacksmithClosed()
    {
        blacksmithManager.Closed -= OnBlacksmithClosed;
        NpcServiceContext context = activeContext;
        activeContext = null;
        if (context == null || context.DialogueManager == null || context.Interactable == null
            || context.Player == null || context.DialogueManager.IsDialogueActive)
            return;

        context.DialogueManager.TryStartDialogue(context.Interactable, context.Player);
    }
}
