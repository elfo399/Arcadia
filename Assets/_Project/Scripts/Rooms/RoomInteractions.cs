using System;
using UnityEngine;

public enum DungeonRequirementKind { None, Coins, Karma, Benedetto, Malefico, StoryFlag }
[Serializable] public sealed class DungeonRequirement
{
    public DungeonRequirementKind kind; public int amount; public string id;
    public bool IsMet(PlayerStats stats)
    { if(kind==DungeonRequirementKind.None)return true; if(stats==null)return false; switch(kind) { case DungeonRequirementKind.Coins:return stats.HasCoins(amount); case DungeonRequirementKind.Karma:return stats.karma>=amount; case DungeonRequirementKind.Benedetto:return stats.benedetto>=amount; case DungeonRequirementKind.Malefico:return stats.malefico>=amount; case DungeonRequirementKind.StoryFlag:return stats.HasStoryFlag(id); default:return false; } }
}

/// <summary>Author a secret or internal area inside a physical room; it never creates a dungeon graph cell.</summary>
public sealed class DungeonSecretAccess : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionId="secret"; [SerializeField] private GameObject openedArea; [SerializeField] private DungeonRequirement requirement; [SerializeField] private bool oneShot=true; [SerializeField] private string prompt="Open secret";
    private Room room; private SavedDungeonRuleState state;
    private void Start(){room=GetComponentInParent<Room>(); if(room==null)return; state=room.GetExternalState("interaction:"+interactionId); if(state.completed&&openedArea)openedArea.SetActive(true);}
    public void Interact(GameObject player){if(room==null)return; var stats=player!=null?player.GetComponentInParent<PlayerStats>():PlayerStats.instance; if(oneShot&&state.completed || requirement!=null&&!requirement.IsMet(stats))return; if(openedArea)openedArea.SetActive(true); state.completed=true; room.SaveExternalState(state);}
    public string GetPrompt()=>state!=null&&state.completed&&oneShot?string.Empty:prompt;
}

public sealed class ShrineInteraction : MonoBehaviour, IInteractable
{
    [SerializeField] private string shrineId="shrine"; [SerializeField] private string family="Faith"; [SerializeField] private RunModifierDefinition modifier; [SerializeField] private DungeonRequirement requirement; [SerializeField] private int coinCost; [SerializeField] private bool oneShot=true; [SerializeField] private string prompt="Pray";
    private Room room; private SavedDungeonRuleState state;
    private void Start(){room=GetComponentInParent<Room>();if(room!=null)state=room.GetExternalState("shrine:"+shrineId);}
    public void Interact(GameObject player){var stats=player!=null?player.GetComponentInParent<PlayerStats>():PlayerStats.instance;if(room==null||stats==null||(oneShot&&state.completed)||(requirement!=null&&!requirement.IsMet(stats))||(coinCost>0&&!stats.HasCoins(coinCost))||RunModifierController.Active==null||!RunModifierController.Active.Add(modifier))return;if(coinCost>0)stats.TryRemoveCoins(coinCost,false);state.completed=true;room.SaveExternalState(state);}
    public string GetPrompt()=>state!=null&&state.completed&&oneShot?string.Empty:prompt;
    private void OnValidate(){if(string.IsNullOrWhiteSpace(family))Debug.LogWarning("[Shrine] A family is required.",this);}
}
