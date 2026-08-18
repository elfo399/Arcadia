#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DungeonIdentityTools
{
    [MenuItem("Arcadia/Dungeon/Generate and validate stable IDs")]
    public static void GenerateAndValidate()
    {
        var roomIds=new Dictionary<string,string>(StringComparer.Ordinal);int changed=0;
        foreach(string guid in AssetDatabase.FindAssets("t:RoomData"))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);RoomData data=AssetDatabase.LoadAssetAtPath<RoomData>(path);if(data==null)continue;
            if(string.IsNullOrWhiteSpace(data.stableId)){data.stableId="roomdef-"+Guid.NewGuid().ToString("N");EditorUtility.SetDirty(data);changed++;}
            if(roomIds.TryGetValue(data.stableId,out string other))Debug.LogError($"[Dungeon IDs] duplicate RoomData stableId '{data.stableId}': {other} and {path}",data);else roomIds.Add(data.stableId,path);
        }
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);GameObject root=PrefabUtility.LoadPrefabContents(path);Room room=root.GetComponent<Room>();if(room==null){PrefabUtility.UnloadPrefabContents(root);continue;}
            var seen=new HashSet<string>(StringComparer.Ordinal);bool dirty=false;foreach(RoomRule rule in root.GetComponentsInChildren<RoomRule>(true)){if(string.IsNullOrWhiteSpace(rule.RuleId)||!seen.Add(rule.RuleId)){rule.SetEditorRuleId("rule-"+Guid.NewGuid().ToString("N"));seen.Add(rule.RuleId);dirty=true;changed++;}}
            bool legacySpecial=room.roomData!=null&&!room.roomData.isStartRoom&&(room.roomData.isShopRoom||room.roomData.isTreasureRoom||room.roomData.isBlessedRoom||room.roomData.isEvilRoom);
            if(legacySpecial)foreach(InteractableDoor door in root.GetComponentsInChildren<InteractableDoor>(true)){door.SetLegacyWholeRoomUnlockForMigration(true);dirty=true;}
            if(dirty)PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
        }
        if(changed>0)AssetDatabase.SaveAssets();Debug.Log($"[Dungeon IDs] complete; generated/repaired {changed} IDs.");
    }
}
#endif
