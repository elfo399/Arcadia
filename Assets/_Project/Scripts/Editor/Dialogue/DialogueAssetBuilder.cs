using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DialogueAssetBuilder
{
    private const string DialogueAssetRoot = "Assets/_Project/Dialogue";
    private const string DialogueSpeakersFolder = DialogueAssetRoot + "/Speakers";
    private const string BlacksmithFolder = "Assets/_Project/NPC/Blacksmith_Eldar/Dialogues";
    private const string DialogueChoicePrefabPath = "Assets/_Project/Prefabs/UI/choise.prefab";
    private const string DefaultBlacksmithPortraitPath =
        "Assets/_Project/Art/Sprites/Test/Super Asset Bundle #2 - Adventure Time v1.5/Updated Paper Book/Sprites/Content/2 Icons/20.png";

    private const string PlayerSpeakerPath = DialogueSpeakersFolder + "/Speaker_Player.asset";
    private const string BlacksmithSpeakerPath = "Assets/_Project/NPC/Blacksmith_Eldar/NpcProfile_Eldar.asset";
    private const string IntroConversationPath = BlacksmithFolder + "/Dialogue_Eldar_Introduction.asset";
    private const string DefaultConversationPath = BlacksmithFolder + "/Dialogue_Eldar_Default.asset";
    private const string LoreConversationPath = BlacksmithFolder + "/Dialogue_Eldar_Lore.asset";
    private const string BlacksmithProfilePath = BlacksmithFolder + "/Profile_Eldar.asset";

    [MenuItem("Arcadia/Dialogue/Create Dialogue Scene Objects", priority = 100)]
    public static void CreateDialogueSceneObjects()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject systemRoot = FindSceneObject(scene, "__SYSTEM");
        GameObject uiRoot = FindSceneObject(scene, "__UI") ?? FindSceneObject(scene, "_UI");
        if (!scene.IsValid() || !scene.isLoaded || systemRoot == null || uiRoot == null)
        {
            Debug.LogWarning("[Dialogue Builder] La scena attiva deve contenere i root __SYSTEM e __UI.");
            return;
        }

        EnsureDialogueSceneObjects(
            scene,
            systemRoot.transform,
            uiRoot.transform,
            out DialogueManager manager,
            out DialogueUI dialogueUi);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = dialogueUi.gameObject;
        EditorGUIUtility.PingObject(dialogueUi.gameObject);
        Debug.Log(
            "[Dialogue Builder] DialogueManager e DialogueUI creati/configurati come normali oggetti della scena.",
            manager);
    }

    public static void EnsureDialogueSceneObjects(
        Scene scene,
        Transform systemParent,
        Transform uiParent,
        out DialogueManager manager,
        out DialogueUI dialogueUi)
    {
        Button choicePrefabButton = ConfigureDialogueChoicePrefab();
        RemoveMissingDialoguePrefabChild(systemParent, "DialogueManager");
        RemoveMissingDialoguePrefabChild(uiParent, "DialogueUI");

        manager = FindComponentInScene<DialogueManager>(scene);
        dialogueUi = FindComponentInScene<DialogueUI>(scene);

        if (dialogueUi != null && !IsDialogueUiConfigured(dialogueUi))
        {
            Undo.DestroyObjectImmediate(dialogueUi.gameObject);
            dialogueUi = null;
        }

        UnpackIfPrefabInstance(manager != null ? manager.gameObject : null);
        UnpackIfPrefabInstance(dialogueUi != null ? dialogueUi.gameObject : null);

        if (manager == null)
        {
            GameObject managerObject = BuildDialogueManagerHierarchy(scene);
            Undo.RegisterCreatedObjectUndo(managerObject, "Create Dialogue Manager");
            manager = managerObject.GetComponent<DialogueManager>();
        }

        if (dialogueUi == null)
        {
            GameObject uiObject = BuildDialogueUiHierarchy(scene);
            Undo.RegisterCreatedObjectUndo(uiObject, "Create Dialogue UI");
            dialogueUi = uiObject.GetComponent<DialogueUI>();
        }

        manager.gameObject.name = "DialogueManager";
        dialogueUi.gameObject.name = "DialogueUI";
        ParentAndReset(manager.transform, systemParent, "Parent Dialogue Manager");
        ParentAndReset(dialogueUi.transform, uiParent, "Parent Dialogue UI");
        ConfigureDialogueChoiceLayout(dialogueUi);

        SerializedObject managerObjectSerialized = new SerializedObject(manager);
        SetObjectReference(managerObjectSerialized, "dialogueUI", dialogueUi);
        managerObjectSerialized.ApplyModifiedPropertiesWithoutUndo();

        if (choicePrefabButton != null)
        {
            SerializedObject serializedUi = new SerializedObject(dialogueUi);
            SetObjectReference(serializedUi, "choiceButtonPrefab", choicePrefabButton);
            serializedUi.ApplyModifiedPropertiesWithoutUndo();
            RemoveLegacySceneChoiceTemplate(dialogueUi, choicePrefabButton);
        }

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(dialogueUi);
    }

    public static Button ConfigureDialogueChoicePrefab()
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DialogueChoicePrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogWarning($"[Dialogue Builder] Prefab choice non trovato: {DialogueChoicePrefabPath}");
            return null;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(DialogueChoicePrefabPath);
        try
        {
            QuestItemUI questItemUi = root.GetComponent<QuestItemUI>();
            if (questItemUi != null)
                UnityEngine.Object.DestroyImmediate(questItemUi);

            Button button = root.GetComponent<Button>();
            if (button == null)
                button = root.AddComponent<Button>();

            const float choiceHeight = 48f;
            RectTransform rootRect = root.GetComponent<RectTransform>();
            if (rootRect != null)
                rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, choiceHeight);

            LayoutElement choiceLayout = root.GetComponent<LayoutElement>();
            if (choiceLayout == null)
                choiceLayout = root.AddComponent<LayoutElement>();
            choiceLayout.minHeight = choiceHeight;
            choiceLayout.preferredHeight = choiceHeight;
            choiceLayout.flexibleHeight = 0f;

            DialogueChoiceUI choiceUi = root.GetComponent<DialogueChoiceUI>();
            if (choiceUi == null)
                choiceUi = root.AddComponent<DialogueChoiceUI>();

            TMP_Text title = FindDescendant(root.transform, "Title")?.GetComponent<TMP_Text>();
            GameObject heardIndicator = FindDescendant(root.transform, "Reward")?.gameObject;
            Image selectionOverlay = FindDescendant(root.transform, "SelectionOverlay")?.GetComponent<Image>();
            if (title != null)
            {
                title.fontSize = 20f;
                title.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 40f);
            }

            string[] frameNames = { "Image", "Image (1)", "Image (2)" };
            for (int i = 0; i < frameNames.Length; i++)
            {
                RectTransform frame = FindDescendant(root.transform, frameNames[i]) as RectTransform;
                if (frame != null)
                    frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, choiceHeight);
            }

            RectTransform heardIndicatorRect = heardIndicator != null
                ? heardIndicator.transform as RectTransform
                : null;
            if (heardIndicatorRect != null)
            {
                heardIndicatorRect.sizeDelta = new Vector2(20f, 20f);
                heardIndicatorRect.anchoredPosition = new Vector2(-12f, heardIndicatorRect.anchoredPosition.y);
            }
            SerializedObject serializedChoice = new SerializedObject(choiceUi);
            SetObjectReference(serializedChoice, "choiceText", title);
            SetObjectReference(serializedChoice, "heardIndicator", heardIndicator);
            SetObjectReference(serializedChoice, "backgroundImage", selectionOverlay);
            serializedChoice.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, DialogueChoicePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DialogueChoicePrefabPath);
        return prefabAsset != null ? prefabAsset.GetComponent<Button>() : null;
    }

    private static void RemoveLegacySceneChoiceTemplate(DialogueUI dialogueUi, Button externalPrefabButton)
    {
        if (dialogueUi == null || externalPrefabButton == null)
            return;

        Transform template = FindDescendant(dialogueUi.transform, "ChoiceButtonTemplate");
        if (template != null)
            Undo.DestroyObjectImmediate(template.gameObject);
    }

    private static void ConfigureDialogueChoiceLayout(DialogueUI dialogueUi)
    {
        if (dialogueUi == null)
            return;

        Transform scrollViewTransform = FindDescendant(dialogueUi.transform, "ChoicesScrollView");
        RectTransform scrollView = scrollViewTransform as RectTransform;
        if (scrollView != null)
        {
            Undo.RecordObject(scrollView, "Resize Dialogue Choices");
            scrollView.anchorMin = new Vector2(1f, 0f);
            scrollView.anchorMax = new Vector2(1f, 0f);
            scrollView.pivot = new Vector2(1f, 0f);
            scrollView.anchoredPosition = new Vector2(-36f, 20f);
            scrollView.sizeDelta = new Vector2(720f, 240f);
            EditorUtility.SetDirty(scrollView);

            ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
            Transform viewportTransform = FindDescendant(scrollView, "Viewport");
            ConfigureChoicesScrollbar(
                scrollRect,
                viewportTransform as RectTransform,
                registerUndo: true);
        }

        Transform choicesRootTransform = FindDescendant(dialogueUi.transform, "ChoicesRoot");
        VerticalLayoutGroup choicesLayout = choicesRootTransform != null
            ? choicesRootTransform.GetComponent<VerticalLayoutGroup>()
            : null;
        if (choicesLayout != null)
        {
            Undo.RecordObject(choicesLayout, "Add Dialogue Choice Margins");
            choicesLayout.padding = new RectOffset(18, 18, 8, 8);
            choicesLayout.spacing = 8f;
            EditorUtility.SetDirty(choicesLayout);
        }
    }

    private static void ConfigureChoicesScrollbar(
        ScrollRect scrollRect,
        RectTransform viewport,
        bool registerUndo)
    {
        if (scrollRect == null || viewport == null)
            return;

        SetStretch(viewport, right: 22f);

        Transform scrollbarTransform = FindDescendant(scrollRect.transform, "Scrollbar Vertical");
        GameObject scrollbarObject;
        if (scrollbarTransform == null)
        {
            scrollbarObject = CreateUiObject("Scrollbar Vertical", scrollRect.transform);
            if (registerUndo)
                Undo.RegisterCreatedObjectUndo(scrollbarObject, "Create Dialogue Scrollbar");
        }
        else
        {
            scrollbarObject = scrollbarTransform.gameObject;
        }

        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(0.5f, 0.5f);
        scrollbarRect.anchoredPosition = new Vector2(-9f, 0f);
        scrollbarRect.sizeDelta = new Vector2(14f, -8f);

        Image trackImage = scrollbarObject.GetComponent<Image>();
        if (trackImage == null)
            trackImage = scrollbarObject.AddComponent<Image>();
        trackImage.color = new Color(0.08f, 0.055f, 0.04f, 0.82f);

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        if (scrollbar == null)
            scrollbar = scrollbarObject.AddComponent<Scrollbar>();

        Transform slidingAreaTransform = FindDescendant(scrollbarObject.transform, "Sliding Area");
        GameObject slidingArea = slidingAreaTransform != null
            ? slidingAreaTransform.gameObject
            : CreateUiObject("Sliding Area", scrollbarObject.transform);
        RectTransform slidingAreaRect = slidingArea.GetComponent<RectTransform>();
        SetStretch(slidingAreaRect, 2f, 2f, 2f, 2f);

        Transform handleTransform = FindDescendant(slidingArea.transform, "Handle");
        GameObject handle = handleTransform != null
            ? handleTransform.gameObject
            : CreateUiObject("Handle", slidingArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        SetStretch(handleRect);

        Image handleImage = handle.GetComponent<Image>();
        if (handleImage == null)
            handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(1f, 0.85f, 0.2f, 1f);

        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.verticalScrollbarSpacing = 4f;

        EditorUtility.SetDirty(viewport);
        EditorUtility.SetDirty(scrollbar);
        EditorUtility.SetDirty(scrollRect);
    }

    private static bool IsDialogueUiConfigured(DialogueUI dialogueUi)
    {
        if (dialogueUi == null || dialogueUi.transform.childCount == 0)
            return false;

        SerializedObject serializedUi = new SerializedObject(dialogueUi);
        string[] requiredReferences =
        {
            "dialogueRoot",
            "speakerNameText",
            "dialogueBodyText",
            "choicesRoot",
            "choiceButtonPrefab"
        };

        for (int i = 0; i < requiredReferences.Length; i++)
        {
            SerializedProperty reference = serializedUi.FindProperty(requiredReferences[i]);
            if (reference == null || reference.objectReferenceValue == null)
                return false;
        }

        return true;
    }

    private static void RemoveMissingDialoguePrefabChild(Transform parent, string objectName)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            bool isMissingPrefab = PrefabUtility.IsPrefabAssetMissing(child) ||
                                   child.name.IndexOf("(Missing Prefab", StringComparison.Ordinal) >= 0;
            if (child.name.StartsWith(objectName, StringComparison.Ordinal) && isMissingPrefab)
                Undo.DestroyObjectImmediate(child);
        }
    }

    [MenuItem("Arcadia/Dialogue/Create Blacksmith Assets", priority = 110)]
    public static void CreateBlacksmithExampleAssets()
    {
        EnsureAssetFolder(DialogueSpeakersFolder);
        EnsureAssetFolder(BlacksmithFolder);
        var createdAssets = new List<UnityEngine.Object>();

        DialogueSpeakerData playerSpeaker = LoadOrCreateAsset<DialogueSpeakerData>(
            PlayerSpeakerPath,
            asset =>
            {
                asset.speakerId = "player";
                asset.displayName = string.Empty;
                asset.portrait = null;
                asset.isPlayer = true;
            },
            createdAssets);

        NpcProfile blacksmithSpeaker = LoadOrCreateAsset<NpcProfile>(
            BlacksmithSpeakerPath,
            asset =>
            {
                asset.speakerId = "blacksmith_eldar";
                asset.displayName = "Eldar";
                asset.portrait = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultBlacksmithPortraitPath);
                asset.isPlayer = false;
            },
            createdAssets);

        DialogueConversation introConversation = LoadOrCreateAsset<DialogueConversation>(
            IntroConversationPath,
            asset => ConfigureIntroductionConversation(asset, blacksmithSpeaker),
            createdAssets);

        DialogueConversation defaultConversation = LoadOrCreateAsset<DialogueConversation>(
            DefaultConversationPath,
            asset => ConfigureDefaultConversation(asset, blacksmithSpeaker),
            createdAssets);

        DialogueConversation loreConversation = LoadOrCreateAsset<DialogueConversation>(
            LoreConversationPath,
            asset => ConfigureLoreConversation(asset, blacksmithSpeaker),
            createdAssets);

        DialogueProfile profile = LoadOrCreateAsset<DialogueProfile>(
            BlacksmithProfilePath,
            asset => ConfigureBlacksmithProfile(asset, introConversation, defaultConversation),
            createdAssets);

        ApplyBlacksmithReadIndicatorPolicy(introConversation);
        ApplyBlacksmithReadIndicatorPolicy(defaultConversation);
        ApplyBlacksmithReadIndicatorPolicy(loreConversation);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        UnityEngine.Object selection = profile != null
            ? profile
            : createdAssets.Count > 0 ? createdAssets[createdAssets.Count - 1] : playerSpeaker;
        if (selection != null)
        {
            Selection.activeObject = selection;
            EditorGUIUtility.PingObject(selection);
        }

        Debug.Log(
            createdAssets.Count > 0
                ? $"[Dialogue Builder] Creati {createdAssets.Count} asset Fabbro in {BlacksmithFolder}. " +
                  "Gli asset gia esistenti sono rimasti invariati."
                : $"[Dialogue Builder] Gli asset Fabbro esistono gia in {BlacksmithFolder}; nessun file e stato sovrascritto.",
            selection);
    }

    private static GameObject BuildDialogueManagerHierarchy(Scene previewScene)
    {
        var managerRoot = new GameObject("DialogueManager");
        if (managerRoot.scene != previewScene)
            SceneManager.MoveGameObjectToScene(managerRoot, previewScene);

        DialogueManager manager = managerRoot.AddComponent<DialogueManager>();
        AudioSource voiceSource = managerRoot.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;

        SerializedObject managerObject = new SerializedObject(manager);
        SetObjectReference(managerObject, "dialogueUI", null);
        SetObjectReference(managerObject, "voiceAudioSource", voiceSource);
        DialogueSpeakerData playerSpeaker = FindPlayerSpeakerAsset();
        if (playerSpeaker != null)
            SetObjectReference(managerObject, "playerSpeaker", playerSpeaker);
        managerObject.ApplyModifiedPropertiesWithoutUndo();

        return managerRoot;
    }

    private static GameObject BuildDialogueUiHierarchy(Scene previewScene)
    {
        var uiRoot = new GameObject("DialogueUI", typeof(RectTransform));
        if (uiRoot.scene != previewScene)
            SceneManager.MoveGameObjectToScene(uiRoot, previewScene);

        Canvas canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        uiRoot.AddComponent<GraphicRaycaster>();
        DialogueUI dialogueUi = uiRoot.AddComponent<DialogueUI>();

        RectTransform canvasRect = uiRoot.GetComponent<RectTransform>();
        SetStretch(canvasRect);

        GameObject dialogueRoot = CreateUiObject("DialogueRoot", uiRoot.transform);
        Image panelImage = dialogueRoot.AddComponent<Image>();
        panelImage.color = new Color(0.035f, 0.045f, 0.06f, 0.96f);
        RectTransform dialogueRect = dialogueRoot.GetComponent<RectTransform>();
        dialogueRect.anchorMin = new Vector2(0f, 0f);
        dialogueRect.anchorMax = new Vector2(1f, 0f);
        dialogueRect.pivot = new Vector2(0.5f, 0f);
        dialogueRect.anchoredPosition = Vector2.zero;
        dialogueRect.sizeDelta = new Vector2(0f, 390f);

        // The portrait and line content share a layout row. DialogueUI disables
        // PortraitContainer when no sprite is available; HorizontalLayoutGroup
        // then gives its width back to LineContent automatically.
        GameObject contentRow = CreateUiObject("ContentRow", dialogueRoot.transform);
        RectTransform contentRowRect = contentRow.GetComponent<RectTransform>();
        SetStretch(contentRowRect, 24f, 24f, 158f, 20f);
        var contentLayout = contentRow.AddComponent<HorizontalLayoutGroup>();
        contentLayout.spacing = 16f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = true;
        contentLayout.childForceExpandWidth = false;

        GameObject portraitContainer = CreateUiObject("PortraitContainer", contentRow.transform);
        Image portraitBackground = portraitContainer.AddComponent<Image>();
        portraitBackground.color = new Color(1f, 1f, 1f, 0.075f);
        LayoutElement portraitLayout = portraitContainer.AddComponent<LayoutElement>();
        portraitLayout.minWidth = 144f;
        portraitLayout.preferredWidth = 144f;
        portraitLayout.flexibleWidth = 0f;

        GameObject portraitImageObject = CreateUiObject("PortraitImage", portraitContainer.transform);
        Image portraitImage = portraitImageObject.AddComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;
        SetStretch(portraitImageObject.GetComponent<RectTransform>(), 8f, 8f, 8f, 8f);

        GameObject lineContent = CreateUiObject("LineContent", contentRow.transform);
        LayoutElement lineContentElement = lineContent.AddComponent<LayoutElement>();
        lineContentElement.minWidth = 0f;
        lineContentElement.flexibleWidth = 1f;
        var lineLayout = lineContent.AddComponent<VerticalLayoutGroup>();
        lineLayout.spacing = 8f;
        lineLayout.childAlignment = TextAnchor.UpperLeft;
        lineLayout.childControlHeight = true;
        lineLayout.childControlWidth = true;
        lineLayout.childForceExpandHeight = false;
        lineLayout.childForceExpandWidth = true;

        TMP_Text speakerName = CreateText(
            "SpeakerName",
            lineContent.transform,
            "Speaker",
            30f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        LayoutElement speakerLayout = speakerName.gameObject.AddComponent<LayoutElement>();
        speakerLayout.minHeight = 42f;
        speakerLayout.preferredHeight = 42f;
        speakerLayout.flexibleHeight = 0f;

        TMP_Text dialogueText = CreateText(
            "DialogueText",
            lineContent.transform,
            "Dialogue text",
            25f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        dialogueText.enableWordWrapping = true;
        dialogueText.overflowMode = TextOverflowModes.Ellipsis;
        LayoutElement dialogueTextLayout = dialogueText.gameObject.AddComponent<LayoutElement>();
        dialogueTextLayout.minHeight = 90f;
        dialogueTextLayout.flexibleHeight = 1f;

        // Choices live in a masked scroll view so a long menu cannot grow into
        // ContentRow. The content itself sizes to the number of runtime rows;
        // keyboard/gamepad selection is kept visible by DialogueUI.
        GameObject choicesScrollView = CreateUiObject("ChoicesScrollView", dialogueRoot.transform);
        RectTransform choicesScrollRectTransform = choicesScrollView.GetComponent<RectTransform>();
        choicesScrollRectTransform.anchorMin = new Vector2(1f, 0f);
        choicesScrollRectTransform.anchorMax = new Vector2(1f, 0f);
        choicesScrollRectTransform.pivot = new Vector2(1f, 0f);
        choicesScrollRectTransform.anchoredPosition = new Vector2(-36f, 20f);
        choicesScrollRectTransform.sizeDelta = new Vector2(720f, 240f);

        ScrollRect choicesScrollRect = choicesScrollView.AddComponent<ScrollRect>();
        choicesScrollRect.horizontal = false;
        choicesScrollRect.vertical = true;
        choicesScrollRect.movementType = ScrollRect.MovementType.Clamped;
        choicesScrollRect.inertia = true;
        choicesScrollRect.decelerationRate = 0.135f;
        choicesScrollRect.scrollSensitivity = 36f;

        GameObject choicesViewport = CreateUiObject("Viewport", choicesScrollView.transform);
        RectTransform choicesViewportRect = choicesViewport.GetComponent<RectTransform>();
        SetStretch(choicesViewportRect);
        Image choicesViewportImage = choicesViewport.AddComponent<Image>();
        choicesViewportImage.color = Color.clear;
        choicesViewportImage.raycastTarget = true;
        choicesViewport.AddComponent<RectMask2D>();

        GameObject choicesRoot = CreateUiObject("ChoicesRoot", choicesViewport.transform);
        var choicesLayout = choicesRoot.AddComponent<VerticalLayoutGroup>();
        choicesLayout.padding = new RectOffset(18, 18, 8, 8);
        choicesLayout.spacing = 8f;
        choicesLayout.childAlignment = TextAnchor.UpperRight;
        choicesLayout.childControlHeight = false;
        choicesLayout.childControlWidth = true;
        choicesLayout.childForceExpandHeight = false;
        choicesLayout.childForceExpandWidth = true;
        RectTransform choicesRect = choicesRoot.GetComponent<RectTransform>();
        choicesRect.anchorMin = new Vector2(0f, 1f);
        choicesRect.anchorMax = new Vector2(1f, 1f);
        choicesRect.pivot = new Vector2(0.5f, 1f);
        choicesRect.anchoredPosition = Vector2.zero;
        choicesRect.sizeDelta = Vector2.zero;
        ContentSizeFitter choicesSizeFitter = choicesRoot.AddComponent<ContentSizeFitter>();
        choicesSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        choicesSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        choicesScrollRect.viewport = choicesViewportRect;
        choicesScrollRect.content = choicesRect;
        ConfigureChoicesScrollbar(choicesScrollRect, choicesViewportRect, registerUndo: false);

        GameObject choiceTemplate = CreateUiObject("ChoiceButtonTemplate", choicesRoot.transform);
        Image choiceImage = choiceTemplate.AddComponent<Image>();
        choiceImage.color = new Color(0.13f, 0.16f, 0.21f, 0.98f);
        Button choiceButton = choiceTemplate.AddComponent<Button>();
        choiceButton.targetGraphic = choiceImage;
        ColorBlock colors = choiceButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.74f, 0.84f, 1f, 1f);
        colors.selectedColor = new Color(0.74f, 0.84f, 1f, 1f);
        colors.pressedColor = new Color(0.55f, 0.7f, 0.95f, 1f);
        colors.disabledColor = new Color(0.48f, 0.48f, 0.48f, 0.7f);
        choiceButton.colors = colors;
        LayoutElement choiceLayout = choiceTemplate.AddComponent<LayoutElement>();
        choiceLayout.minHeight = 34f;
        choiceLayout.preferredHeight = 34f;
        RectTransform choiceRect = choiceTemplate.GetComponent<RectTransform>();
        choiceRect.sizeDelta = new Vector2(0f, 34f);

        TMP_Text choiceLabel = CreateText(
            "Label",
            choiceTemplate.transform,
            "Choice",
            21f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        choiceLabel.enableWordWrapping = false;
        SetStretch(choiceLabel.rectTransform, 14f, 14f, 3f, 3f);
        choiceTemplate.SetActive(false);

        TMP_Text continueIndicator = CreateText(
            "ContinueIndicator",
            dialogueRoot.transform,
            "\u25BC",
            24f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        RectTransform continueRect = continueIndicator.rectTransform;
        continueRect.anchorMin = new Vector2(1f, 0f);
        continueRect.anchorMax = new Vector2(1f, 0f);
        continueRect.pivot = new Vector2(1f, 0f);
        continueRect.anchoredPosition = new Vector2(-24f, 20f);
        continueRect.sizeDelta = new Vector2(42f, 34f);
        continueIndicator.gameObject.SetActive(false);

        SerializedObject uiObject = new SerializedObject(dialogueUi);
        SetObjectReference(uiObject, "dialogueRoot", dialogueRoot);
        SetObjectReference(uiObject, "portraitContainer", portraitContainer);
        SetObjectReference(uiObject, "portraitImage", portraitImage);
        SetObjectReference(uiObject, "speakerNameText", speakerName);
        SetObjectReference(uiObject, "dialogueBodyText", dialogueText);
        SetObjectReference(uiObject, "choicesRoot", choicesRoot.transform);
        SetObjectReference(uiObject, "choicesScrollRect", choicesScrollRect);
        SetObjectReference(uiObject, "choiceButtonPrefab", choiceButton);
        SetObjectReference(uiObject, "continueIndicator", continueIndicator.gameObject);
        uiObject.ApplyModifiedPropertiesWithoutUndo();

        dialogueRoot.SetActive(false);
        return uiRoot;
    }

    private static void ConfigureIntroductionConversation(
        DialogueConversation conversation,
        DialogueSpeakerData blacksmith)
    {
        conversation.conversationId = "blacksmith_introduction";
        conversation.startNodeId = "intro_hello";
        conversation.nodes = new List<DialogueNode>
        {
            Node(
                "intro_hello",
                blacksmith,
                "Ah, non ti ho mai visto da queste parti.",
                "intro_weapon"),
            Node(
                "intro_weapon",
                blacksmith,
                "Se hai bisogno di un'arma, sei nel posto giusto.",
                "service_menu",
                actionsOnExit: new List<DialogueAction>
                {
                    StoryFlagAction(DialogueActionType.SetStoryFlag, "met_blacksmith")
                })
        };

        conversation.nodes.AddRange(BuildServiceMenuNodes(blacksmith));
    }

    private static void ConfigureDefaultConversation(
        DialogueConversation conversation,
        DialogueSpeakerData blacksmith)
    {
        conversation.conversationId = "blacksmith_default";
        conversation.startNodeId = "service_menu";
        conversation.nodes = BuildServiceMenuNodes(blacksmith);
    }

    private static void ConfigureLoreConversation(
        DialogueConversation conversation,
        DialogueSpeakerData blacksmith)
    {
        conversation.conversationId = "blacksmith_lore";
        conversation.startNodeId = "service_menu";
        conversation.nodes = new List<DialogueNode>
        {
            new DialogueNode
            {
                nodeId = "service_menu",
                speaker = blacksmith,
                text = "Di cosa vuoi parlare?",
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        choiceId = "tower_lore",
                        text = "Parlami della torre.",
                        nextNodeId = "lore_tower",
                        returnNodeId = "service_menu",
                        playerSpeaksChoice = true,
                        showReadIndicator = true
                    },
                    CreateAncientRunesChoice(),
                    new DialogueChoice
                    {
                        choiceId = "exit",
                        text = "Esci.",
                        playerSpeaksChoice = false
                    }
                }
            },
            Node(
                "lore_tower",
                blacksmith,
                "La vecchia torre veglia sulla valle da prima che io nascessi.",
                "lore_tower_end"),
            Node(
                "lore_tower_end",
                blacksmith,
                "Le sue pietre ricordano cose che gli uomini farebbero bene a dimenticare."),
            Node(
                "ancient_runes",
                blacksmith,
                "Quelle rune non decorano il metallo: gli impongono di ricordare la magia."),
        };
    }

    private static void ConfigureBlacksmithProfile(
        DialogueProfile profile,
        DialogueConversation introConversation,
        DialogueConversation defaultConversation)
    {
        profile.rules = new List<DialogueProfileRule>();
        if (introConversation != null)
        {
            profile.rules.Add(new DialogueProfileRule
            {
                ruleId = "first_meeting",
                priority = 80,
                conversation = introConversation,
                conditions = new DialogueConditionGroup
                {
                    logic = DialogueLogicalOperator.And,
                    conditions = new List<DialogueCondition>
                    {
                        new DialogueCondition
                        {
                            type = DialogueConditionType.StoryFlag,
                            id = "met_blacksmith",
                            expected = true,
                            negate = true
                        }
                    }
                }
            });
        }

        profile.fallbackConversation = defaultConversation;
    }

    private static List<DialogueNode> BuildServiceMenuNodes(DialogueSpeakerData blacksmith)
    {
        return new List<DialogueNode>
        {
            new DialogueNode
            {
                nodeId = "service_menu",
                speaker = blacksmith,
                text = "Che posso fare per te?",
                choices = new List<DialogueChoice>
                {
                    ServiceChoice("upgrade", "Potenzia equipaggiamento", "blacksmith_upgrade"),
                    ServiceChoice("buy", "Compra", "merchant_buy"),
                    new DialogueChoice
                    {
                        choiceId = "talk",
                        text = "Parla.",
                        playerSpokenText = "Parlami di piu della fucina.",
                        nextNodeId = "lore_start",
                        returnNodeId = "service_menu",
                        playerSpeaksChoice = true,
                        showReadIndicator = false
                    },
                    CreateAncientRunesChoice(),
                    new DialogueChoice
                    {
                        choiceId = "accept_dark_power",
                        text = "Accetto il potere oscuro.",
                        nextNodeId = "dark_response",
                        returnNodeId = "service_menu",
                        playerSpeaksChoice = true,
                        showReadIndicator = false,
                        actions = new List<DialogueAction>
                        {
                            AmountAction(DialogueActionType.ModifyKarma, -7),
                            StoryFlagAction(DialogueActionType.SetStoryFlag, "accepted_dark_power")
                        }
                    },
                    new DialogueChoice
                    {
                        choiceId = "exit",
                        text = "Esci.",
                        playerSpeaksChoice = false
                    }
                }
            },
            Node(
                "lore_start",
                blacksmith,
                "La torre a nord era una fucina, molto prima che diventasse una rovina.",
                "lore_end"),
            Node(
                "lore_end",
                blacksmith,
                "Se trovi un martello con il marchio del sole, riportamelo."),
            Node(
                "ancient_runes",
                blacksmith,
                "Hai occhio. Quelle rune legano il ricordo del fuoco alla lama."),
            Node(
                "dark_response",
                blacksmith,
                "Allora porta il peso della tua scelta. Il ferro non dimentica."),
        };
    }

    private static DialogueChoice CreateAncientRunesChoice()
    {
        return new DialogueChoice
        {
            choiceId = "ancient_runes",
            text = "[Intelligence 20] Parlami delle rune antiche.",
            nextNodeId = "ancient_runes",
            returnNodeId = "service_menu",
            playerSpeaksChoice = true,
            unavailableDisplay = DialogueUnavailableChoiceDisplay.Disabled,
            showReadIndicator = true,
            conditions = new DialogueConditionGroup
            {
                logic = DialogueLogicalOperator.And,
                conditions = new List<DialogueCondition>
                {
                    new DialogueCondition
                    {
                        type = DialogueConditionType.PlayerAttribute,
                        playerAttribute = DialoguePlayerAttribute.Intelligence,
                        comparison = DialogueComparisonOperator.GreaterOrEqual,
                        value = 20
                    }
                }
            }
        };
    }

    private static DialogueChoice ServiceChoice(string choiceId, string text, string serviceId)
    {
        return new DialogueChoice
        {
            choiceId = choiceId,
            text = text,
            playerSpeaksChoice = false,
            showReadIndicator = false,
            actions = new List<DialogueAction>
            {
                new DialogueAction
                {
                    type = DialogueActionType.OpenService,
                    serviceId = serviceId,
                    stopOnFailure = true
                }
            }
        };
    }

    private static void ApplyBlacksmithReadIndicatorPolicy(DialogueConversation conversation)
    {
        if (conversation == null || conversation.nodes == null)
            return;

        bool changed = false;
        for (int nodeIndex = 0; nodeIndex < conversation.nodes.Count; nodeIndex++)
        {
            DialogueNode node = conversation.nodes[nodeIndex];
            if (node == null || node.choices == null)
                continue;

            for (int choiceIndex = 0; choiceIndex < node.choices.Count; choiceIndex++)
            {
                DialogueChoice choice = node.choices[choiceIndex];
                if (choice == null)
                    continue;

                // Explicit example policy: only these authored lore topics opt in.
                bool shouldShow = choice.choiceId == "tower_lore" || choice.choiceId == "ancient_runes";
                if (choice.showReadIndicator == shouldShow)
                    continue;

                choice.showReadIndicator = shouldShow;
                changed = true;
            }
        }

        if (changed)
            EditorUtility.SetDirty(conversation);
    }

    private static DialogueNode Node(
        string nodeId,
        DialogueSpeakerData speaker,
        string text,
        string nextNodeId = "",
        List<DialogueAction> actionsOnExit = null)
    {
        return new DialogueNode
        {
            nodeId = nodeId,
            speaker = speaker,
            text = text,
            nextNodeId = nextNodeId,
            actionsOnExit = actionsOnExit ?? new List<DialogueAction>()
        };
    }

    private static DialogueAction AmountAction(DialogueActionType type, int amount)
    {
        return new DialogueAction
        {
            type = type,
            amount = amount
        };
    }

    private static DialogueAction StoryFlagAction(DialogueActionType type, string id)
    {
        return new DialogueAction
        {
            type = type,
            id = id
        };
    }

    private static T LoadOrCreateAsset<T>(
        string path,
        Action<T> configure,
        List<UnityEngine.Object> createdAssets)
        where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
            return existing;

        UnityEngine.Object occupiedAsset = AssetDatabase.LoadMainAssetAtPath(path);
        if (occupiedAsset != null)
        {
            Debug.LogError(
                $"[Dialogue Builder] '{path}' esiste ma non e un {typeof(T).Name}; il file e stato lasciato invariato.",
                occupiedAsset);
            return null;
        }

        T asset = ScriptableObject.CreateInstance<T>();
        asset.name = Path.GetFileNameWithoutExtension(path);
        configure?.Invoke(asset);
        AssetDatabase.CreateAsset(asset, path);
        createdAssets.Add(asset);
        return asset;
    }

    private static DialogueSpeakerData FindPlayerSpeakerAsset()
    {
        DialogueSpeakerData knownSpeaker = AssetDatabase.LoadAssetAtPath<DialogueSpeakerData>(PlayerSpeakerPath);
        if (knownSpeaker != null && knownSpeaker.isPlayer)
            return knownSpeaker;

        string[] guids = AssetDatabase.FindAssets("t:DialogueSpeakerData");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            DialogueSpeakerData speaker = AssetDatabase.LoadAssetAtPath<DialogueSpeakerData>(path);
            if (speaker != null && speaker.isPlayer)
                return speaker;
        }

        return null;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(name, parent);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont != null)
            text.font = defaultFont;

        return text;
    }

    private static void SetObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(
                $"Campo serialized '{propertyName}' non trovato su {serializedObject.targetObject.GetType().Name}.");

        property.objectReferenceValue = value;
    }

    private static void SetStretch(
        RectTransform rectTransform,
        float left = 0f,
        float right = 0f,
        float bottom = 0f,
        float top = 0f)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
    }

    private static void UnpackIfPrefabInstance(GameObject instanceObject)
    {
        if (instanceObject == null || !PrefabUtility.IsPartOfPrefabInstance(instanceObject))
            return;

        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(instanceObject);
        if (root != null)
            PrefabUtility.UnpackPrefabInstance(
                root,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
    }

    private static void ParentAndReset(Transform child, Transform parent, string undoName)
    {
        if (child.parent != parent)
            Undo.SetTransformParent(child, parent, undoName);

        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform match = FindDescendant(roots[i].transform, objectName);
            if (match != null)
                return match.gameObject;
        }

        return null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
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

    private static void EnsureAssetFolder(string folderPath)
    {
        string normalized = folderPath.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(normalized))
            return;

        string[] segments = normalized.Split('/');
        if (segments.Length == 0 || segments[0] != "Assets")
            throw new ArgumentException("Il path deve iniziare con 'Assets'.", nameof(folderPath));

        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }
}
