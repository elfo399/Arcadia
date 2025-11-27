using UnityEngine;

[CreateAssetMenu(fileName = "NewRoom", menuName = "Dungeon/Room Data")]
public class RoomData : ScriptableObject
{
    [Header("Identità")]
    public string roomName;
    public GameObject roomPrefab; // Il modello 3D della stanza

    [Header("Dimensioni Griglia")]
    public Vector2Int size = new Vector2Int(1, 1); // 1x1, 2x1, 2x2, ecc.

    [Header("Tipo")]
    public bool isBossRoom;
    public bool isTreasureRoom;
    public bool isStartRoom;
}