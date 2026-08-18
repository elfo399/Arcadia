using System;
using System.Collections.Generic;
using UnityEngine;

public enum DungeonCostKind { Coins, Health, Flasks, InventoryItem }
[Serializable] public sealed class DungeonCost
{
    public DungeonCostKind kind; [Min(1)] public int amount=1; public ItemData item; public bool allowLethal;
    public bool CanPay(PlayerStats stats)
    {
        if(stats==null)return false;switch(kind){case DungeonCostKind.Coins:return stats.HasCoins(amount);case DungeonCostKind.Flasks:return stats.HasFlasks(amount);case DungeonCostKind.Health:return allowLethal?stats.currentHealth>0f:stats.currentHealth>amount;case DungeonCostKind.InventoryItem:{var inventory=stats.GetComponent<PlayerInventory>();return item!=null&&inventory!=null&&inventory.GetTotalItemAmount(item)>=amount;}default:return false;}
    }
}
public static class DungeonCostTransaction
{
    public static bool TryPay(IReadOnlyList<DungeonCost> costs,PlayerStats stats)
    {
        if(costs==null)return true;for(int i=0;i<costs.Count;i++)if(costs[i]!=null&&!costs[i].CanPay(stats))return false;
        // Each authority has been validated first; non-failable operations are then consumed.
        for(int i=0;i<costs.Count;i++){DungeonCost cost=costs[i];if(cost==null)continue;switch(cost.kind){case DungeonCostKind.Coins:stats.TryRemoveCoins(cost.amount,false);break;case DungeonCostKind.Flasks:stats.TryConsumeFlasks(cost.amount,false);break;case DungeonCostKind.Health:stats.TakeDamage(cost.amount);break;case DungeonCostKind.InventoryItem:stats.GetComponent<PlayerInventory>().TryRemoveItem(cost.item,cost.amount,out _,false);break;}}
        return true;
    }
}
public enum DungeonOutcomeKind { RunModifier, Item, LootPool, Karma, Benedetto, Malefico, StoryFlag, Heal, RestoreFlasks, MagicRecipe }
[Serializable] public sealed class DungeonOutcome
{
    public DungeonOutcomeKind kind; public RunModifierDefinition modifier; public ItemData item; public LootPoolDefinition lootPool; public string id; public int amount=1;
    public void Apply(PlayerStats stats,System.Random random)
    {
        if(stats==null)return;switch(kind){case DungeonOutcomeKind.RunModifier:RunModifierController.Active?.Add(modifier);break;case DungeonOutcomeKind.Item:stats.GetComponent<PlayerInventory>()?.TryAddItem(item,Mathf.Max(1,amount));break;case DungeonOutcomeKind.LootPool:LootPoolDefinition.Entry entry=lootPool!=null?lootPool.Pick(random):null;if(entry!=null)stats.GetComponent<PlayerInventory>()?.TryAddItem(entry.item,entry.amount);break;case DungeonOutcomeKind.Karma:stats.ModifyKarma(amount,false);break;case DungeonOutcomeKind.Benedetto:stats.ModifyBenedetto(amount,false);break;case DungeonOutcomeKind.Malefico:stats.ModifyMalefico(amount,false);break;case DungeonOutcomeKind.StoryFlag:stats.SetStoryFlag(id,false);break;case DungeonOutcomeKind.Heal:stats.RestoreHealth(amount);break;case DungeonOutcomeKind.RestoreFlasks:stats.RestoreFlasks(amount);break;case DungeonOutcomeKind.MagicRecipe:stats.UnlockMagicRecipe(id,false);break;}
    }
}
