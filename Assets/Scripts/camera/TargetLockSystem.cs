using UnityEngine;
using Cinemachine;
using UnityEngine.InputSystem;

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

    [Header("UI")]
    public RectTransform targetIcon;  

    [Header("Debug")]
    public bool isLockedOn = false;
    public Transform currentTarget;

    private PlayerControls controls;
    private Camera mainCam;
    private PlayerController controller; // Riferimento al controller

    void Awake()
    {
        controls = new PlayerControls();
        mainCam = Camera.main;
        controller = GetComponent<PlayerController>(); // Prendiamo il controller
    }

    void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.LockOn.performed += _ => HandleLockOnInput();
    }

    void OnDisable()
    {
        controls.Player.LockOn.performed -= _ => HandleLockOnInput();
        controls.Player.Disable();
    }

    void Start()
    {
        freeLookCamera.Priority = 10;
        lockOnCamera.Priority = 0;
        if (targetIcon != null) targetIcon.gameObject.SetActive(false);
    }

    void Update()
    {
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
        }
    }

    void HandleLockOnInput()
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

        if (mainCam != null && freeLookCamera != null)
        {
            freeLookCamera.m_XAxis.Value = mainCam.transform.eulerAngles.y;
            freeLookCamera.m_YAxis.Value = 0.5f; 
        }

        lockOnCamera.Priority = 0;
        lockOnCamera.LookAt = null;
        if (targetIcon != null) targetIcon.gameObject.SetActive(false);
        
        // Reset rotazione grafica per sicurezza
        if (playerModel != null) playerModel.localRotation = Quaternion.identity;
    }

    void HandleRotation()
    {
        // --- FIX FONDAMENTALE ---
        // Se stiamo rollando, NON forzare la rotazione verso il nemico.
        // Lascia che il PlayerController gestisca la direzione (così può girarsi e scappare).
        if (controller != null && controller.isDodging) return;
        // ------------------------

        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0; 
        if (dir == Vector3.zero) return;
        
        Quaternion targetRot = Quaternion.LookRotation(dir);
        
        // Ruotiamo il PADRE
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 15f);
        
        // Teniamo il FIGLIO dritto
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
}