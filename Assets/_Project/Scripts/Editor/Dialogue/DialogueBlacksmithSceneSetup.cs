using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DialogueBlacksmithSceneSetup
{
    private const string HubScenePath = "Assets/_Project/Scenes/HubSceneV1.unity";
    private const string PlayerSpeakerPath = "Assets/_Project/Dialogue/Speakers/Speaker_Player.asset";
    private const string BlacksmithSpeakerPath = "Assets/_Project/NPC/Blacksmith_Eldar/Dialogues/Speaker_Eldar.asset";
    private const string BlacksmithProfilePath = "Assets/_Project/NPC/Blacksmith_Eldar/Dialogues/Profile_Eldar.asset";

    [MenuItem("Arcadia/Dialogue/Setup Active Hub Blacksmith", priority = 120)]
    public static void SetupActiveHubBlacksmith()
    {
        SetupActiveHubBlacksmithInternal(showCompletionLog: true);
    }

    public static void SetupHubBlacksmithBatch()
    {
        EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
        if (!SetupActiveHubBlacksmithInternal(showCompletionLog: true))
            throw new InvalidOperationException("Setup batch del Fabbro non completato.");
    }

    private static bool SetupActiveHubBlacksmithInternal(bool showCompletionLog)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != HubScenePath)
        {
            Debug.LogWarning(
                $"[Dialogue Setup] Apri '{HubScenePath}' e rendila la scena attiva prima di eseguire il setup.");
            return false;
        }

        GameObject npcRoot = FindSceneObject(scene, "__NPC");
        GameObject blacksmith = npcRoot != null
            ? FindDescendant(npcRoot.transform, "city_dwellers_1")?.gameObject
            : null;
        if (blacksmith == null && npcRoot != null)
        {
            NPCInteractable existingNpc = npcRoot.GetComponentInChildren<NPCInteractable>(true);
            blacksmith = existingNpc != null ? existingNpc.gameObject : null;
        }

        if (npcRoot == null || blacksmith == null)
        {
            Debug.LogError("[Dialogue Setup] __NPC/city_dwellers_1 non trovato nella HubSceneV1.");
            return false;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Fabbro Dialogue");

        DialogueAssetBuilder.CreateBlacksmithExampleAssets();

        DialogueSpeakerData playerSpeaker = AssetDatabase.LoadAssetAtPath<DialogueSpeakerData>(PlayerSpeakerPath);
        DialogueSpeakerData blacksmithSpeaker = AssetDatabase.LoadAssetAtPath<DialogueSpeakerData>(BlacksmithSpeakerPath);
        DialogueProfile blacksmithProfile = AssetDatabase.LoadAssetAtPath<DialogueProfile>(BlacksmithProfilePath);
        if (playerSpeaker == null || blacksmithSpeaker == null || blacksmithProfile == null)
            throw new InvalidOperationException("Gli asset Dialogue del Fabbro non sono stati generati correttamente.");

        GameObject systemRoot = FindSceneObject(scene, "__SYSTEM");
        GameObject uiSceneRoot = FindSceneObject(scene, "__UI") ?? FindSceneObject(scene, "_UI");
        if (systemRoot == null || uiSceneRoot == null)
            throw new InvalidOperationException("I root __SYSTEM e __UI non sono presenti nella HubSceneV1.");

        DialogueAssetBuilder.EnsureDialogueSceneObjects(
            scene,
            systemRoot.transform,
            uiSceneRoot.transform,
            out DialogueManager manager,
            out DialogueUI dialogueUi);

        ConfigureManager(manager, playerSpeaker, dialogueUi);
        ConfigureBlacksmith(blacksmith, blacksmithSpeaker, blacksmithProfile);
        EnsurePlayerInteractionLayer(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Unity non e riuscito a salvare HubSceneV1.");

        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);

        Selection.activeGameObject = blacksmith;
        EditorGUIUtility.PingObject(blacksmith);
        if (showCompletionLog)
        {
            Debug.Log(
                "[Dialogue Setup] Fabbro configurato: layer Interactable, trigger collider, " +
                "DialogueActor, NPCInteractable, profilo Eldar, DialogueManager sotto __SYSTEM e DialogueUI sotto __UI.",
                blacksmith);
        }

        return true;
    }

    private static void ConfigureManager(
        DialogueManager manager,
        DialogueSpeakerData playerSpeaker,
        DialogueUI dialogueUi)
    {
        if (manager == null)
            throw new InvalidOperationException("DialogueManager non disponibile nella scena.");

        Undo.RecordObject(manager, "Configure Dialogue Manager");
        var serializedManager = new SerializedObject(manager);
        SerializedProperty playerSpeakerProperty = serializedManager.FindProperty("playerSpeaker");
        if (playerSpeakerProperty == null)
            throw new MissingFieldException(nameof(DialogueManager), "playerSpeaker");

        playerSpeakerProperty.objectReferenceValue = playerSpeaker;
        SetObjectReference(serializedManager, "dialogueUI", dialogueUi);
        serializedManager.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
    }

    private static void ConfigureBlacksmith(
        GameObject blacksmith,
        DialogueSpeakerData speaker,
        DialogueProfile profile)
    {
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer < 0)
            throw new InvalidOperationException("Il layer 'Interactable' non esiste nel progetto.");

        Undo.RecordObject(blacksmith, "Set Blacksmith Interaction Layer");
        blacksmith.layer = interactableLayer;
        EditorUtility.SetDirty(blacksmith);
        PrefabUtility.RecordPrefabInstancePropertyModifications(blacksmith);

        CapsuleCollider interactionCollider = blacksmith.GetComponent<CapsuleCollider>();
        if (interactionCollider == null)
            interactionCollider = Undo.AddComponent<CapsuleCollider>(blacksmith);
        ConfigureInteractionCollider(blacksmith, interactionCollider);

        DialogueActor actor = blacksmith.GetComponent<DialogueActor>();
        if (actor == null)
            actor = Undo.AddComponent<DialogueActor>(blacksmith);

        Animator animator = blacksmith.GetComponentInChildren<Animator>(true);
        Undo.RecordObject(actor, "Configure Blacksmith Dialogue Actor");
        var serializedActor = new SerializedObject(actor);
        SetObjectReference(serializedActor, "speaker", speaker);
        SetObjectReference(serializedActor, "animator", animator);
        SetObjectReference(serializedActor, "focusTransform", blacksmith.transform);
        serializedActor.ApplyModifiedProperties();
        EditorUtility.SetDirty(actor);
        PrefabUtility.RecordPrefabInstancePropertyModifications(actor);

        NPCInteractable interactable = blacksmith.GetComponent<NPCInteractable>();
        if (interactable == null)
            interactable = Undo.AddComponent<NPCInteractable>(blacksmith);

        Undo.RecordObject(interactable, "Configure Blacksmith Interaction");
        var serializedInteractable = new SerializedObject(interactable);
        SetString(serializedInteractable, "prompt", "Parla con Eldar");
        SetObjectReference(serializedInteractable, "npcProfile", AssetDatabase.LoadAssetAtPath<NpcProfile>("Assets/_Project/NPC/Blacksmith_Eldar/NpcProfile_Eldar.asset"));
        SetObjectReference(serializedInteractable, "mainSpeakerActor", actor);
        SetObjectReference(serializedInteractable, "lookTarget", blacksmith.transform);
        SetBool(serializedInteractable, "rotatePlayerTowardsNpc", true);
        SetBool(serializedInteractable, "rotateNpcTowardsPlayer", true);
        SetFloat(serializedInteractable, "rotationSpeed", 240f);
        SetBool(serializedInteractable, "allowCancel", true);
        serializedInteractable.ApplyModifiedProperties();
        EditorUtility.SetDirty(interactable);
        PrefabUtility.RecordPrefabInstancePropertyModifications(interactable);
    }

    private static void ConfigureInteractionCollider(GameObject blacksmith, CapsuleCollider collider)
    {
        Undo.RecordObject(collider, "Configure Blacksmith Interaction Collider");
        collider.isTrigger = true;
        collider.direction = 1;

        Renderer[] renderers = blacksmith.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            collider.center = new Vector3(0f, 1f, 0f);
            collider.height = 2f;
            collider.radius = 0.45f;
        }
        else
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 scale = blacksmith.transform.lossyScale;
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
            float scaleXZ = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            float localHeight = bounds.size.y / scaleY;
            float localRadius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f / scaleXZ;

            collider.center = blacksmith.transform.InverseTransformPoint(bounds.center);
            collider.height = Mathf.Max(0.5f, localHeight);
            collider.radius = Mathf.Clamp(localRadius, 0.2f, collider.height * 0.5f);
        }

        EditorUtility.SetDirty(collider);
        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
    }

    private static void EnsurePlayerInteractionLayer(Scene scene)
    {
        PlayerInteraction interaction = FindComponentInScene<PlayerInteraction>(scene);
        if (interaction == null)
            throw new InvalidOperationException("PlayerInteraction non trovato nella HubSceneV1.");

        int layer = LayerMask.NameToLayer("Interactable");
        int requiredMask = 1 << layer;
        if ((interaction.interactLayer.value & requiredMask) != 0)
            return;

        Undo.RecordObject(interaction, "Include Interactable Layer");
        interaction.interactLayer = interaction.interactLayer.value | requiredMask;
        EditorUtility.SetDirty(interaction);
        PrefabUtility.RecordPrefabInstancePropertyModifications(interaction);
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName)
                return roots[i];

            Transform child = FindDescendant(roots[i].transform, objectName);
            if (child != null)
                return child.gameObject;
        }

        return null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindDescendant(root.GetChild(i), objectName);
            if (match != null)
                return match;
        }

        return null;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingFieldException(serializedObject.targetObject.GetType().Name, propertyName);
        property.objectReferenceValue = value;
    }

    private static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingFieldException(serializedObject.targetObject.GetType().Name, propertyName);
        property.stringValue = value;
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingFieldException(serializedObject.targetObject.GetType().Name, propertyName);
        property.boolValue = value;
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingFieldException(serializedObject.targetObject.GetType().Name, propertyName);
        property.floatValue = value;
    }

}
