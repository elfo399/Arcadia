using Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class SceneRuntimeReferences : MonoBehaviour
{
    public static SceneRuntimeReferences Current { get; private set; }

    [Header("Gameplay Camera")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private CinemachineFreeLook freeLookCamera;
    [SerializeField] private CinemachineVirtualCamera lockOnCamera;

    [Header("Scene UI")]
    [SerializeField] private MenuManager menuManager;

    public Camera GameplayCamera => gameplayCamera;
    public CinemachineFreeLook FreeLookCamera => freeLookCamera;
    public CinemachineVirtualCamera LockOnCamera => lockOnCamera;
    public MenuManager MenuManager => menuManager;

    private void Awake()
    {
        Current = this;
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }
}
