using UnityEngine;

[DisallowMultipleComponent]
public class TargetLockPoint : MonoBehaviour
{
    [SerializeField] private bool lockable = true;
    [SerializeField, Min(0.01f)] private float priority = 1f;

    public bool IsLockable => lockable && isActiveAndEnabled && gameObject.activeInHierarchy;
    public float Priority => Mathf.Max(0.01f, priority);

    private void OnDrawGizmos()
    {
        Gizmos.color = lockable ? Color.white : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.12f);
    }
}
