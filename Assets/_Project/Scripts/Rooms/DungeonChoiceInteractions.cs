using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Authored parent plus physical child choices; no separate UI system is introduced.</summary>
public sealed class ShrineEncounter : MonoBehaviour
{
    [SerializeField] private string shrineId; [SerializeField] private string family="Faith"; [SerializeField,Min(1)] private int maxSelections=1;
    private Room room; private SavedDungeonRuleState state; private readonly HashSet<string> selected=new HashSet<string>();private bool available=true;
    private void Start(){room=GetComponentInParent<Room>();if(room==null)return;available=CoreGenerator.Instance==null||CoreGenerator.Instance.ActiveFloorDefinition==null||CoreGenerator.Instance.ActiveFloorDefinition.shrinesAvailable;state=room.GetExternalState("shrine:"+shrineId);if(!string.IsNullOrEmpty(state.payload))foreach(string id in state.payload.Split(','))selected.Add(id);Refresh();}
    internal bool Select(ShrineChoice choice,GameObject player)
    {
        if(!available||room==null||choice==null||selected.Count>=maxSelections||selected.Contains(choice.ChoiceId))return false;PlayerStats stats=player!=null?player.GetComponentInParent<PlayerStats>():PlayerStats.instance;
        if(stats==null||!choice.AreRequirementsMet(stats)||!DungeonCostTransaction.CanPay(choice.Costs,stats)||!choice.TryResolveOutcomes(stats,room,out var resolved))return false;
        if(!DungeonCostTransaction.TryPay(choice.Costs,stats))return false;
        if(!DungeonOutcomeResolution.ApplyAll(resolved,stats))return false;selected.Add(choice.ChoiceId);state.payload=string.Join(",",selected);state.completed=selected.Count>=maxSelections;room.SaveExternalState(state);Refresh();return true;
    }
    private void Refresh(){foreach(var choice in GetComponentsInChildren<ShrineChoice>(true))choice.SetAvailable(available&&selected.Count<maxSelections&&!selected.Contains(choice.ChoiceId));}
#if UNITY_EDITOR
    private void OnValidate(){if(string.IsNullOrWhiteSpace(shrineId)){shrineId="shrine-"+Guid.NewGuid().ToString("N");UnityEditor.EditorUtility.SetDirty(this);}}
#endif
}
public sealed class ShrineChoice:MonoBehaviour,IInteractable
{
    [SerializeField] private string choiceId; [SerializeField] private string title; [SerializeField,TextArea] private string description; [SerializeField] private DungeonRequirement[] requirements; [SerializeField] private DungeonCost[] costs; [SerializeField] private DungeonOutcome[] outcomes; [SerializeField] private GameObject unavailableVisual; private ShrineEncounter shrine; private bool available;
    public string ChoiceId=>choiceId; public IReadOnlyList<DungeonCost> Costs=>costs; public IReadOnlyList<DungeonOutcome> Outcomes=>outcomes;
    private void Start(){shrine=GetComponentInParent<ShrineEncounter>();}
    internal bool AreRequirementsMet(PlayerStats stats){if(requirements==null)return true;foreach(var requirement in requirements)if(requirement!=null&&!requirement.IsMet(stats))return false;return true;}
    internal bool TryResolveOutcomes(PlayerStats stats,Room room,out List<DungeonResolvedOutcome> resolved)
    =>DungeonOutcomeResolution.TryResolveAll(outcomes,stats,index=>DungeonDeterminism.Create(DungeonRunStateController.Active?.RunSeed??string.Empty,room.Floor,room.RuntimeId,"shrine:"+choiceId+":"+index),out resolved);
    internal void SetAvailable(bool value){available=value;if(unavailableVisual)unavailableVisual.SetActive(!value);}
    public void Interact(GameObject player){if(available&&shrine!=null)shrine.Select(this,player);}
    public string GetPrompt()=>available?(string.IsNullOrWhiteSpace(title)?"Choose":title):string.Empty;
#if UNITY_EDITOR
    private void OnValidate(){if(string.IsNullOrWhiteSpace(choiceId)){choiceId="shrine-choice-"+Guid.NewGuid().ToString("N");UnityEditor.EditorUtility.SetDirty(this);}}
#endif
}
public sealed class RiskRewardInteraction:MonoBehaviour,IInteractable
{
    [SerializeField] private string interactionId; [SerializeField] private DungeonRequirement[] requirements; [SerializeField] private DungeonCost[] costs; [SerializeField] private DungeonOutcome[] outcomes; [SerializeField] private bool oneShot=true; private Room room;private SavedDungeonRuleState state;
    private void Start(){room=GetComponentInParent<Room>();if(room!=null)state=room.GetExternalState("risk:"+interactionId);}
    public void Interact(GameObject player)
    {
        PlayerStats stats=player!=null?player.GetComponentInParent<PlayerStats>():PlayerStats.instance;if(room==null||stats==null||(oneShot&&state.completed))return;
        if(requirements!=null)foreach(var r in requirements)if(r!=null&&!r.IsMet(stats))return;
        if(!DungeonCostTransaction.CanPay(costs,stats)||!TryResolveOutcomes(stats,out var resolved))return;if(!DungeonCostTransaction.TryPay(costs,stats))return;
        if(!DungeonOutcomeResolution.ApplyAll(resolved,stats))return;state.completed=true;room.SaveExternalState(state);
    }
    private bool TryResolveOutcomes(PlayerStats stats,out List<DungeonResolvedOutcome> resolved)
    =>DungeonOutcomeResolution.TryResolveAll(outcomes,stats,index=>DungeonDeterminism.Create(DungeonRunStateController.Active?.RunSeed??string.Empty,room.Floor,room.RuntimeId,interactionId+":"+index),out resolved);
    public string GetPrompt()=>state!=null&&state.completed&&oneShot?string.Empty:"Interact";
#if UNITY_EDITOR
    private void OnValidate(){if(string.IsNullOrWhiteSpace(interactionId)){interactionId="risk-"+Guid.NewGuid().ToString("N");UnityEditor.EditorUtility.SetDirty(this);}}
#endif
}
