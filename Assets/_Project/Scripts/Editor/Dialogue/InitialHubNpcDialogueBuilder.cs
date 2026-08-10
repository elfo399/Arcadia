using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates authoring-only placeholder conversations for the initial Hub NPCs.
/// It deliberately does not create or configure any scene object or service.
/// </summary>
public static class InitialHubNpcDialogueBuilder
{
    private const string Root = "Assets/_Project/Dialogue/NPC";

    [MenuItem("Arcadia/Dialogue/Create Initial Hub NPC Dialogues", priority = 111)]
    public static void CreateInitialHubNpcDialogues()
    {
        CreateNpc(
            folderName: "Merchant",
            speakerFileName: "Speaker_Merchant",
            profileFileName: "Profile_Merchant",
            displayName: "Mercante",
            speakerId: "merchant",
            flagId: "met_merchant",
            conversationPrefix: "merchant",
            introGreeting: "Benvenuto. Cerchi qualcosa per il tuo viaggio?",
            introFarewell: "Dai un'occhiata alla mia merce quando vuoi.",
            serviceChoiceId: "merchant_service_Buy",
            serviceLabel: "Compra",
            serviceId: "merchant_buy",
            loreChoiceId: "merchant_lore",
            loreLabel: "Parlami del mercato.",
            loreText: "Il mercato cambia volto ogni giorno, ma una buona merce trova sempre il suo acquirente.");

        CreateNpc(
            folderName: "Mage",
            speakerFileName: "Speaker_Mage",
            profileFileName: "Profile_Mage",
            displayName: "Mago",
            speakerId: "mage",
            flagId: "met_mage",
            conversationPrefix: "mage",
            introGreeting: "Le correnti arcane ti hanno condotto fino a me.",
            introFarewell: "Quando sarai pronto, parleremo di magia.",
            serviceChoiceId: "mage_service",
            serviceLabel: "Impara magie",
            serviceId: "magic",
            loreChoiceId: "mage_lore",
            loreLabel: "Parlami delle rune.",
            loreText: "Ogni runa è una promessa: pronunciarla significa accettarne il prezzo.");

        CreateNpc(
            folderName: "Tavernkeeper",
            speakerFileName: "Speaker_Tavernkeeper",
            profileFileName: "Profile_Tavernkeeper",
            displayName: "Taverniere",
            speakerId: "tavernkeeper",
            flagId: "met_tavernkeeper",
            conversationPrefix: "tavernkeeper",
            introGreeting: "Entra pure, viandante. Qui troverai un pasto caldo.",
            introFarewell: "La porta resta aperta quando avrai bisogno di riposare.",
            serviceChoiceId: "tavern_board",
            serviceLabel: "Guarda la bacheca",
            serviceId: "tavern",
            loreChoiceId: "tavern_lore",
            loreLabel: "Raccontami della locanda.",
            loreText: "Questa locanda ha visto passare mercanti, eroi e qualche bugiardo memorabile.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Dialogue Builder] Dialoghi base Mercante, Mago e Taverniere creati.");
    }

    private static void CreateNpc(
        string folderName,
        string speakerFileName,
        string profileFileName,
        string displayName,
        string speakerId,
        string flagId,
        string conversationPrefix,
        string introGreeting,
        string introFarewell,
        string serviceChoiceId,
        string serviceLabel,
        string serviceId,
        string loreChoiceId,
        string loreLabel,
        string loreText)
    {
        string folder = Root + "/" + folderName;
        EnsureFolder(folder);

        DialogueSpeakerData speaker = LoadOrCreate(
            folder + "/" + speakerFileName + ".asset",
            () =>
            {
                DialogueSpeakerData value = ScriptableObject.CreateInstance<DialogueSpeakerData>();
                value.name = speakerFileName;
                value.speakerId = speakerId;
                value.displayName = displayName;
                value.isPlayer = false;
                return value;
            });

        DialogueConversation introduction = LoadOrCreate(
            folder + "/Dialogue_" + folderName + "_Introduction.asset",
            () => CreateIntroduction(speaker, conversationPrefix, flagId, introGreeting, introFarewell,
                serviceChoiceId, serviceLabel, serviceId, loreChoiceId, loreLabel, loreText));
        DialogueConversation fallback = LoadOrCreate(
            folder + "/Dialogue_" + folderName + "_Default.asset",
            () => CreateConversation(speaker, conversationPrefix + "_default", conversationPrefix + "_service_menu",
                serviceChoiceId, serviceLabel, serviceId, loreChoiceId, loreLabel, loreText));
        LoadOrCreate(
            folder + "/Dialogue_" + folderName + "_Lore.asset",
            () => CreateConversation(speaker, conversationPrefix + "_lore", conversationPrefix + "_service_menu",
                serviceChoiceId, serviceLabel, serviceId, loreChoiceId, loreLabel, loreText));

        LoadOrCreate(
            folder + "/" + profileFileName + ".asset",
            () =>
            {
                DialogueProfile value = ScriptableObject.CreateInstance<DialogueProfile>();
                value.name = profileFileName;
                value.rules = new List<DialogueProfileRule>
                {
                    new DialogueProfileRule
                    {
                        ruleId = "first_meeting",
                        priority = 80,
                        conversation = introduction,
                        conditions = new DialogueConditionGroup
                        {
                            logic = DialogueLogicalOperator.And,
                            conditions = new List<DialogueCondition>
                            {
                                new DialogueCondition
                                {
                                    type = DialogueConditionType.StoryFlag,
                                    id = flagId,
                                    expected = true,
                                    negate = true
                                }
                            }
                        }
                    }
                };
                value.fallbackConversation = fallback;
                return value;
            });
    }

    private static DialogueConversation CreateIntroduction(
        DialogueSpeakerData speaker,
        string prefix,
        string flagId,
        string greeting,
        string farewell,
        string serviceChoiceId,
        string serviceLabel,
        string serviceId,
        string loreChoiceId,
        string loreLabel,
        string loreText)
    {
        DialogueConversation conversation = ScriptableObject.CreateInstance<DialogueConversation>();
        conversation.name = "Dialogue_" + prefix + "_Introduction";
        conversation.conversationId = prefix + "_introduction";
        conversation.startNodeId = prefix + "_intro_greeting";
        conversation.nodes = new List<DialogueNode>
        {
            Node(prefix + "_intro_greeting", speaker, greeting, prefix + "_intro_farewell"),
            Node(prefix + "_intro_farewell", speaker, farewell, prefix + "_service_menu",
                new List<DialogueAction> { StoryFlag(flagId) })
        };
        conversation.nodes.AddRange(BuildMenu(speaker, prefix, serviceChoiceId, serviceLabel, serviceId,
            loreChoiceId, loreLabel, loreText));
        return conversation;
    }

    private static DialogueConversation CreateConversation(
        DialogueSpeakerData speaker,
        string conversationId,
        string menuNodeId,
        string serviceChoiceId,
        string serviceLabel,
        string serviceId,
        string loreChoiceId,
        string loreLabel,
        string loreText)
    {
        DialogueConversation conversation = ScriptableObject.CreateInstance<DialogueConversation>();
        conversation.name = "Dialogue_" + conversationId;
        conversation.conversationId = conversationId;
        conversation.startNodeId = menuNodeId;
        string prefix = conversationId.Substring(0, conversationId.IndexOf('_'));
        conversation.nodes = BuildMenu(speaker, prefix, serviceChoiceId, serviceLabel, serviceId,
            loreChoiceId, loreLabel, loreText);
        return conversation;
    }

    private static List<DialogueNode> BuildMenu(
        DialogueSpeakerData speaker,
        string prefix,
        string serviceChoiceId,
        string serviceLabel,
        string serviceId,
        string loreChoiceId,
        string loreLabel,
        string loreText)
    {
        string menuId = prefix + "_service_menu";
        string loreNodeId = prefix + "_lore";
        return new List<DialogueNode>
        {
            new DialogueNode
            {
                nodeId = menuId,
                speaker = speaker,
                text = "Che posso fare per te?",
                choices = BuildMenuChoices(prefix, menuId, loreNodeId, serviceChoiceId,
                    serviceLabel, serviceId, loreChoiceId, loreLabel)
            },
            Node(loreNodeId, speaker, loreText)
        };
    }

    private static List<DialogueChoice> BuildMenuChoices(
        string prefix,
        string menuId,
        string loreNodeId,
        string serviceChoiceId,
        string serviceLabel,
        string serviceId,
        string loreChoiceId,
        string loreLabel)
    {
        var choices = new List<DialogueChoice>
        {
            ServiceChoice(serviceChoiceId, serviceLabel, serviceId)
        };

        if (prefix == "merchant")
            choices.Add(ServiceChoice("merchant_service_Sell", "Vendi", "merchant_sell"));

        choices.Add(new DialogueChoice
        {
            choiceId = prefix + "_talk",
            text = "Parla.",
            playerSpokenText = "Vorrei saperne di piu.",
            nextNodeId = loreNodeId,
            returnNodeId = menuId,
            playerSpeaksChoice = true,
            showReadIndicator = false
        });
        choices.Add(new DialogueChoice
        {
            choiceId = loreChoiceId,
            text = loreLabel,
            nextNodeId = loreNodeId,
            returnNodeId = menuId,
            playerSpeaksChoice = true,
            showReadIndicator = true
        });
        choices.Add(new DialogueChoice
        {
            choiceId = prefix + "_exit",
            text = "Esci.",
            playerSpeaksChoice = false,
            showReadIndicator = false
        });
        return choices;
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

    private static DialogueNode Node(string nodeId, DialogueSpeakerData speaker, string text,
        string nextNodeId = "", List<DialogueAction> actionsOnExit = null)
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

    private static DialogueAction StoryFlag(string flagId)
    {
        return new DialogueAction { type = DialogueActionType.SetStoryFlag, id = flagId };
    }

    private static T LoadOrCreate<T>(string path, Func<T> factory) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
            return existing;

        T asset = factory();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
