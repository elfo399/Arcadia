using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Configurable deterministic reward offers. Put a collider on the authored room/pedestal.</summary>
public sealed class RoomRewardRule : RoomRule, IInteractable
{
    [SerializeField] private LootPoolDefinition lootPool;
    [SerializeField, Min(1)] private int generatedChoices = 1;
    [SerializeField, Min(1)] private int maxClaims = 1;
    [SerializeField] private bool completeAfterClaims;
    [SerializeField] private string prompt = "Claim reward";
    private LootPoolDefinition.Entry[] offers;
    private readonly HashSet<int> claimed = new HashSet<int>();
    protected override void OnRoomInitialized() { BuildOffers(); if(completeAfterClaims && claimed.Count >= maxClaims) Complete(); }
    protected override void OnStateRestored(string payload)
    { if(!string.IsNullOrEmpty(payload)) foreach(string token in payload.Split(',')) if(int.TryParse(token,out int index)) claimed.Add(index); if(completeAfterClaims && claimed.Count>=maxClaims) Complete(); }
    protected override string CaptureState() { var values=new List<int>(claimed); values.Sort(); return string.Join(",",values); }
    private void BuildOffers()
    { if(lootPool==null) return; offers=new LootPoolDefinition.Entry[Mathf.Max(1,generatedChoices)]; var random=Context.CreateRandom(RuleId+":offers"); for(int i=0;i<offers.Length;i++) offers[i]=lootPool.Pick(random); }
    public void Interact(GameObject player)
    {
        if(offers==null || claimed.Count>=maxClaims) return; PlayerInventory inventory=player!=null?player.GetComponentInParent<PlayerInventory>():null; if(inventory==null)return;
        for(int i=0;i<offers.Length && claimed.Count<maxClaims;i++) { var offer=offers[i]; if(offer==null||offer.item==null||claimed.Contains(i)||!inventory.TryAddItem(offer.item,offer.amount)) continue; claimed.Add(i); Context.State.rewardClaimed=true; break; }
        if(completeAfterClaims && claimed.Count>=maxClaims) Complete(); Context.Room.NotifyRuleChanged(this);
    }
    public string GetPrompt() => claimed.Count >= maxClaims ? string.Empty : prompt;
    private void OnValidate() { if(maxClaims>generatedChoices) maxClaims=generatedChoices; if(lootPool==null) Debug.LogWarning($"[RoomRewardRule] {name} has no LootPool.",this); }
}
