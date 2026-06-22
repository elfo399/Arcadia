using System;

public struct QuestEvent
{
    public QuestObjectiveEventType Type;
    public string TargetId;
    public string TargetTag;
    public int Amount;

    public QuestEvent(QuestObjectiveEventType type, string targetId, string targetTag, int amount)
    {
        Type = type;
        TargetId = targetId ?? string.Empty;
        TargetTag = targetTag ?? string.Empty;
        Amount = amount <= 0 ? 1 : amount;
    }
}

public static class QuestEvents
{
    public static event Action<QuestEvent> Raised;

    public static void Raise(QuestObjectiveEventType type, string targetId = "", string targetTag = "", int amount = 1)
    {
        if (type == QuestObjectiveEventType.None)
            return;

        Raised?.Invoke(new QuestEvent(type, targetId, targetTag, amount));
    }
}
