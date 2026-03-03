using UnityEngine;

[System.Obsolete("StatBarManager e' legacy. Le barre player sono gestite da PlayerUI.", false)]
public class StatBarManager : MonoBehaviour
{
    private void Awake()
    {
        enabled = false;
    }
}
