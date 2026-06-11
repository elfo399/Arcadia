using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerCharacterDatabase", menuName = "Arcadia/Player Character Database")]
public class PlayerCharacterDatabase : ScriptableObject
{
    [SerializeField] private PlayerCharacterData defaultCharacter;
    [SerializeField] private PlayerCharacterData[] characters = Array.Empty<PlayerCharacterData>();

    public PlayerCharacterData DefaultCharacter => defaultCharacter != null ? defaultCharacter : GetFirstCharacter();
    public PlayerCharacterData[] Characters => characters;

    public PlayerCharacterData GetById(string characterId)
    {
        if (characters == null || characters.Length == 0)
            return DefaultCharacter;

        if (string.IsNullOrWhiteSpace(characterId))
            return DefaultCharacter;

        string normalizedId = characterId.Trim();
        for (int i = 0; i < characters.Length; i++)
        {
            PlayerCharacterData character = characters[i];
            if (character == null)
                continue;

            if (string.Equals(character.GetCharacterId(), normalizedId, StringComparison.OrdinalIgnoreCase))
                return character;
        }

        return DefaultCharacter;
    }

    private PlayerCharacterData GetFirstCharacter()
    {
        if (characters == null)
            return null;

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null)
                return characters[i];
        }

        return null;
    }
}
