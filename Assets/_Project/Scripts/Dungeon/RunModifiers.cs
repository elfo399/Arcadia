using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunModifierStacking { Unique, Stack, Replace }
public enum RunModifierKind { Blessing, Curse, Pact }
public enum RunModifierEffect { DamageDealtMultiplier, DamageTakenMultiplier }
[CreateAssetMenu(fileName="RunModifier", menuName="Dungeon/Run Modifier")]
public sealed class RunModifierDefinition : ScriptableObject
{
    public string stableId;
    public RunModifierKind kind;
    public RunModifierStacking stacking = RunModifierStacking.Unique;
    public RunModifierEffect effect;
    [Min(0.01f)] public float multiplierPerStack = 1f;
    [TextArea] public string description;
}

/// <summary>Single authority for temporary run modifiers. Consumers query it; shrines/events never own modifier lists.</summary>
[DisallowMultipleComponent]
public sealed class RunModifierController : MonoBehaviour
{
    private readonly Dictionary<string,int> active = new Dictionary<string,int>(StringComparer.Ordinal);
    private readonly Dictionary<string,RunModifierDefinition> definitions = new Dictionary<string,RunModifierDefinition>(StringComparer.Ordinal);
    private readonly List<SavedRunModifierState> modifiers = new List<SavedRunModifierState>();
    public static RunModifierController Active { get; private set; }
    public event Action Changed;
    private void Awake(){Active=this; if(DungeonRunStateController.Active!=null) Import(DungeonRunStateController.Active.Modifiers);}
    private void OnDestroy(){if(Active==this)Active=null;}
    public bool Add(RunModifierDefinition definition)
    {
        if(definition==null||string.IsNullOrWhiteSpace(definition.stableId)) return false; string id=definition.stableId.Trim();
        definitions[id]=definition; if(active.TryGetValue(id,out int stacks)) { if(definition.stacking==RunModifierStacking.Unique)return false; active[id]=definition.stacking==RunModifierStacking.Replace?1:stacks+1; } else active[id]=1;
        Sync(); return true;
    }
    public bool Remove(string id){if(string.IsNullOrWhiteSpace(id)||!active.Remove(id))return false;Sync();return true;}
    public int GetStacks(string id)=>!string.IsNullOrWhiteSpace(id)&&active.TryGetValue(id,out int stacks)?stacks:0;
    public float GetMultiplier(RunModifierEffect effect)
    {
        float value=1f; foreach(var item in modifiers) if(item!=null&&item.effect==effect)value*=Mathf.Pow(Mathf.Max(.01f,item.multiplierPerStack),item.stacks); return value;
    }
    public void RestoreFromRunState(){active.Clear();modifiers.Clear();if(DungeonRunStateController.Active!=null)Import(DungeonRunStateController.Active.Modifiers);Changed?.Invoke();}
    public void ClearForRunEnd(){active.Clear();Sync();}
    private void Import(IReadOnlyList<SavedRunModifierState> saved){if(saved==null)return;foreach(var item in saved)if(item!=null&&!string.IsNullOrWhiteSpace(item.modifierId)){active[item.modifierId]=Mathf.Max(1,item.stacks);modifiers.Add(item);}}
    private void Sync(){modifiers.Clear();foreach(var item in active){definitions.TryGetValue(item.Key,out RunModifierDefinition definition);modifiers.Add(new SavedRunModifierState{modifierId=item.Key,stacks=item.Value,effect=definition!=null?definition.effect:RunModifierEffect.DamageDealtMultiplier,multiplierPerStack=definition!=null?definition.multiplierPerStack:1f});}DungeonRunStateController.Active?.SetModifiers(modifiers);Changed?.Invoke();}
}
