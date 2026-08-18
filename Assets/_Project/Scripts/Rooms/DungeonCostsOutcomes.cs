using System;
using System.Collections.Generic;
using UnityEngine;

public enum DungeonCostKind { Coins, Health, Flasks, InventoryItem }
[Serializable] public sealed class DungeonCost
{
    public DungeonCostKind kind; [Min(1)] public int amount=1; public ItemData item; public bool allowLethal;
    public bool CanPay(PlayerStats stats)
    {
        if(stats==null)return false;switch(kind){case DungeonCostKind.Coins:return stats.HasCoins(amount);case DungeonCostKind.Flasks:return stats.HasFlasks(amount);case DungeonCostKind.Health:return stats.CanSacrificeHealth(amount,allowLethal);case DungeonCostKind.InventoryItem:{var inventory=stats.GetComponent<PlayerInventory>();return item!=null&&inventory!=null&&inventory.GetTotalItemAmount(item)>=amount;}default:return false;}
    }
}

/// <summary>Aggregates every resource before mutating any authoritative player state.</summary>
public static class DungeonCostTransaction
{
    private sealed class Totals { public int coins; public int flasks; public float health; public bool healthMustRemainNonLethal; public readonly Dictionary<ItemData,int> items=new Dictionary<ItemData,int>(); }
    public static bool CanPay(IReadOnlyList<DungeonCost> costs,PlayerStats stats)=>BuildTotals(costs,stats,out _);
    public static bool TryPay(IReadOnlyList<DungeonCost> costs,PlayerStats stats)
    {
        if(!BuildTotals(costs,stats,out Totals totals))return false;PlayerInventory inventory=stats.GetComponent<PlayerInventory>();bool coinsPaid=false,flasksPaid=false,healthPaid=false;var removed=new List<KeyValuePair<ItemData,int>>();
        if(totals.coins>0&&!stats.TryRemoveCoins(totals.coins,false))return false;coinsPaid=totals.coins>0;
        if(totals.flasks>0&&!stats.TryConsumeFlasks(totals.flasks,false)){Rollback(stats,inventory,totals,coinsPaid,false,false,removed);return false;}flasksPaid=totals.flasks>0;
        if(totals.health>0f&&!stats.TrySacrificeHealth(totals.health,!totals.healthMustRemainNonLethal,false)){Rollback(stats,inventory,totals,coinsPaid,flasksPaid,false,removed);return false;}healthPaid=totals.health>0f;
        foreach(var pair in totals.items)
        {
            int before=inventory.GetTotalItemAmount(pair.Key);
            if(!inventory.TryRemoveItem(pair.Key,pair.Value,out _,false))
            {
                int actuallyRemoved=Mathf.Max(0,before-inventory.GetTotalItemAmount(pair.Key));
                if(actuallyRemoved>0)removed.Add(new KeyValuePair<ItemData,int>(pair.Key,actuallyRemoved));
                Rollback(stats,inventory,totals,coinsPaid,flasksPaid,healthPaid,removed);return false;
            }
            removed.Add(pair);
        }
        return true;
    }
    /// <summary>Compatibility path for consuming legacy requirements.</summary>
    public static bool TryConsumeRequirements(IEnumerable<DungeonRequirement> requirements,PlayerStats stats)
    {
        if(requirements==null)return true;var costs=new List<DungeonCost>();foreach(var requirement in requirements){if(requirement==null||!requirement.IsMet(stats))return false;if(requirement.kind==DungeonRequirementKind.InventoryItem&&requirement.consumeItem)costs.Add(new DungeonCost{kind=DungeonCostKind.InventoryItem,item=requirement.item,amount=Mathf.Max(1,requirement.amount)});}return TryPay(costs,stats);
    }
    private static bool BuildTotals(IReadOnlyList<DungeonCost> costs,PlayerStats stats,out Totals totals)
    {
        totals=new Totals();if(stats==null)return false;if(costs==null)return true;foreach(var cost in costs){if(cost==null)continue;int amount=Mathf.Max(1,cost.amount);switch(cost.kind){case DungeonCostKind.Coins:totals.coins=SaturatingAdd(totals.coins,amount);break;case DungeonCostKind.Flasks:totals.flasks=SaturatingAdd(totals.flasks,amount);break;case DungeonCostKind.Health:totals.health+=amount;totals.healthMustRemainNonLethal|=!cost.allowLethal;break;case DungeonCostKind.InventoryItem:if(cost.item==null)return false;totals.items[cost.item]=totals.items.TryGetValue(cost.item,out int existing)?SaturatingAdd(existing,amount):amount;break;default:return false;}}
        if(!stats.HasCoins(totals.coins)||!stats.HasFlasks(totals.flasks)||!stats.CanSacrificeHealth(totals.health,!totals.healthMustRemainNonLethal))return false;PlayerInventory inventory=stats.GetComponent<PlayerInventory>();if(totals.items.Count>0&&inventory==null)return false;foreach(var pair in totals.items)if(inventory.GetTotalItemAmount(pair.Key)<pair.Value)return false;return true;
    }
    private static int SaturatingAdd(int left,int right)=>left>int.MaxValue-right?int.MaxValue:left+right;
    private static void Rollback(PlayerStats stats,PlayerInventory inventory,Totals totals,bool coinsPaid,bool flasksPaid,bool healthPaid,List<KeyValuePair<ItemData,int>> removed)
    {for(int i=removed.Count-1;i>=0;i--)if(inventory==null||!inventory.TryAddItem(removed[i].Key,removed[i].Value,false))Debug.LogError("[DungeonCostTransaction] Inventory rollback failed.");if(healthPaid)stats.RestoreHealth(totals.health);if(flasksPaid)stats.RestoreFlasks(totals.flasks);if(coinsPaid)stats.AddCoins(totals.coins,false);}
}

public enum DungeonOutcomeKind { RunModifier, Item, LootPool, Karma, Benedetto, Malefico, StoryFlag, Heal, RestoreFlasks, MagicRecipe }
[Serializable] public sealed class DungeonOutcome
{
    public DungeonOutcomeKind kind; public RunModifierDefinition modifier; public ItemData item; public LootPoolDefinition lootPool; public string id; public int amount=1;
    public bool CanApply(PlayerStats stats,System.Random random)
    {
        if(stats==null)return false;PlayerInventory inventory=stats.GetComponent<PlayerInventory>();switch(kind){case DungeonOutcomeKind.RunModifier:return RunModifierController.Active!=null&&RunModifierController.Active.CanAdd(modifier);case DungeonOutcomeKind.Item:return item!=null&&inventory!=null&&inventory.CanAddItem(item,Mathf.Max(1,amount));case DungeonOutcomeKind.LootPool:LootPoolDefinition.Entry entry=lootPool!=null?lootPool.Pick(random):null;return entry!=null&&entry.item!=null&&inventory!=null&&inventory.CanAddItem(entry.item,entry.amount);case DungeonOutcomeKind.StoryFlag:return !string.IsNullOrWhiteSpace(id);case DungeonOutcomeKind.MagicRecipe:return !string.IsNullOrWhiteSpace(id)&&!stats.IsMagicRecipeUnlocked(id);default:return true;}
    }
    public void Apply(PlayerStats stats,System.Random random)
    {
        if(stats==null)return;switch(kind){case DungeonOutcomeKind.RunModifier:RunModifierController.Active?.Add(modifier);break;case DungeonOutcomeKind.Item:stats.GetComponent<PlayerInventory>()?.TryAddItem(item,Mathf.Max(1,amount));break;case DungeonOutcomeKind.LootPool:LootPoolDefinition.Entry entry=lootPool!=null?lootPool.Pick(random):null;if(entry!=null)stats.GetComponent<PlayerInventory>()?.TryAddItem(entry.item,entry.amount);break;case DungeonOutcomeKind.Karma:stats.ModifyKarma(amount,false);break;case DungeonOutcomeKind.Benedetto:stats.ModifyBenedetto(amount,false);break;case DungeonOutcomeKind.Malefico:stats.ModifyMalefico(amount,false);break;case DungeonOutcomeKind.StoryFlag:stats.SetStoryFlag(id,false);break;case DungeonOutcomeKind.Heal:stats.RestoreHealth(amount);break;case DungeonOutcomeKind.RestoreFlasks:stats.RestoreFlasks(amount);break;case DungeonOutcomeKind.MagicRecipe:stats.UnlockMagicRecipe(id,false);break;}
    }
}
