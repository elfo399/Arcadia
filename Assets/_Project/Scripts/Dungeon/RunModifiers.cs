using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunModifierStacking { Unique, Stack, Replace }
public enum RunModifierKind { Blessing, Curse, Pact }
public enum RunModifierEffect { DamageDealtMultiplier, DamageTakenMultiplier, MaxHealthMultiplier, MaxStaminaMultiplier, MaxManaMultiplier, StaminaRegenMultiplier, FlaskHealingMultiplier }
[Serializable] public sealed class RunModifierEffectEntry { public RunModifierEffect effect; [Min(.01f)] public float multiplierPerStack=1f; }
[CreateAssetMenu(fileName="RunModifier",menuName="Dungeon/Run Modifier")]
public sealed class RunModifierDefinition : ScriptableObject
{
    public string stableId; public RunModifierKind kind; public RunModifierStacking stacking=RunModifierStacking.Unique;
    [Tooltip("Legacy single effect. It is used when Effects is empty, so existing assets remain valid.")]
    public RunModifierEffect effect; [Min(.01f)] public float multiplierPerStack=1f;
    [Tooltip("A pact can carry several independent effects. Avoid repeating the same effect in one definition.")]
    public List<RunModifierEffectEntry> effects=new List<RunModifierEffectEntry>();
    [TextArea] public string description;

    public SavedRunModifierEffect[] BuildEffects()
    {
        if(effects==null||effects.Count==0)return new[]{new SavedRunModifierEffect{effect=effect,multiplierPerStack=Mathf.Max(.01f,multiplierPerStack)}};
        var result=new List<SavedRunModifierEffect>();
        foreach(var entry in effects)if(entry!=null)result.Add(new SavedRunModifierEffect{effect=entry.effect,multiplierPerStack=Mathf.Max(.01f,entry.multiplierPerStack)});
        return result.Count>0?result.ToArray():new[]{new SavedRunModifierEffect{effect=effect,multiplierPerStack=Mathf.Max(.01f,multiplierPerStack)}};
    }
}

/// <summary>Runtime records are authoritative after restore; definitions are used only to add new records.</summary>
[DisallowMultipleComponent]
public sealed class RunModifierController:MonoBehaviour
{
    private readonly Dictionary<string,SavedRunModifierState> states=new Dictionary<string,SavedRunModifierState>(StringComparer.Ordinal);
    public static RunModifierController Active{get;private set;} public event Action Changed;
    private void Awake(){Active=this;if(DungeonRunStateController.Active!=null)Import(DungeonRunStateController.Active.Modifiers);}
    private void OnDestroy(){if(Active==this)Active=null;}
    public bool Add(RunModifierDefinition definition)
    {
        if(definition==null||string.IsNullOrWhiteSpace(definition.stableId))return false;string id=definition.stableId.Trim();
        if(states.TryGetValue(id,out var record)){if(definition.stacking==RunModifierStacking.Unique)return false;record.stacks=definition.stacking==RunModifierStacking.Replace?1:record.stacks+1;states[id]=record;}
        else states[id]=new SavedRunModifierState{modifierId=id,stacks=1,effect=definition.effect,multiplierPerStack=Mathf.Max(.01f,definition.multiplierPerStack),effects=definition.BuildEffects()};
        Sync();return true;
    }
    public bool Remove(string id){if(string.IsNullOrWhiteSpace(id)||!states.Remove(id))return false;Sync();return true;}
    public int GetStacks(string id)=>!string.IsNullOrWhiteSpace(id)&&states.TryGetValue(id,out var record)?record.stacks:0;
    public float GetMultiplier(RunModifierEffect effect)
    {
        float value=1f;
        foreach(var record in states.Values)
        {
            SavedRunModifierEffect[] entries=GetEffects(record);
            foreach(var entry in entries)if(entry!=null&&entry.effect==effect)value*=Mathf.Pow(Mathf.Max(.01f,entry.multiplierPerStack),Mathf.Max(1,record.stacks));
        }
        return value;
    }
    public void RestoreFromRunState(){states.Clear();if(DungeonRunStateController.Active!=null)Import(DungeonRunStateController.Active.Modifiers);PlayerStats.instance?.RecalculateDerivedStats(true);Changed?.Invoke();}
    public void ClearForRunEnd(){states.Clear();Sync();}
    private void Import(IReadOnlyList<SavedRunModifierState> saved){if(saved==null)return;foreach(var record in saved)if(record!=null&&!string.IsNullOrWhiteSpace(record.modifierId)){record.stacks=Mathf.Max(1,record.stacks);record.multiplierPerStack=Mathf.Max(.01f,record.multiplierPerStack);record.effects=GetEffects(record);states[record.modifierId]=record;}}
    private static SavedRunModifierEffect[] GetEffects(SavedRunModifierState record)
    {
        if(record.effects!=null&&record.effects.Length>0)return record.effects;
        return new[]{new SavedRunModifierEffect{effect=record.effect,multiplierPerStack=Mathf.Max(.01f,record.multiplierPerStack)}};
    }
    private void Sync(){var list=new List<SavedRunModifierState>(states.Values);list.Sort((a,b)=>string.CompareOrdinal(a.modifierId,b.modifierId));DungeonRunStateController.Active?.SetModifiers(list);PlayerStats.instance?.RecalculateDerivedStats(true);Changed?.Invoke();}
}
