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
    private Room room; private SavedDungeonRuleState state;

    private void Start()
    {
        room=GetComponentInParent<Room>();if(room==null)return;state=room.GetExternalState("encounter-trigger:"+interactionId);Refresh();
    }
    public void Interact(GameObject player)
    {
        PlayerStats stats=player!=null?player.GetComponentInParent<PlayerStats>():PlayerStats.instance;if(room==null||stats==null||(oneShot&&state.completed))return;
        if(requirements!=null)foreach(DungeonRequirement requirement in requirements)if(requirement!=null&&!requirement.IsMet(stats))return;
        RoomRule rule=room.GetRule(targetRuleId);ITriggeredRoomEncounter encounter=rule as ITriggeredRoomEncounter;
        if(encounter==null||!encounter.CanStartFromTrigger()){Debug.LogError($"[EncounterTriggerInteraction] '{name}' target '{targetRuleId}' is missing, unavailable, or is not triggerable.",this);return;}
        if(!DungeonCostTransaction.CanPay(costs,stats)||!DungeonCostTransaction.TryPay(costs,stats,out bool lethalPayment)||lethalPayment)return;
        if(!encounter.TryStartFromTrigger())return;
        state.completed=true;room.SaveExternalState(state);Refresh();
    }
    public string GetPrompt()=>state!=null&&state.completed&&oneShot?string.Empty:prompt;
    private void Refresh(){bool used=state!=null&&state.completed;if(availableVisual)availableVisual.SetActive(!used);if(usedVisual)usedVisual.SetActive(used);}
#if UNITY_EDITOR
    private void OnValidate(){if(string.IsNullOrWhiteSpace(interactionId)){interactionId="encounter-trigger-"+Guid.NewGuid().ToString("N");UnityEditor.EditorUtility.SetDirty(this);}}
#endif
}
