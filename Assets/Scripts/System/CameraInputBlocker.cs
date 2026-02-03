using UnityEngine;
using Cinemachine;
using System.Collections.Generic;

public static class CameraInputBlocker
{
    private static List<CinemachineFreeLook> _freeLookCams;
    private static bool _isInitialized = false;

    /// <summary>
    /// Initializes the cache of FreeLook cameras in the scene.
    /// This is called automatically the first time input is set.
    /// </summary>
    private static void Initialize()
    {
        if (_isInitialized) return;

        RefreshCameraList();
        _isInitialized = true;
    }

    /// <summary>
    /// Refreshes the cached list so newly spawned cameras are included.
    /// </summary>
    private static void RefreshCameraList()
    {
        _freeLookCams = new List<CinemachineFreeLook>(Object.FindObjectsOfType<CinemachineFreeLook>(true));
    }

    /// <summary>
    /// Finds every single CinemachineFreeLook camera in the scene and forcibly
    /// enables or disables its input by setting the raw axis names.
    /// This acts as a global override.
    /// </summary>
    /// <param name="active">Whether the input should be active.</param>
    public static void SetAllCinemachineInput(bool active)
    {
        if (!_isInitialized)
        {
            Initialize();
        }
        else
        {
            RefreshCameraList(); // include cameras created after first call
        }

        foreach (var cam in _freeLookCams)
        {
            if (cam == null) continue;

            var inputProvider = cam.GetComponent<CinemachineInputProvider>();
            if (inputProvider != null)
            {
                inputProvider.enabled = active;
            }
            else
            {
                // Fallback for cameras that might not be using an Input Provider
                if (active)
                {
                    cam.m_XAxis.m_InputAxisName = "Mouse X";
                    cam.m_YAxis.m_InputAxisName = "Mouse Y";
                }
                else
                {
                    cam.m_XAxis.m_InputAxisName = "";
                    cam.m_YAxis.m_InputAxisName = "";
                }
            }
        }
    }
}
