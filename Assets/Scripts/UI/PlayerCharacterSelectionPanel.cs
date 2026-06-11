using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerCharacterSelectionPanel : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PlayerCharacterDatabase database;
    [SerializeField] private int initialIndex;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image portraitImage;

    [Header("Preview")]
    [SerializeField] private Transform previewRoot;

    [Header("Select")]
    [SerializeField] private bool applyToCurrentPlayer;
    [SerializeField] private string sceneToLoadAfterSelect;

    private int currentIndex;
    private GameObject previewInstance;

    private void Awake()
    {
        if (database == null)
            database = Resources.Load<PlayerCharacterDatabase>("PlayerCharacterDatabase");
    }

    private void OnEnable()
    {
        currentIndex = Mathf.Clamp(initialIndex, 0, Mathf.Max(0, GetCharacterCount() - 1));
        Refresh();
    }

    public void NextCharacter()
    {
        int count = GetCharacterCount();
        if (count <= 0)
            return;

        currentIndex = (currentIndex + 1) % count;
        Refresh();
    }

    public void PreviousCharacter()
    {
        int count = GetCharacterCount();
        if (count <= 0)
            return;

        currentIndex = (currentIndex - 1 + count) % count;
        Refresh();
    }

    public void SelectCurrent()
    {
        PlayerCharacterData character = GetCurrentCharacter();
        if (character == null)
            return;

        PlayerCharacterSelection.StartNewCharacter(character);

        if (applyToCurrentPlayer)
            PlayerCharacterBootstrapper.ApplyToCurrentPlayer(database);

        if (!string.IsNullOrWhiteSpace(sceneToLoadAfterSelect))
            SceneManager.LoadScene(sceneToLoadAfterSelect);
    }

    public PlayerCharacterData GetCurrentCharacter()
    {
        if (database == null || database.Characters == null || database.Characters.Length == 0)
            return null;

        currentIndex = Mathf.Clamp(currentIndex, 0, database.Characters.Length - 1);
        return database.Characters[currentIndex];
    }

    private void Refresh()
    {
        PlayerCharacterData character = GetCurrentCharacter();

        if (nameText != null)
            nameText.text = character != null ? character.displayName : string.Empty;

        if (descriptionText != null)
            descriptionText.text = character != null ? character.description : string.Empty;

        if (portraitImage != null)
        {
            Sprite portrait = character != null ? character.portrait : null;
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        RefreshPreview(character);
    }

    private void RefreshPreview(PlayerCharacterData character)
    {
        if (previewRoot == null)
            return;

        if (previewInstance != null)
            Destroy(previewInstance);

        if (character == null || character.previewPrefab == null)
            return;

        previewInstance = Instantiate(character.previewPrefab, previewRoot);
        previewInstance.transform.localPosition = Vector3.zero;
        previewInstance.transform.localRotation = Quaternion.identity;
    }

    private int GetCharacterCount()
    {
        return database != null && database.Characters != null ? database.Characters.Length : 0;
    }
}
