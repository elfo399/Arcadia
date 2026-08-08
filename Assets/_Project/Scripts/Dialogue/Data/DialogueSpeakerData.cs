using UnityEngine;

[CreateAssetMenu(fileName = "DialogueSpeaker", menuName = "Arcadia/Dialogue/Speaker")]
public sealed class DialogueSpeakerData : ScriptableObject
{
    [Tooltip("ID stabile usato dagli asset dialogo e dai binding runtime.")]
    public string speakerId;
    public string displayName;
    public Sprite portrait;
    [Tooltip("Il nome viene risolto da PlayerStats.PlayerName a runtime.")]
    public bool isPlayer;

    public string ResolveDisplayName(PlayerStats playerStats)
    {
        if (isPlayer)
            return playerStats != null ? playerStats.PlayerName : SaveSystem.DefaultPlayerName;

        return string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(speakerId))
            Debug.LogWarning($"[DialogueSpeakerData] Speaker '{name}' senza speakerId stabile.", this);
    }
#endif
}
