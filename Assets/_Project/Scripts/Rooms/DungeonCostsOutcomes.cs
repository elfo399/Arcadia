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
    /// <summary>Immediate-use receipt. It is intentionally not serializable or global.</summary>
    public sealed class DungeonCostPayment
    {
        private readonly PlayerStats stats; private readonly PlayerInventory inventory; private int coins; private int flasks; private float health; private readonly List<KeyValuePair<ItemData,int>> items;
        private bool committed; private bool rolledBack;
        internal DungeonCostPayment(PlayerStats stats,PlayerInventory inventory,List<KeyValuePair<ItemData,int>> items){this.stats=stats;this.inventory=inventory;this.items=items;}
        internal bool IsLethal { get; set; }
        internal void MarkCoins(int amount){coins=amount;} internal void MarkFlasks(int amount){flasks=amount;} internal void MarkHealth(float amount){health=amount;}
        public void Commit(){committed=true;}
        public bool Rollback()
        {
            if(committed||rolledBack||IsLethal)return false;rolledBack=true;bool success=true;
            for(int i=items.Count-1;i>=0;i--)if(inventory==null||!inventory.TryAddItem(items[i].Key,items[i].Value,false)){Debug.LogError("[DungeonCostPayment] Critical inventory rollback failure.");success=false;}
            if(health>0f)stats.RestoreHealth(health);if(flasks>0)stats.RestoreFlasks(flasks);if(coins>0)stats.AddCoins(coins,false);return success;
        }
    }
    public static bool CanPay(IReadOnlyList<DungeonCost> costs,PlayerStats stats)=>BuildTotals(costs,stats,out _);
    public static bool TryPay(IReadOnlyList<DungeonCost> costs,PlayerStats stats)=>TryPay(costs,stats,out _);
    /// <summary>When lethalPayment is true, death/run failure owns the result and normal rewards must not be applied.</summary>
    public static bool TryPay(IReadOnlyList<DungeonCost> costs,PlayerStats stats,out bool lethalPayment)
    {
        bool paid=TryPay(costs,stats,out DungeonCostPayment payment,out lethalPayment);if(paid&&!lethalPayment)payment?.Commit();return paid;
    }
    public static bool TryPay(IReadOnlyList<DungeonCost> costs,PlayerStats stats,out DungeonCostPayment payment,out bool lethalPayment)
    {
        payment=null;lethalPayment=false;if(!BuildTotals(costs,stats,out Totals totals))return false;PlayerInventory inventory=stats.GetComponent<PlayerInventory>();var removed=new List<KeyValuePair<ItemData,int>>();payment=new DungeonCostPayment(stats,inventory,removed);
        if(totals.coins>0&&!stats.TryRemoveCoins(totals.coins,false)){payment=null;return false;}payment.MarkCoins(totals.coins);
        if(totals.flasks>0&&!stats.TryConsumeFlasks(totals.flasks,false)){payment.Rollback();payment=null;return false;}payment.MarkFlasks(totals.flasks);
        foreach(var pair in totals.items)
        {
            int before=inventory.GetTotalItemAmount(pair.Key);
            if(!inventory.TryRemoveItem(pair.Key,pair.Value,out _,false))
            {
                int actuallyRemoved=Mathf.Max(0,before-inventory.GetTotalItemAmount(pair.Key));
                if(actuallyRemoved>0)removed.Add(new KeyValuePair<ItemData,int>(pair.Key,actuallyRemoved));
                payment.Rollback();payment=null;return false;
            }
            removed.Add(pair);
        }
        // Exact sacrifice is deliberately last: a lethal payment may immediately
        // enter run-failure flow, so no further resource mutation can follow it.
        if(totals.health>0f&&!stats.TrySacrificeHealth(totals.health,!totals.healthMustRemainNonLethal,false)){payment.Rollback();payment=null;return false;}payment.MarkHealth(totals.health);
        lethalPayment=totals.health>0f&&stats.currentHealth<=0f;payment.IsLethal=lethalPayment;
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

public enum DungeonOutcomeKind { RunModifier = 0, Item = 1, LootPool = 2, Karma = 3, StoryFlag = 6, Heal = 7, RestoreFlasks = 8, MagicRecipe = 9 }
[Serializable] public sealed class DungeonResolvedOutcome
{
    [NonSerialized] internal DungeonOutcome source;
    [NonSerialized] internal LootPoolDefinition.Entry resolvedLoot;
    public bool Apply(PlayerStats stats)
    {
        if(stats==null||source==null)return false;PlayerInventory inventory=stats.GetComponent<PlayerInventory>();
        switch(source.kind)
        {
            case DungeonOutcomeKind.RunModifier:return RunModifierController.Active!=null&&RunModifierController.Active.Add(source.modifier);
            case DungeonOutcomeKind.Item:return inventory!=null&&inventory.TryAddItem(source.item,Mathf.Max(1,source.amount));
            case DungeonOutcomeKind.LootPool:return resolvedLoot!=null&&inventory!=null&&inventory.TryAddItem(resolvedLoot.item,resolvedLoot.amount);
            case DungeonOutcomeKind.Karma:return stats.ModifyKarma(source.amount,false);
            case DungeonOutcomeKind.StoryFlag:return stats.HasStoryFlag(source.id)||stats.SetStoryFlag(source.id,false);
            case DungeonOutcomeKind.Heal:stats.RestoreHealth(source.amount);return true;
            case DungeonOutcomeKind.RestoreFlasks:stats.RestoreFlasks(source.amount);return true;
            case DungeonOutcomeKind.MagicRecipe:return stats.IsMagicRecipeUnlocked(source.id)||stats.UnlockMagicRecipe(source.id,false);
            default:return false;
        }
    }
}
[Serializable] public sealed class DungeonOutcome
{
    public DungeonOutcomeKind kind; public RunModifierDefinition modifier; public ItemData item; public LootPoolDefinition lootPool; public string id; public int amount=1;
    public bool TryResolve(PlayerStats stats,System.Random random,out DungeonResolvedOutcome resolved)
    {
        resolved=null;if(stats==null)return false;PlayerInventory inventory=stats.GetComponent<PlayerInventory>();var candidate=new DungeonResolvedOutcome{source=this};
        switch(kind)
        {
            case DungeonOutcomeKind.RunModifier:if(RunModifierController.Active==null||!RunModifierController.Active.CanAdd(modifier))return false;break;
            case DungeonOutcomeKind.Item:if(item==null||inventory==null||!inventory.CanAddItem(item,Mathf.Max(1,amount)))return false;break;
            case DungeonOutcomeKind.LootPool:candidate.resolvedLoot=lootPool!=null?lootPool.Pick(random,stats):null;if(candidate.resolvedLoot==null||inventory==null||!inventory.CanAddItem(candidate.resolvedLoot.item,candidate.resolvedLoot.amount))return false;break;
            case DungeonOutcomeKind.StoryFlag:if(string.IsNullOrWhiteSpace(id))return false;break;
            case DungeonOutcomeKind.MagicRecipe:if(string.IsNullOrWhiteSpace(id))return false;break;
        }
        resolved=candidate;return true;
    }
    public bool CanApply(PlayerStats stats,System.Random random)=>TryResolve(stats,random,out _);
    public void Apply(PlayerStats stats,System.Random random)
    {
        if(TryResolve(stats,random,out DungeonResolvedOutcome resolved))resolved.Apply(stats);
    }
}

public static class DungeonOutcomeResolution
{
    public static bool TryResolveAll(IReadOnlyList<DungeonOutcome> outcomes,PlayerStats stats,Func<int,System.Random> randomForIndex,out List<DungeonResolvedOutcome> resolved)
    {
        resolved=new List<DungeonResolvedOutcome>();if(outcomes==null)return true;
        var inventoryAdds=new Dictionary<ScriptableObject,int>();var uniqueModifiers=new HashSet<string>(StringComparer.Ordinal);
        for(int i=0;i<outcomes.Count;i++)if(outcomes[i]!=null)
        {
            if(!outcomes[i].TryResolve(stats,randomForIndex(i),out DungeonResolvedOutcome entry))return false;
            DungeonOutcome outcome=entry.source;
            if(outcome.kind==DungeonOutcomeKind.RunModifier&&outcome.modifier!=null&&outcome.modifier.stacking==RunModifierStacking.Unique&&!uniqueModifiers.Add(outcome.modifier.stableId.Trim()))return false;
            ScriptableObject inventoryItem=null;int amount=0;
            if(outcome.kind==DungeonOutcomeKind.Item){inventoryItem=outcome.item;amount=Mathf.Max(1,outcome.amount);}
            else if(outcome.kind==DungeonOutcomeKind.LootPool&&entry.resolvedLoot!=null){inventoryItem=entry.resolvedLoot.item;amount=entry.resolvedLoot.amount;}
            if(inventoryItem!=null)inventoryAdds[inventoryItem]=inventoryAdds.TryGetValue(inventoryItem,out int current)?SaturatingAdd(current,amount):amount;
            resolved.Add(entry);
        }
        PlayerInventory inventory=stats!=null?stats.GetComponent<PlayerInventory>():null;
        return inventoryAdds.Count==0||(inventory!=null&&inventory.CanAddItemsBatch(inventoryAdds));
    }
    public static bool ApplyAll(IReadOnlyList<DungeonResolvedOutcome> resolved,PlayerStats stats)
    {if(resolved==null)return true;foreach(DungeonResolvedOutcome entry in resolved)if(entry==null||!entry.Apply(stats))return false;return true;}
    private static int SaturatingAdd(int left,int right)=>left>int.MaxValue-right?int.MaxValue:left+right;
}
