using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Deterministic, authored-pedestal reward choices. It never auto-selects an offer.</summary>
public sealed class RoomRewardRule : RoomRule
{
    [Tooltip("Only enable when taking the configured number of rewards must complete the room.")]
    [SerializeField] private bool blocksUntilClaimed;
    public override bool BlocksRoomCompletion => blocksUntilClaimed;
    [SerializeField] private LootPoolDefinition lootPool;
    [SerializeField,Min(1)] private int generatedChoices=1;
    [SerializeField,Min(1)] private int maxClaims=1;
    [SerializeField] private bool completeAfterClaims;
    [SerializeField] private string requiredRuleId;
    [SerializeField] private bool requiresRuleSuccess;
    private LootPoolDefinition.Entry[] offers; private readonly HashSet<int> claimed=new HashSet<int>(); private DungeonRewardOfferAnchor[] anchors;
    protected override void OnRoomInitialized()
    {
        if(lootPool==null&&CoreGenerator.Instance!=null&&CoreGenerator.Instance.ActiveFloorDefinition!=null)
        {
            var pools=CoreGenerator.Instance.ActiveFloorDefinition.lootPools;
            if(pools!=null&&pools.Count>0)
            {
                var valid=new List<LootPoolDefinition>();foreach(var pool in pools)if(pool!=null)valid.Add(pool);
                if(valid.Count>0)lootPool=valid[Context.CreateRandom(RuleId+":floor-loot-pool").Next(valid.Count)];
            }
        }
        BuildOffers();anchors=GetComponentsInChildren<DungeonRewardOfferAnchor>(true);BindAnchors();
    }
    protected override void OnStateRestored(string payload){if(!string.IsNullOrWhiteSpace(payload))foreach(string token in payload.Split(','))if(int.TryParse(token,out int index))claimed.Add(index);BindAnchors();if((completeAfterClaims||blocksUntilClaimed)&&claimed.Count>=maxClaims)Complete();}
    protected override string CaptureState(){var indices=new List<int>(claimed);indices.Sort();return string.Join(",",indices);}
    private void BuildOffers(){if(lootPool==null)return;offers=new LootPoolDefinition.Entry[Mathf.Max(1,generatedChoices)];var random=Context.CreateRandom(RuleId+":offers");for(int i=0;i<offers.Length;i++)offers[i]=lootPool.Pick(random,PlayerStats.instance);}
    private bool IsAvailable(){if(!requiresRuleSuccess)return true;RoomRule source=Context.Room.GetRule(requiredRuleId);return source!=null&&source.Outcome==RoomRuleOutcome.Succeeded;}
    private void BindAnchors()
    {
        if(anchors==null||offers==null)return;bool exhausted=claimed.Count>=maxClaims;for(int i=0;i<anchors.Length;i++){var anchor=anchors[i];if(anchor==null)continue;bool offered=i<offers.Length&&offers[i]!=null&&offers[i].item!=null&&!claimed.Contains(i)&&!exhausted&&IsAvailable();anchor.Configure(this,i,offered?offers[i]:null,offered);}
    }
    internal void TryClaim(int index,GameObject player)
    {
        if(!IsAvailable()||offers==null||index<0||index>=offers.Length||claimed.Contains(index)||claimed.Count>=maxClaims)return;var offer=offers[index];PlayerInventory inventory=player!=null?player.GetComponentInParent<PlayerInventory>():null;if(offer==null||offer.item==null||inventory==null||!inventory.TryAddItem(offer.item,offer.amount))return;
        claimed.Add(index);Context.State.rewardClaimed=true;if((completeAfterClaims||blocksUntilClaimed)&&claimed.Count>=maxClaims)Complete();BindAnchors();Context.Room.NotifyRuleChanged(this);
    }
    public override void OnRuleChanged(RoomRule rule){if(requiresRuleSuccess&&rule!=null&&rule.RuleId==requiredRuleId)BindAnchors();}
#if UNITY_EDITOR
    protected override void OnValidate(){base.OnValidate();if(maxClaims>generatedChoices)maxClaims=generatedChoices;if(blocksUntilClaimed)completeAfterClaims=true;if(lootPool==null)Debug.LogWarning($"[RoomRewardRule] '{name}' has no LootPool.",this);}
#endif
}

/// <summary>Put one on every authored pedestal. The component is both its physical presentation and interaction target.</summary>
public sealed class DungeonRewardOfferAnchor : MonoBehaviour,IInteractable
{
    [SerializeField] private GameObject availableVisual; [SerializeField] private GameObject claimedVisual; [SerializeField] private string prompt="Take reward";
    private RoomRewardRule owner; private int index; private bool available;
    internal void Configure(RoomRewardRule rule,int offerIndex,LootPoolDefinition.Entry entry,bool canClaim){owner=rule;index=offerIndex;available=canClaim;if(availableVisual)availableVisual.SetActive(canClaim);if(claimedVisual)claimedVisual.SetActive(!canClaim);}
    public void Interact(GameObject player){if(available&&owner!=null)owner.TryClaim(index,player);}
    public string GetPrompt()=>available?prompt:string.Empty;
}

/// <summary>Temporary migration for existing RoomData.rewards. Modern RoomRewardRule takes precedence.</summary>
public sealed class LegacyRoomRewardRule : RoomRule
{
    public override bool BlocksRoomCompletion => false;
    public override void OnRoomCompleted()
    {
        if(Context.State.rewardClaimed||Context.Room.roomData==null)return;var random=Context.CreateRandom("legacy-rewards");
        foreach(var loot in Context.Room.roomData.rewards)
        {
            if(loot==null||loot.itemPrefab==null||random.Next(0,101)>loot.dropChance)continue;int amount=1;
            if(loot.quantityWeights!=null&&loot.quantityWeights.Count>0){float total=0;foreach(var weight in loot.quantityWeights)if(weight!=null)total+=Mathf.Max(0,weight.chance);double roll=random.NextDouble()*total;float cursor=0;foreach(var weight in loot.quantityWeights)if(weight!=null){cursor+=Mathf.Max(0,weight.chance);if(roll<=cursor){amount=Mathf.Max(0,weight.amount);break;}}}
            for(int i=0;i<amount;i++){Vector3 offset=new Vector3((float)(random.NextDouble()*4-2),.2f,(float)(random.NextDouble()*4-2));Instantiate(loot.itemPrefab,Context.Room.transform.position+offset,Quaternion.identity,Context.Room.transform);}
        }
        Context.State.rewardClaimed=true;Context.Room.NotifyRuleChanged(this);
    }
}
