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
    private const string DialoguePrefabFolder = DialogueAssetRoot + "/Prefabs";
    private const string DialogueExampleFolder = DialogueAssetRoot + "/Examples";
    private const string DialoguePrefabPath = DialoguePrefabFolder + "/DialogueSystem.prefab";

    private const string PlayerSpeakerPath = DialogueExampleFolder + "/Speaker_Player.asset";
    private const string BlacksmithSpeakerPath = DialogueExampleFolder + "/Speaker_Blacksmith.asset";
    private const string IntroConversationPath = DialogueExampleFolder + "/Dialogue_Blacksmith_Introduction.asset";
    private const string DefaultConversationPath = DialogueExampleFolder + "/Dialogue_Blacksmith_Default.asset";
    private const string LoreConversationPath = DialogueExampleFolder + "/Dialogue_Blacksmith_Lore.asset";
    private const string BlacksmithProfilePath = DialogueExampleFolder + "/DialogueProfile_Blacksmith.asset";

    [MenuItem("Arcadia/Dialogue/Create UI Prefab", priority = 100)]
    public static void CreateDialogueUiPrefab()
    {
        EnsureAssetFolder(DialoguePrefabFolder);

        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePrefabPath);
        if (existingPrefab != null)
        {
            Selection.activeObject = existingPrefab;
            EditorGUIUtility.PingObject(existingPrefab);
            Debug.Log($"[Dialogue Builder] Prefab gia presente e lasciato invariato: {DialoguePrefabPath}", existingPrefab);
            return;
        }

        if (AssetDatabase.LoadMainAssetAtPath(DialoguePrefabPath) != null)
        {
            Debug.LogError($"[Dialogue Builder] Il path '{DialoguePrefabPath}' e occupato da un asset non compatibile.");
            return;
        }

        Scene previewScene = EditorSceneManager.NewPreviewScene();
        GameObject systemRoot = null;

        try
        {
            // Unity 2022 does not allow a PreviewScene to become the active
            // scene. BuildDialogueUiHierarchy moves its root into the preview
            // scene immediately, before any prefab content is authored.
            systemRoot = BuildDialogueUiHierarchy(previewScene);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(systemRoot, DialoguePrefabPath, out bool success);
            if (!success || savedPrefab == null)
            {
                Debug.LogError($"[Dialogue Builder] Creazione prefab fallita: {DialoguePrefabPath}");
                return;
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);
            Debug.Log(
                $"[Dialogue Builder] Creato {DialoguePrefabPath}. Le scene non sono state modificate; " +
                "trascina il prefab nella scena bootstrap e assicurati che sia presente un EventSystem.",
                savedPrefab);
        }
        finally
        {
            if (systemRoot != null)
                UnityEngine.Object.DestroyImmediate(systemRoot);

            if (previewScene.IsValid())
                EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    [MenuItem("Arcadia/Dialogue/Create Blacksmith Example Assets", priority = 110)]
    public static void CreateBlacksmithExampleAssets()
    {
        EnsureAssetFolder(DialogueExampleFolder);
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

        DialogueSpeakerData blacksmithSpeaker = LoadOrCreateAsset<DialogueSpeakerData>(
            BlacksmithSpeakerPath,
            asset =>
            {
                asset.speakerId = "blacksmith_eldar";
                asset.displayName = "Eldar";
                asset.portrait = null;
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

        LoadOrCreateAsset<DialogueConversation>(
            LoreConversationPath,
            asset => ConfigureLoreConversation(asset, blacksmithSpeaker),
            createdAssets);

        DialogueProfile profile = LoadOrCreateAsset<DialogueProfile>(
            BlacksmithProfilePath,
            asset => ConfigureBlacksmithProfile(asset, introConversation, defaultConversation),
            createdAssets);

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
                ? $"[Dialogue Builder] Creati {createdAssets.Count} asset Fabbro in {DialogueExampleFolder}. " +
                  "Gli asset gia esistenti sono rimasti invariati."
                : $"[Dialogue Builder] Gli asset Fabbro esistono gia in {DialogueExampleFolder}; nessun file e stato sovrascritto.",
            selection);
    }

    private static GameObject BuildDialogueUiHierarchy(Scene previewScene)
    {
        var systemRoot = new GameObject("DialogueSystem");
        if (systemRoot.scene != previewScene)
            SceneManager.MoveGameObjectToScene(systemRoot, previewScene);

        DialogueManager manager = systemRoot.AddComponent<DialogueManager>();
        AudioSource voiceSource = systemRoot.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;

        GameObject canvasObject = CreateUiObject("Canvas", systemRoot.transform);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        DialogueUI dialogueUi = canvasObject.AddComponent<DialogueUI>();

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        SetStretch(canvasRect);

        GameObject dialogueRoot = CreateUiObject("DialogueRoot", canvasObject.transform);
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
        choicesScrollRectTransform.anchorMin = new Vector2(0.39f, 0f);
        choicesScrollRectTransform.anchorMax = new Vector2(1f, 0f);
        choicesScrollRectTransform.pivot = new Vector2(0.5f, 0f);
        choicesScrollRectTransform.anchoredPosition = new Vector2(-12f, 20f);
        choicesScrollRectTransform.sizeDelta = new Vector2(-24f, 126f);

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
        choicesLayout.spacing = 6f;
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

        SerializedObject managerObject = new SerializedObject(manager);
        SetObjectReference(managerObject, "dialogueUI", dialogueUi);
        SetObjectReference(managerObject, "voiceAudioSource", voiceSource);

        DialogueSpeakerData playerSpeaker = FindPlayerSpeakerAsset();
        if (playerSpeaker != null)
            SetObjectReference(managerObject, "playerSpeaker", playerSpeaker);

        managerObject.ApplyModifiedPropertiesWithoutUndo();

        dialogueRoot.SetActive(false);
        return systemRoot;
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
                    ServiceChoice("buy", "Compra", "merchant"),
                    new DialogueChoice
                    {
                        choiceId = "talk",
                        text = "Parla.",
                        nextNodeId = "lore_start",
                        returnNodeId = "service_menu",
                        playerSpeaksChoice = true,
                        showReadIndicator = true
                    },
                    CreateAncientRunesChoice(),
                    new DialogueChoice
                    {
                        choiceId = "accept_dark_power",
                        text = "Accetto il potere oscuro.",
                        nextNodeId = "dark_response",
                        returnNodeId = "service_menu",
                        playerSpeaksChoice = true,
                        showReadIndicator = true,
                        actions = new List<DialogueAction>
                        {
                            AmountAction(DialogueActionType.ModifyMalefico, 5),
                            AmountAction(DialogueActionType.ModifyKarma, -2),
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
