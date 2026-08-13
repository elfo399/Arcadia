using UnityEngine;

[CreateAssetMenu(menuName = "RogueLike/Usable Item")]
public class UsableItemData : ScriptableObject
{
    [Header("Persistence")]
    [Tooltip("Stable save identifier. Do not change after this definition ships.")]
    public string definitionId;

    [Header("Info")]
    public string itemName;
    [Min(0)] public int baseValue = 1;
    [TextArea] public string description;
    public Sprite icon;
    [Min(0f)] public float weight = 0.1f;

    public enum UsableEffectType
    {
        Heal,
        Mana,
        Invisibility,
        Custom
    }

    [Header("Uso")]
    // Per esempio tempo di ricarica tra gli usi
    public float cooldownSeconds = 0f;
    // Numero di cariche per singolo stack (se 0 = infinito)
    public int maxCharges = 0;

    [Header("Effetto")]
    public UsableEffectType effectType = UsableEffectType.Heal;
    // Durata in secondi per effetti temporanei (es. invisibilitÃ )
    public float durationSeconds = 0f;
    // Id libero per effetti custom gestiti dal gameplay code
    public string customEffectId;

    [Header("Effetti (placeholder)")]
    // Questi campi sono opzionali; puoi specializzarli a seconda del gioco
    public int healAmount = 0;
    public int manaRestore = 0;
}
