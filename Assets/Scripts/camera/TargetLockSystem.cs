using UnityEngine;
using Cinemachine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class TargetLockSystem : MonoBehaviour
{
    [Header("Riferimenti")]
    public CinemachineFreeLook freeLookCamera;    
    public CinemachineVirtualCamera lockOnCamera; 
    public Transform playerModel;                 

    [Header("Ricerca")]
    public float scanRadius = 20f;    
    public LayerMask enemyLayer;      
    public float maxLockDistance = 25f; 

    [Header("Switching (NUOVO)")]
    public float switchCooldown = 0.2f;
    public float switchThreshold = 0.5f;

    [Header("Parametri Movimento")]
    public float rotationSpeed = 20f; 

    [Header("UI")]
    public RectTransform targetIcon;  

    [Header("Debug")]
    public bool isLockedOn = false;
    public Transform currentTarget;

    private Camera mainCam;
    private float lastSwitchTime;
    private PlayerController playerController;

    void Awake()
    {
        mainCam = Camera.main;
        // Cerca il PlayerController nel genitore (o dove si trova)
        playerController = GetComponentInParent<PlayerController>();
    }

    void Start()
    {
        if(playerController != null && playerController.Controls != null)
        {
            playerController.Controls.Player.LockOn.performed += HandleLockOnInput;
        }

        freeLookCamera.Priority = 10;
        lockOnCamera.Priority = 0;
        if (targetIcon != null) targetIcon.gameObject.SetActive(false);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (playerController != null && playerController.Controls != null)
        {
            playerController.Controls.Player.LockOn.performed -= HandleLockOnInput;
        }
    }

    void Update()
    {
        if (playerController != null && playerController.IsInventoryOpen) return;

        if (isLockedOn)
        {
            if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
            {
                StopLockOn();
                return;
            }

            if (Vector3.Distance(transform.position, currentTarget.position) > maxLockDistance)
            {
                StopLockOn();
                return;
            }

            HandleRotation();
            UpdateTargetUI();

            // --- NUOVO: CAMBIO BERSAGLIO ---
            HandleTargetSwitching();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        mainCam = Camera.main;
        playerController = GetComponentInParent<PlayerController>();
        targetIcon = FindTargetIcon();
        StopLockOn();
    }

    // --- LOGICA SWITCHING ---
    void HandleTargetSwitching()
    {
        if (Time.time < lastSwitchTime + switchCooldown) return;
        if (playerController == null || playerController.Controls == null) return;

        // Legge l'input della camera (Mouse Delta o Right Stick)
        Vector2 lookInput = playerController.Controls.Player.Look.ReadValue<Vector2>();

        // Se muovo forte a destra o sinistra
        if (Mathf.Abs(lookInput.x) > switchThreshold)
        {
            SwitchTarget(Mathf.Sign(lookInput.x)); // +1 Destra, -1 Sinistra
            lastSwitchTime = Time.time;
        }
    }

    void SwitchTarget(float direction)
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, scanRadius, enemyLayer);
        Transform bestCandidate = null;
        float bestScore = Mathf.Infinity;

        // Posizione schermo attuale
        Vector3 currentScreenPos = mainCam.WorldToViewportPoint(currentTarget.position);

        foreach (Collider col in colliders)
        {
            EnemyHealth health = col.GetComponentInParent<EnemyHealth>();
            if (health == null) continue;
            Transform candidate = health.transform;

            if (candidate == currentTarget) continue;

            Vector3 candidateScreenPos = mainCam.WorldToViewportPoint(candidate.position);
            
            // Differenza X e Y
            float diffX = candidateScreenPos.x - currentScreenPos.x;
            float diffY = Mathf.Abs(candidateScreenPos.y - currentScreenPos.y);

            // Se cerco a Destra (dir > 0) voglio diffX positiva
            // Se cerco a Sinistra (dir < 0) voglio diffX negativa
            if ((direction > 0 && diffX > 0) || (direction < 0 && diffX < 0))
            {
                // Score: Più è vicino in orizzontale meglio è. Penalizziamo la distanza verticale.
                float score = Mathf.Abs(diffX) + (diffY * 2);
                
                if (score < bestScore)
                {
                    bestScore = score;
                    bestCandidate = candidate;
                }
            }
        }

        if (bestCandidate != null)
        {
            StartLockOn(bestCandidate);
        }
    }
    // ------------------------

    void HandleLockOnInput(InputAction.CallbackContext context)
    {
        if (isLockedOn) StopLockOn();
        else FindAndLockTarget();
    }

    void FindAndLockTarget()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, scanRadius, enemyLayer);
        float shortestDistance = Mathf.Infinity;
        Transform nearestTarget = null;

        foreach (Collider col in colliders)
        {
            EnemyHealth health = col.GetComponentInParent<EnemyHealth>();
            if (health == null) continue; 
            
            Transform targetRoot = health.transform; 
            Vector3 dirToTarget = (targetRoot.position - transform.position).normalized;
            float angle = Vector3.Angle(mainCam.transform.forward, dirToTarget);
            
            if (angle < 60f) 
            {
                float dist = Vector3.Distance(transform.position, targetRoot.position);
                if (dist < shortestDistance)
                {
                    shortestDistance = dist;
                    nearestTarget = targetRoot;
                }
            }
        }

        if (nearestTarget != null) StartLockOn(nearestTarget);
        else RecenterCamera();
    }

    void StartLockOn(Transform target)
    {
        currentTarget = target;
        isLockedOn = true;
        lockOnCamera.LookAt = currentTarget;
        lockOnCamera.Priority = 20; 
        if (targetIcon != null) targetIcon.gameObject.SetActive(true);
    }

    public void StopLockOn()
    {
        isLockedOn = false;
        currentTarget = null;

        // FIX CAMERA SNAP (Mantenuto dalla tua versione)
        if (mainCam != null && freeLookCamera != null)
        {
            freeLookCamera.m_XAxis.Value = mainCam.transform.eulerAngles.y;
            freeLookCamera.m_YAxis.Value = 0.5f; 
        }

        lockOnCamera.Priority = 0;
        lockOnCamera.LookAt = null;
        if (targetIcon != null) targetIcon.gameObject.SetActive(false);
        
        // FIX ROTAZIONE (Mantenuto)
        if (playerModel != null) playerModel.localRotation = Quaternion.identity;
    }

    private RectTransform FindTargetIcon()
    {
        if (targetIcon != null)
            return targetIcon;

        GameObject iconObject = GameObject.Find("targetLock");
        if (iconObject != null)
            return iconObject.GetComponent<RectTransform>();

        return null;
    }

    void HandleRotation()
    {
        // Se il giocatore sta schivando, non forzare la rotazione verso il nemico.
        if (playerController != null && playerController.isDodging)
        {
            return;
        }

        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0; 
        if (dir == Vector3.zero) return;
        
        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        
        if (playerModel != null) playerModel.localRotation = Quaternion.identity;
    }

    void UpdateTargetUI()
    {
        if (targetIcon != null && currentTarget != null)
        {
            Vector3 worldPos = currentTarget.position + Vector3.up * 1.4f;
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);
            
            if (screenPos.z > 0)
            {
                targetIcon.gameObject.SetActive(true);
                targetIcon.position = screenPos;
            }
            else targetIcon.gameObject.SetActive(false);
        }
    }

    void RecenterCamera()
    {
        if(freeLookCamera != null)
            freeLookCamera.m_XAxis.Value = transform.eulerAngles.y;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, scanRadius);
    }

    public void SetCameraInputActive(bool active)
    {
        if (freeLookCamera == null) return;

        if (active)
        {
            freeLookCamera.m_XAxis.m_InputAxisName = "Mouse X";
            freeLookCamera.m_YAxis.m_InputAxisName = "Mouse Y";
        }
        else
        {
            freeLookCamera.m_XAxis.m_InputAxisName = "";
            freeLookCamera.m_YAxis.m_InputAxisName = "";
        }
    }
}
