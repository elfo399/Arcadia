using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewQuest", menuName = "Arcadia/Quest Definition")]
public class QuestDefinition : ScriptableObject
{
    public string questId;
    public string title;
    public string location;
    public string questTypeLabel = "Main Quest";
    public string recommendedLabel = "";
    public Sprite questImage;
    public string loreTitle = "";
    [TextArea(2, 6)] public string loreDescription = "";
    public string loreAuthor = "";
    public List<QuestManager.QuestObjectiveData> objectives = new();
    public List<QuestManager.QuestRewardData> rewards = new();

    public QuestManager.QuestData CreateRuntimeData()
    {
        return new QuestManager.QuestData
        {
            questId = questId,
            title = title,
            location = location,
            completed = false,
            rewardClaimed = false,
            questTypeLabel = questTypeLabel,
            recommendedLabel = recommendedLabel,
            questImage = questImage,
            loreTitle = loreTitle,
            loreDescription = loreDescription,
            loreAuthor = loreAuthor,
            objectives = CloneObjectives(objectives),
            rewards = CloneRewards(rewards)
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(questId))
            Debug.LogWarning($"[QuestDefinition] Quest ID vuoto nell'asset '{name}'.", this);
    }
#endif

    private static List<QuestManager.QuestObjectiveData> CloneObjectives(List<QuestManager.QuestObjectiveData> source)
    {
        var result = new List<QuestManager.QuestObjectiveData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            var objective = source[i];
            if (objective == null) continue;
            result.Add(new QuestManager.QuestObjectiveData
            {
                phase = Mathf.Max(1, objective.phase),
                title = objective.title,
                description = objective.description,
                eventType = objective.eventType,
                targetObject = objective.targetObject,
                targetId = objective.targetId,
                targetTag = objective.targetTag,
                requiredAmount = Mathf.Max(1, objective.requiredAmount),
                currentAmount = Mathf.Max(0, objective.currentAmount),
                completed = objective.completed
            });
        }

        return result;
    }

    private static List<QuestManager.QuestRewardData> CloneRewards(List<QuestManager.QuestRewardData> source)
    {
        var result = new List<QuestManager.QuestRewardData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            var reward = source[i];
            if (reward == null) continue;
            result.Add(new QuestManager.QuestRewardData
            {
                rewardType = reward.rewardType,
                type = reward.type,
                amount = reward.amount,
                itemName = reward.itemName,
                weaponAsset = reward.weaponAsset,
                usableAsset = reward.usableAsset,
                itemAsset = reward.itemAsset,
                magicAsset = reward.magicAsset,
                armorAsset = reward.armorAsset
            });
        }

        return result;
    }
}
