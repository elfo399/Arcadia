using UnityEngine;

[CreateAssetMenu(fileName = "DungeonRoomSet", menuName = "Dungeon/Room Set")]
public class DungeonRoomSet : ScriptableObject
{
    [Header("Start")]
    public Room startRoomPrefab;

    [Header("Normal")]
    public Room[] normal1x1Variants;
    public Room[] normal2x1Variants;
    public Room[] normal1x2Variants;
    public Room[] normal2x2Variants;

    [Header("Boss")]
    public Room[] boss1x1Variants;
    public Room[] boss2x1Variants;
    public Room[] boss1x2Variants;
    public Room[] boss2x2Variants;

    [Header("Treasure")]
    public Room[] treasure1x1Variants;
    public Room[] treasure2x1Variants;
    public Room[] treasure1x2Variants;
    public Room[] treasure2x2Variants;

    [Header("Shop")]
    public Room[] shop1x1Variants;
    public Room[] shop2x1Variants;
    public Room[] shop1x2Variants;
    public Room[] shop2x2Variants;

    [Header("Curch")]
    public Room[] curch1x1Variants;
    public Room[] curch2x2Variants;

    [Header("Evil Curch")]
    public Room[] evilCurch1x1Variants;
    public Room[] evilCurch2x2Variants;
}
