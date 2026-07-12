using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MinimapRoomIconView : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Image overlayImage;

    public Image FillImage => fillImage;
    public Image OverlayImage => overlayImage;
}
