using UnityEngine;
using System;
using System.Collections.Generic;

public enum DungeonOccurrencePolicy { Repeatable, OncePerRun, OncePerSave }

/// <summary>Small reusable event hook for authored NPCs, props, risk/reward and sacrifice interactions.</summary>
public sealed class DungeonNarrativeEvent : MonoBehaviour, IInteractable
{
    [Serializable]
    public sealed class Choice
    {
        public string choiceId;
        public string title;
        [TextArea] public string description;
        public DungeonRequirement[] requirements;
        public DungeonCost[] costs;
        public DungeonOutcome[] outcomes;
        [Tooltip("When false (for example Leave), this choice does not consume the event occurrence.")]
        public bool consumesEvent = true;
        [Tooltip("Optional bridge to Arcadia's existing dialogue system after this outcome succeeds.")]
        public NPCInteractable dialogueHook;
    }
    [SerializeField] private string eventId;
    [SerializeField] private DungeonOccurrencePolicy occurrence= DungeonOccurrencePolicy.OncePerRun;
    [SerializeField] private Choice[] choices=Array.Empty<Choice>();
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
        // Multi-choice events are selected through authored DungeonNarrativeChoiceAnchor children.
        if(choices!=null&&choices.Length>0)return;
        PlayerStats stats=player!=null?player.GetComponentInParent<PlayerStats>():PlayerStats.instance; if(room==null||stats==null||string.IsNullOrWhiteSpace(eventId)||(requirement!=null&&!requirement.IsMet(stats))||!CanOccur(stats))return;
        var costs=new System.Collections.Generic.List<DungeonCost>();if(coinCost>0)costs.Add(new DungeonCost{kind=DungeonCostKind.Coins,amount=coinCost});if(healthSacrifice>0)costs.Add(new DungeonCost{kind=DungeonCostKind.Health,amount=healthSacrifice});
        if(!DungeonCostTransaction.CanPay(costs,stats)||(grantModifier!=null&&(RunModifierController.Active==null||!RunModifierController.Active.CanAdd(grantModifier)))||!DungeonCostTransaction.TryPay(costs,stats,out bool lethalPayment)||lethalPayment)return;
        if(karmaDelta!=0)stats.ModifyKarma(karmaDelta,false); if(benedettoDelta!=0)stats.ModifyBenedetto(benedettoDelta,false); if(maleficoDelta!=0)stats.ModifyMalefico(maleficoDelta,false); if(!string.IsNullOrWhiteSpace(setStoryFlag))stats.SetStoryFlag(setStoryFlag,false); if(grantModifier!=null)RunModifierController.Active.Add(grantModifier);
        if(occurrence==DungeonOccurrencePolicy.OncePerRun) DungeonRunStateController.Active?.ConsumeOncePerRun(eventId); if(occurrence==DungeonOccurrencePolicy.OncePerSave)stats.SetStoryFlag(SaveFlag,false); state.completed=true;room.SaveExternalState(state);
    }
    internal bool TrySelect(string choiceId,GameObject player)
    {
        if(room==null||string.IsNullOrWhiteSpace(choiceId))return false;Choice choice=null;
        foreach(Choice candidate in choices)if(candidate!=null&&string.Equals(candidate.choiceId,choiceId,StringComparison.Ordinal)){choice=candidate;break;}
        PlayerStats stats=player!=null?player.GetComponentInParent<PlayerStats>():PlayerStats.instance;if(choice==null||stats==null||!CanOccur(stats)||!RequirementsMet(choice.requirements,stats)||!DungeonCostTransaction.CanPay(choice.costs,stats)||!TryResolve(choice,stats,out var resolved))return false;
        if(!DungeonCostTransaction.TryPay(choice.costs,stats,out bool lethalPayment)||lethalPayment)return false;
        if(!DungeonOutcomeResolution.ApplyAll(resolved,stats)){Debug.LogError($"[DungeonNarrativeEvent] Resolved outcome batch unexpectedly failed after payment on '{name}'.",this);return false;}
        if(choice.consumesEvent){if(occurrence==DungeonOccurrencePolicy.OncePerRun)DungeonRunStateController.Active?.ConsumeOncePerRun(eventId);if(occurrence==DungeonOccurrencePolicy.OncePerSave)stats.SetStoryFlag(SaveFlag,false);state.completed=true;}
        room.SaveExternalState(state);RefreshChoices();
        choice.dialogueHook?.Interact(player??stats.gameObject);return true;
    }
    internal bool IsChoiceAvailable(string choiceId,PlayerStats stats)
    {
        if(!CanOccur(stats)||choices==null)return false;foreach(Choice choice in choices)if(choice!=null&&choice.choiceId==choiceId)return RequirementsMet(choice.requirements,stats)&&DungeonCostTransaction.CanPay(choice.costs,stats)&&TryResolve(choice,stats,out _);return false;
    }
    private bool TryResolve(Choice choice,PlayerStats stats,out List<DungeonResolvedOutcome> resolved)
    =>DungeonOutcomeResolution.TryResolveAll(choice.outcomes,stats,index=>DungeonDeterminism.Create(DungeonRunStateController.Active?.RunSeed??string.Empty,room.Floor,room.RuntimeId,eventId+":"+choice.choiceId+":"+index),out resolved);
    private static bool RequirementsMet(IEnumerable<DungeonRequirement> requirements,PlayerStats stats){if(requirements==null)return true;foreach(DungeonRequirement requirement in requirements)if(requirement!=null&&!requirement.IsMet(stats))return false;return true;}
    private void RefreshChoices(){foreach(DungeonNarrativeChoiceAnchor anchor in GetComponentsInChildren<DungeonNarrativeChoiceAnchor>(true))anchor.Refresh();}
    private bool CanOccur(PlayerStats stats)
    { if(occurrence==DungeonOccurrencePolicy.Repeatable)return true; if(occurrence==DungeonOccurrencePolicy.OncePerSave)return stats!=null&&!stats.HasStoryFlag(SaveFlag); return DungeonRunStateController.Active==null ? state==null||!state.completed : !DungeonRunStateController.Active.HasConsumedOncePerRun(eventId); }
    public string GetPrompt(){if(occurrence==DungeonOccurrencePolicy.OncePerRun&&DungeonRunStateController.Active!=null&&DungeonRunStateController.Active.HasConsumedOncePerRun(eventId))return string.Empty;return state!=null&&state.completed&&occurrence!=DungeonOccurrencePolicy.Repeatable?string.Empty:prompt;}
#if UNITY_EDITOR
    private void OnValidate(){if(string.IsNullOrWhiteSpace(eventId)){eventId="event-"+System.Guid.NewGuid().ToString("N");UnityEditor.EditorUtility.SetDirty(this);}if(choices!=null)foreach(Choice choice in choices)if(choice!=null&&string.IsNullOrWhiteSpace(choice.choiceId))choice.choiceId="event-choice-"+System.Guid.NewGuid().ToString("N");}
#endif
}

/// <summary>Place one on each authored physical choice prop; it reuses DungeonNarrativeEvent's state and outcomes.</summary>
public sealed class DungeonNarrativeChoiceAnchor : MonoBehaviour,IInteractable
{
    [SerializeField] private string choiceId;
    [SerializeField] private GameObject unavailableVisual;
    private DungeonNarrativeEvent owner;private bool available;
    private void Start(){owner=GetComponentInParent<DungeonNarrativeEvent>();Refresh();}
    internal void Refresh(){available=owner!=null&&owner.IsChoiceAvailable(choiceId,PlayerStats.instance);if(unavailableVisual)unavailableVisual.SetActive(!available);}
    public void Interact(GameObject player){Refresh();if(available&&owner.TrySelect(choiceId,player))Refresh();}
    public string GetPrompt()=>available?"Choose":string.Empty;
#if UNITY_EDITOR
    private void OnValidate(){if(string.IsNullOrWhiteSpace(choiceId)){choiceId="event-choice-"+Guid.NewGuid().ToString("N");UnityEditor.EditorUtility.SetDirty(this);}}
#endif
}
