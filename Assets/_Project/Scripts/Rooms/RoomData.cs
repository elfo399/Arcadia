using UnityEngine;

[CreateAssetMenu(fileName = "NewRoom", menuName = "Dungeon/Room Data")]
public class RoomData : ScriptableObject, ISerializationCallbackReceiver
{
    [Header("Identità")]
    [Tooltip("Immutable authoring ID used by deterministic generated-room identities.")]
    public string stableId;
    public string roomName;
    public GameObject roomPrefab; 
    [Min(1)] [Tooltip("Relative deterministic selection weight inside a matching room pool.")]
    public int generationWeight = 1;

    [Header("Dimensioni Griglia")]
    public Vector2Int size = new Vector2Int(1, 1); 

    [Header("Structural type")]
    public RoomType roomType = RoomType.Normal;
    
    public void OnBeforeSerialize()
    {
        roomType = RoomTypeMigration.Normalize(roomType);
    }

    public void OnAfterDeserialize()
    {
        roomType = RoomTypeMigration.Normalize(roomType);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        roomType = RoomTypeMigration.Normalize(roomType);
        if (string.IsNullOrWhiteSpace(stableId))
        {
            stableId = "roomdef-" + System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
