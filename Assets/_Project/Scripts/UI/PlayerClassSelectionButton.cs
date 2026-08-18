using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class PlayerClassSelectionButton : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("character")] private PlayerClassData playerClass;
    [SerializeField] private bool applyToCurrentPlayer;
    [SerializeField] private string sceneToLoadAfterSelect;

    public void SelectClass()
    {
        if (playerClass == null)
        {
            Debug.LogWarning("[PlayerClassSelectionButton] Player class not assigned.");
            return;
        }

        PlayerClassSelection.StartNewPlayer(playerClass);

        if (applyToCurrentPlayer)
            PlayerClassBootstrapper.ApplyToCurrentPlayer();

        if (!string.IsNullOrWhiteSpace(sceneToLoadAfterSelect))
        {
            if (string.Equals(sceneToLoadAfterSelect, "HubScene", System.StringComparison.OrdinalIgnoreCase))
                PlayerStats.instance?.EndDungeonRun(DungeonRunEndReason.VoluntaryExit);
            SceneManager.LoadScene(sceneToLoadAfterSelect);
        }
    }
}
