using UnityEngine;

public sealed class DialogueRuntimeContext
{
    public DialogueManager Manager { get; internal set; }
    public DialogueConversation Conversation { get; internal set; }
    public NPCInteractable Interactable { get; internal set; }
    public GameObject Player { get; internal set; }
    public PlayerStats PlayerStats { get; internal set; }
    public PlayerInventory PlayerInventory { get; internal set; }
    public PlayerController PlayerController { get; internal set; }
    public PlayerCombat PlayerCombat { get; internal set; }
    public CoreGenerator DungeonGenerator { get; internal set; }

    public string ConversationId => Conversation != null ? Conversation.conversationId : string.Empty;

    public static DialogueRuntimeContext Create(GameObject player, NPCInteractable interactable, DialogueManager manager)
    {
        var context = new DialogueRuntimeContext
        {
            Manager = manager,
            Interactable = interactable,
            Player = player
        };

        if (player != null)
        {
            context.PlayerStats = player.GetComponent<PlayerStats>();
            context.PlayerInventory = player.GetComponent<PlayerInventory>();
            context.PlayerController = player.GetComponent<PlayerController>();
            context.PlayerCombat = player.GetComponent<PlayerCombat>();
        }

        if (context.PlayerStats == null)
            context.PlayerStats = PlayerStats.instance;
        if (context.PlayerStats != null)
        {
            if (context.Player == null)
                context.Player = context.PlayerStats.gameObject;
            if (context.PlayerInventory == null)
                context.PlayerInventory = context.PlayerStats.GetComponent<PlayerInventory>();
            if (context.PlayerController == null)
                context.PlayerController = context.PlayerStats.GetComponent<PlayerController>();
            if (context.PlayerCombat == null)
                context.PlayerCombat = context.PlayerStats.GetComponent<PlayerCombat>();
        }

        // Risolto solo quando si apre un dialogo, mai per-frame.
        context.DungeonGenerator = Object.FindObjectOfType<CoreGenerator>();
        return context;
    }
}
