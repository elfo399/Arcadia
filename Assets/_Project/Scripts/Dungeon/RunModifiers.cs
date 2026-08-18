using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunModifierStacking { Unique, Stack, Replace }
public enum RunModifierKind { Blessing, Curse, Pact }
[CreateAssetMenu(fileName="RunModifier", menuName="Dungeon/Run Modifier")]
public sealed class RunModifierDefinition : ScriptableObject
{
    public string stableId;
    public RunModifierKind kind;
    public RunModifierStacking stacking = RunModifierStacking.Unique;
    [TextArea] public string description;
}

/// <summary>Single authority for temporary run modifiers. Consumers query it; shrines/events never own modifier lists.</summary>
[DisallowMultipleComponent]
public sealed class RunModifierController : MonoBehaviour
{
    private readonly Dictionary<string,int> active = new Dictionary<string,int>(StringComparer.Ordinal);
    public static RunModifierController Active { get; private set; }
    public event Action Changed;
    private void Awake(){Active=this; if(DungeonRunStateController.Active!=null) Import(DungeonRunStateController.Active.Modifiers);}
    private void OnDestroy(){if(Active==this)Active=null;}
    public bool Add(RunModifierDefinition definition)
    {
        if(definition==null||string.IsNullOrWhiteSpace(definition.stableId)) return false; string id=definition.stableId.Trim();
        if(active.TryGetValue(id,out int stacks)) { if(definition.stacking==RunModifierStacking.Unique)return false; active[id]=definition.stacking==RunModifierStacking.Replace?1:stacks+1; } else active[id]=1;
        Sync(); return true;
    }
    public bool Remove(string id){if(string.IsNullOrWhiteSpace(id)||!active.Remove(id))return false;Sync();return true;}
    public int GetStacks(string id)=>!string.IsNullOrWhiteSpace(id)&&active.TryGetValue(id,out int stacks)?stacks:0;
    public void RestoreFromRunState(){active.Clear();if(DungeonRunStateController.Active!=null)Import(DungeonRunStateController.Active.Modifiers);Changed?.Invoke();}
    public void ClearForRunEnd(){active.Clear();Sync();}
    private void Import(IReadOnlyList<SavedRunModifierState> saved){if(saved==null)return;foreach(var item in saved)if(item!=null&&!string.IsNullOrWhiteSpace(item.modifierId))active[item.modifierId]=Mathf.Max(1,item.stacks);}
    private void Sync(){var list=new List<SavedRunModifierState>();foreach(var item in active)list.Add(new SavedRunModifierState{modifierId=item.Key,stacks=item.Value});DungeonRunStateController.Active?.SetModifiers(list);Changed?.Invoke();}
}
