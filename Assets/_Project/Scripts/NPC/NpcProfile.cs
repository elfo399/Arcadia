using UnityEngine;

[CreateAssetMenu(menuName = "Arcadia/NPC/NPC Profile")]
public sealed class NpcProfile : ScriptableObject
{
    public string npcId;
    public string displayName;
    public DialogueProfile dialogueProfile;
    public MerchantData merchantData;
}
