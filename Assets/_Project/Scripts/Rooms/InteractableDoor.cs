using System;
using UnityEngine;

/// <summary>Authored local door with persistent opened state.</summary>
public class InteractableDoor : MonoBehaviour,IInteractable
{
    [SerializeField] private string doorId;
    [SerializeField] private DungeonRequirement[] requirements=Array.Empty<DungeonRequirement>();
    [SerializeField] private DungeonCost[] costs=Array.Empty<DungeonCost>();
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openedVisual;
    [SerializeField] private Collider blockingCollider;
    [SerializeField] private string prompt="Open";
    private const string DungeonKeyDefinitionId="dungeon-key";
    private Room parentRoom; private SavedDungeonRuleState state;
    private Renderer[] fallbackClosedRenderers=Array.Empty<Renderer>();
    private bool socketControlled; private bool socketConnected=true; private bool encounterBlocked; private bool opened;
    private bool IsOpened=>opened||(state!=null&&state.completed);
    public bool IsPassable => (!socketControlled || socketConnected) && IsOpened && !encounterBlocked;

    private void Awake()
    {
        if(blockingCollider==null)blockingCollider=GetComponent<Collider>();
        fallbackClosedRenderers=GetComponentsInChildren<Renderer>(true);
    }

    private void Start()
    {
        parentRoom=GetComponentInParent<Room>();
        if(parentRoom!=null)
        {
            state=parentRoom.GetExternalState("door:"+GetEffectiveDoorId());
            opened=state.completed;
        }
        ApplyPhysicalState();
    }

    /// <summary>Called only by the graph socket this authored gate belongs to.</summary>
    public void SetSocketConnected(bool connected)=>SetSocketConnection(null,connected);
    public void SetSocketConnection(string socketDoorId,bool connected)
    {
        if(!string.IsNullOrWhiteSpace(socketDoorId))doorId=socketDoorId;
        socketControlled=true;socketConnected=connected;
        if(gameObject.activeSelf!=connected)gameObject.SetActive(connected);
        if(connected)ApplyPhysicalState();
    }

    /// <summary>Temporary encounter locks compose with, but never overwrite, the persistent gate state.</summary>
    public void SetEncounterBlocked(bool blocked){encounterBlocked=blocked;ApplyPhysicalState();}
    public void Interact(GameObject player)
    {
        if(IsOpened)return;
        PlayerStats stats=player!=null?player.GetComponentInParent<PlayerStats>():PlayerStats.instance;
        DungeonCost[] payableCosts=ResolveCosts(stats);
        if(stats==null||payableCosts==null||!RequirementsMet(stats)||!DungeonCostTransaction.CanPay(payableCosts,stats))return;
        if(!DungeonCostTransaction.TryPay(payableCosts,stats,out DungeonCostTransaction.DungeonCostPayment payment,out bool lethalPayment)||lethalPayment)return;

        opened=true;
        if(state!=null)state.completed=true;
        ApplyPhysicalState();
        payment?.Commit();
        if(parentRoom!=null)parentRoom.SaveExternalState(state);
    }

    public string GetPrompt()
    {
        if(IsOpened)return string.Empty;
        PlayerStats stats=PlayerStats.instance;
        DungeonCost[] payableCosts=ResolveCosts(stats);
        if(stats==null||payableCosts==null||!RequirementsMet(stats)||!DungeonCostTransaction.CanPay(payableCosts,stats))return HasKeyCost()?"Requires Key":"Requires requirements";
        return prompt;
    }

    private bool RequirementsMet(PlayerStats stats)
    {
        if(requirements==null)return true;
        foreach(var requirement in requirements)if(requirement!=null&&!requirement.IsMet(stats))return false;
        return true;
    }

    private bool HasKeyCost()
    {
        if(socketControlled&&(costs==null||costs.Length==0))return true;
        if(costs==null)return false;
        foreach(var cost in costs)if(cost!=null&&cost.kind==DungeonCostKind.InventoryItem&&cost.item!=null&&cost.item.itemName.IndexOf("key",StringComparison.OrdinalIgnoreCase)>=0)return true;
        return false;
    }

    private DungeonCost[] ResolveCosts(PlayerStats stats)
    {
        if(costs!=null&&costs.Length>0)return costs;
        // Existing authored LockDoor objects become socket gates through Room. Their
        // empty legacy serialization is interpreted as the project's one modern key cost;
        // ordinary local InteractableDoor components remain free unless explicitly costed.
        if(!socketControlled||stats==null)return costs??Array.Empty<DungeonCost>();
        PlayerInventory inventory=stats.GetComponent<PlayerInventory>();
        if(inventory==null||inventory.ItemDatabase==null||!inventory.ItemDatabase.TryGetItem(DungeonKeyDefinitionId,out ItemData key))return null;
        return new[]{new DungeonCost{kind=DungeonCostKind.InventoryItem,item=key,amount=1}};
    }

    private void ApplyPhysicalState()
    {
        if(socketControlled&&!socketConnected)return;
        bool closed=!IsOpened||encounterBlocked;
        if(blockingCollider!=null)blockingCollider.enabled=closed;
        if(closedVisual!=null)closedVisual.SetActive(closed);
        else foreach(var renderer in fallbackClosedRenderers)if(renderer!=null)renderer.enabled=closed;
        if(openedVisual!=null)openedVisual.SetActive(!closed&&IsOpened);
    }

    public void ConfigureAsKeyGate(string stableDoorId,ItemData keyItem,Collider collider)
    {
        doorId=stableDoorId;
        requirements=Array.Empty<DungeonRequirement>();
        costs=new[]{new DungeonCost{kind=DungeonCostKind.InventoryItem,item=keyItem,amount=1}};
        blockingCollider=collider;
        closedVisual=null;
        openedVisual=null;
        prompt="Open";
    }

    private string GetEffectiveDoorId()=>string.IsNullOrWhiteSpace(doorId)?"local-"+GetInstanceID().ToString():doorId;
#if UNITY_EDITOR
    private void OnValidate(){if(string.IsNullOrWhiteSpace(doorId)){doorId="door-"+System.Guid.NewGuid().ToString("N");UnityEditor.EditorUtility.SetDirty(this);}}
#endif
}
