using UnityEngine;

public enum DungeonOccurrencePolicy { Repeatable, OncePerRun, OncePerSave }

/// <summary>Small reusable event hook for authored NPCs, props, risk/reward and sacrifice interactions.</summary>
public sealed class DungeonNarrativeEvent : MonoBehaviour, IInteractable
{
    [SerializeField] private string eventId="event";
    [SerializeField] private DungeonOccurrencePolicy occurrence= DungeonOccurrencePolicy.OncePerRun;
    [SerializeField] private DungeonRequirement requirement;
    [SerializeField] private int coinCost;
    [SerializeField] private int healthSacrifice;
    [SerializeField] private int karmaDelta;
    [SerializeField] private int benedettoDelta;
    [SerializeField] private int maleficoDelta;
    [SerializeField] private string setStoryFlag;
    [SerializeField] private RunModifierDefinition grantModifier;
    [SerializeField] private string prompt="Interact";
    private Room room; private SavedDungeonRuleState state;
    private string SaveFlag => "dungeon.event." + eventId;
    private void Start(){room=GetComponentInParent<Room>();if(room!=null)state=room.GetExternalState("event:"+eventId);}
    public void Interact(GameObject player)
    {
        PlayerStats stats=player!=null?player.GetComponentInParent<PlayerStats>():PlayerStats.instance; if(room==null||stats==null||string.IsNullOrWhiteSpace(eventId)||(requirement!=null&&!requirement.IsMet(stats))||!CanOccur(stats)||coinCost>0&&!stats.HasCoins(coinCost))return;
        if(coinCost>0)stats.TryRemoveCoins(coinCost,false); if(healthSacrifice>0)stats.TakeDamage(healthSacrifice); if(karmaDelta!=0)stats.ModifyKarma(karmaDelta,false); if(benedettoDelta!=0)stats.ModifyBenedetto(benedettoDelta,false); if(maleficoDelta!=0)stats.ModifyMalefico(maleficoDelta,false); if(!string.IsNullOrWhiteSpace(setStoryFlag))stats.SetStoryFlag(setStoryFlag,false); if(grantModifier!=null)RunModifierController.Active?.Add(grantModifier);
        if(occurrence==DungeonOccurrencePolicy.OncePerRun) DungeonRunStateController.Active?.ConsumeOncePerRun(eventId); if(occurrence==DungeonOccurrencePolicy.OncePerSave)stats.SetStoryFlag(SaveFlag,false); state.completed=true;room.SaveExternalState(state);
    }
    private bool CanOccur(PlayerStats stats)
    { if(occurrence==DungeonOccurrencePolicy.Repeatable)return true; if(occurrence==DungeonOccurrencePolicy.OncePerSave)return !stats.HasStoryFlag(SaveFlag); return state==null||!state.completed; }
    public string GetPrompt()=>state!=null&&state.completed&&occurrence!=DungeonOccurrencePolicy.Repeatable?string.Empty:prompt;
}
