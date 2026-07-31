using UnityEngine.EventSystems;

public interface IInventorySlotHandler
{
    void HandleSlotPointerDown(int index);
    void HandleSlotBeginDrag(int index, PointerEventData eventData);
    void HandleSlotDrag(PointerEventData eventData);
    void HandleSlotEndDrag();
    void HandleSlotDrop(int targetIndex);
    void HandleSlotSelected(int index);
    void HandleSlotSubmit(int index);
}
