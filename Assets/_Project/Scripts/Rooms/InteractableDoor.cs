using UnityEngine;

/// <summary>Authored internal door. It never changes graph-door locks unless legacy mode is explicitly enabled.</summary>
public class InteractableDoor : MonoBehaviour,IInteractable
{
    [SerializeField] private string doorId;
    [SerializeField] private DungeonRequirement requirement;
    [Tooltip("Legacy entry-door compatibility only. New internal doors must leave this disabled.")]
    [SerializeField] private bool legacyUnlockWholeRoom;
    [SerializeField] private GameObject openedVisual;
    [SerializeField] private string prompt="Open";
    private Room parentRoom; private SavedDungeonRuleState state;
    private void Start(){parentRoom=GetComponentInParent<Room>();if(parentRoom!=null){state=parentRoom.GetExternalState("door:"+doorId);if(state.completed){OpenVisual();if(legacyUnlockWholeRoom)parentRoom.UnlockSpecialRoom();}}}
    public void Interact(GameObject player)
    {
        if(state!=null&&state.completed)return;PlayerStats stats=player!=null?player.GetComponentInParent<PlayerStats>():PlayerStats.instance;
        if(requirement!=null){if(!requirement.TryConsume(stats))return;}else if(legacyUnlockWholeRoom&&parentRoom!=null&&parentRoom.isLocked){if(stats==null||!stats.UseKey())return;}
        if(state!=null)state.completed=true;OpenVisual();if(legacyUnlockWholeRoom&&parentRoom!=null)parentRoom.UnlockSpecialRoom();if(parentRoom!=null)parentRoom.SaveExternalState(state);
    }
    private void OpenVisual(){if(openedVisual)openedVisual.SetActive(true);gameObject.SetActive(false);}
    public string GetPrompt()=>state!=null&&state.completed?string.Empty:prompt;
    public void SetLegacyWholeRoomUnlockForMigration(bool value){legacyUnlockWholeRoom=value;}
#if UNITY_EDITOR
    private void OnValidate(){if(string.IsNullOrWhiteSpace(doorId)){doorId="door-"+System.Guid.NewGuid().ToString("N");UnityEditor.EditorUtility.SetDirty(this);}}
#endif
}
