using System;
using System.Collections.Generic;
using UnityEngine;

public interface INpcService
{
    string ServiceId { get; }
    bool Open(NpcServiceContext context);
    void Close();
}

public sealed class NpcServiceContext
{
    public string ServiceId { get; internal set; }
    public DialogueManager DialogueManager { get; internal set; }
    public NPCInteractable Interactable { get; internal set; }
    public GameObject Player { get; internal set; }
    public PlayerStats PlayerStats { get; internal set; }
    public PlayerInventory PlayerInventory { get; internal set; }
}

public static class NpcServiceRegistry
{
    private static readonly Dictionary<string, INpcService> Services =
        new Dictionary<string, INpcService>(StringComparer.OrdinalIgnoreCase);

    public static bool Register(INpcService service)
    {
        if (service == null || string.IsNullOrWhiteSpace(service.ServiceId))
            return false;

        string id = service.ServiceId.Trim();
        if (Services.TryGetValue(id, out INpcService existing) && !IsUnavailable(existing) && !ReferenceEquals(existing, service))
        {
            Debug.LogWarning($"[NpcServiceRegistry] ServiceId duplicato '{id}'.");
            return false;
        }

        Services[id] = service;
        return true;
    }

    public static void Unregister(INpcService service)
    {
        if (service == null || string.IsNullOrWhiteSpace(service.ServiceId))
            return;

        string id = service.ServiceId.Trim();
        if (Services.TryGetValue(id, out INpcService existing) && ReferenceEquals(existing, service))
            Services.Remove(id);
    }

    public static bool HasService(string serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
            return false;

        string id = serviceId.Trim();
        if (!Services.TryGetValue(id, out INpcService service) || IsUnavailable(service))
        {
            Services.Remove(id);
            return false;
        }

        return true;
    }

    public static bool TryOpen(string serviceId, NpcServiceContext context)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
            return false;

        string id = serviceId.Trim();
        if (!Services.TryGetValue(id, out INpcService service) || IsUnavailable(service))
        {
            Services.Remove(id);
            Debug.LogWarning($"[NpcServiceRegistry] Nessun servizio registrato con ID '{id}'.");
            return false;
        }

        if (context != null)
            context.ServiceId = id;
        return service.Open(context);
    }

    private static bool IsUnavailable(INpcService service)
    {
        if (service == null)
            return true;
        return service is UnityEngine.Object unityObject && unityObject == null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        Services.Clear();
    }
}

public abstract class NpcServiceBehaviour : MonoBehaviour, INpcService
{
    [SerializeField] private string serviceId;
    public string ServiceId => serviceId;

    protected virtual void OnEnable()
    {
        NpcServiceRegistry.Register(this);
    }

    protected virtual void OnDisable()
    {
        NpcServiceRegistry.Unregister(this);
    }

    public abstract bool Open(NpcServiceContext context);
    public abstract void Close();
}
