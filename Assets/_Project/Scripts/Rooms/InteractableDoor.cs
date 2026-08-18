using UnityEngine;

/// <summary>Authored local door with persistent opened state.</summary>
public class InteractableDoor : MonoBehaviour,IInteractable
{
    [SerializeField] private string doorId;
    [SerializeField] private DungeonRequirement requirement;
    [SerializeField] private GameObject openedVisual;
    [SerializeField] private string prompt="Open";
    private Room parentRoom; private SavedDungeonRuleState state;
    private void Start(){parentRoom=GetComponentInParent<Room>();if(parentRoom!=null){state=parentRoom.GetExternalState("door:"+doorId);if(state.completed)OpenVisual();}}
    public void Interact(GameObject player)
    {
        if(state!=null&&state.completed)return;PlayerStats stats=player!=null?player.GetComponentInParent<PlayerStats>():PlayerStats.instance;
        if(requirement!=null&&!requirement.IsMet(stats))return;
        if(state!=null)state.completed=true;OpenVisual();if(parentRoom!=null)parentRoom.SaveExternalState(state);
    }
    private void OpenVisual(){if(openedVisual)openedVisual.SetActive(true);gameObject.SetActive(false);}
    public string GetPrompt()=>state!=null&&state.completed?string.Empty:prompt;
#if UNITY_EDITOR
    private void OnValidate(){if(string.IsNullOrWhiteSpace(doorId)){doorId="door-"+System.Guid.NewGuid().ToString("N");UnityEditor.EditorUtility.SetDirty(this);}}
#endif
}
