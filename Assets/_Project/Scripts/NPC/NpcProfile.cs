using UnityEngine;

[CreateAssetMenu(menuName = "Arcadia/NPC/NPC Profile")]
public sealed class NpcProfile : DialogueSpeakerData
{
    public DialogueProfile dialogueProfile;
    public MerchantData merchantData;
}
