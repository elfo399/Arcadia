using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerCharacterSelectionButton : MonoBehaviour
{
    [SerializeField] private PlayerCharacterData character;
    [SerializeField] private bool applyToCurrentPlayer;
    [SerializeField] private string sceneToLoadAfterSelect;

    public void SelectCharacter()
    {
        if (character == null)
        {
            Debug.LogWarning("[PlayerCharacterSelectionButton] Character not assigned.");
            return;
        }

        PlayerCharacterSelection.StartNewCharacter(character);

        if (applyToCurrentPlayer)
            PlayerCharacterBootstrapper.ApplyToCurrentPlayer();

        if (!string.IsNullOrWhiteSpace(sceneToLoadAfterSelect))
            SceneManager.LoadScene(sceneToLoadAfterSelect);
    }
}
