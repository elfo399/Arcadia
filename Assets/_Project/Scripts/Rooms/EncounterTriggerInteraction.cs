using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authored prop bridge for optional combat and ambush/mimic compositions. It
/// starts an existing encounter rule; combat ownership and reward gating remain
/// entirely in that rule and RoomRewardRule.
/// </summary>
public sealed class EncounterTriggerInteraction : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionId;
    [SerializeField] private string targetRuleId;
    [SerializeField] private DungeonRequirement[] requirements;
    [SerializeField] private DungeonCost[] costs;
    [SerializeField] private bool oneShot = true;
    [SerializeField] private GameObject availableVisual;
    [SerializeField] private GameObject usedVisual;
    [SerializeField] private string prompt = "Investigate";
    private Room room; private SavedDungeonRuleState state; private ITriggeredRoomEncounter target;

    private void Start()
    {
        room=GetComponentInParent<Room>();if(room==null)return;state=room.GetExternalState("encounter-trigger:"+interactionId);ResolveTarget();Refresh();
    }
    public void Interact(GameObject player)
    {
        PlayerStats stats=player!=null?player.GetComponentInParent<PlayerStats>():PlayerStats.instance;if(room==null||stats==null||(oneShot&&state.completed))return;
        if(requirements!=null)foreach(DungeonRequirement requirement in requirements)if(requirement!=null&&!requirement.IsMet(stats))return;
        ITriggeredRoomEncounter encounter=ResolveTarget();
        if(encounter==null||!encounter.CanStartFromTrigger()){Debug.LogError($"[EncounterTriggerInteraction] '{name}' target '{targetRuleId}' is missing, unavailable, or is not triggerable.",this);return;}
        if(!DungeonCostTransaction.CanPay(costs,stats)||!DungeonCostTransaction.TryPay(costs,stats,out bool lethalPayment)||lethalPayment)return;
        if(!encounter.TryStartFromTrigger())return;
        state.completed=true;room.SaveExternalState(state);Refresh();
    }
    public string GetPrompt()
    {
        if(state!=null&&state.completed&&oneShot)return string.Empty;PlayerStats stats=PlayerStats.instance;
        if(room==null||stats==null||ResolveTarget()==null||!target.CanStartFromTrigger())return string.Empty;
        if(requirements!=null)foreach(DungeonRequirement requirement in requirements)if(requirement!=null&&!requirement.IsMet(stats))return string.Empty;
        return DungeonCostTransaction.CanPay(costs,stats)?prompt:string.Empty;
    }
    private void Refresh(){bool used=state!=null&&state.completed;if(availableVisual)availableVisual.SetActive(!used);if(usedVisual)usedVisual.SetActive(used);}
    private ITriggeredRoomEncounter ResolveTarget()
    {
        if(target!=null)return target;if(room==null||string.IsNullOrWhiteSpace(targetRuleId))return null;target=room.GetRule(targetRuleId) as ITriggeredRoomEncounter;return target;
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        if(string.IsNullOrWhiteSpace(interactionId)){interactionId="encounter-trigger-"+Guid.NewGuid().ToString("N");UnityEditor.EditorUtility.SetDirty(this);}
        if(string.IsNullOrWhiteSpace(targetRuleId))Debug.LogWarning($"[EncounterTriggerInteraction] '{name}' needs a target RoomRule ID.",this);
    }
#endif
}
