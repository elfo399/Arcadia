using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Migrates the existing authored LockDoor objects to socket-bound inventory-key gates.</summary>
public static class SpecialRoomGateMigration
{
    private const string KeyAssetPath="Assets/_Project/Data/Database/Items/NoUsable/Key/Key.asset";
    private const string SessionMigrationKey="Arcadia.SpecialRoomGateMigration.v1";

    [InitializeOnLoadMethod]
    private static void ScheduleAutomaticMigration()
    {
        if(SessionState.GetBool(SessionMigrationKey,false))return;
        EditorApplication.delayCall+=()=>
        {
            if(SessionState.GetBool(SessionMigrationKey,false))return;
            try { RunBatchMigration();SessionState.SetBool(SessionMigrationKey,true); }
            catch(Exception exception) { Debug.LogError("[SpecialRoomGateMigration] "+exception); }
        };
    }

    [MenuItem("Arcadia/Migration/Migrate Special Room Key Gates")]
    public static void Migrate()
    {
        ItemData key=AssetDatabase.LoadAssetAtPath<ItemData>(KeyAssetPath);
        if(key==null)throw new InvalidOperationException("Missing modern Key ItemData at "+KeyAssetPath);
        string[] guids=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_Project/Data/Database/Floor"});
        int prefabCount=0,gateCount=0;
        foreach(string guid in guids)
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(!IsSpecialRoomPath(path))continue;
            GameObject root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                Room room=root.GetComponent<Room>();
                if(room==null)continue;
                bool changed=false;
                for(int i=0;i<room.doors.Count;i++)
                {
                    Room.DoorEntry entry=room.doors[i];
                    InteractableDoor gate=entry.lockObject!=null?entry.lockObject.GetComponent<InteractableDoor>():null;
                    if(gate==null)continue;
                    entry.authoredGate=gate;
                    room.doors[i]=entry;
                    string id=$"entry-{entry.label}-{entry.gridOffset.x}-{entry.gridOffset.y}-{entry.direction.x}-{entry.direction.y}";
                    gate.ConfigureAsKeyGate(id,key,entry.lockObject.GetComponent<Collider>());
                    EditorUtility.SetDirty(gate);
                    changed=true;gateCount++;
                }
                if(!changed)continue;
                EditorUtility.SetDirty(room);
                PrefabUtility.SaveAsPrefabAsset(root,path);
                prefabCount++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[SpecialRoomGateMigration] Migrated {gateCount} gates across {prefabCount} special-room prefabs.");
    }

    [MenuItem("Arcadia/Migration/Migrate Key Pickup")]
    public static void MigrateKeyPickup()
    {
        ItemData key=AssetDatabase.LoadAssetAtPath<ItemData>(KeyAssetPath);
        GameObject root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Items/Key.prefab");
        try
        {
            KeyPickup pickup=root.GetComponentInChildren<KeyPickup>(true);
            if(pickup==null)throw new InvalidOperationException("Key prefab has no KeyPickup component.");
            SerializedObject serialized=new SerializedObject(pickup);
            serialized.FindProperty("keyItem").objectReferenceValue=key;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/_Project/Prefabs/Items/Key.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    public static void RunBatchMigration(){Migrate();MigrateKeyPickup();}
    private static bool IsSpecialRoomPath(string path)=>path.IndexOf("/Rooms/Shop/",StringComparison.Ordinal)>=0||path.IndexOf("/Rooms/Treasure/",StringComparison.Ordinal)>=0||path.IndexOf("/Rooms/Curch/",StringComparison.Ordinal)>=0||path.IndexOf("/Rooms/EvilCurch/",StringComparison.Ordinal)>=0;
}
