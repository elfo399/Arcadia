using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TargetLockPoint : MonoBehaviour
{
    private static readonly List<TargetLockPoint> activeLockPoints = new List<TargetLockPoint>();

    [SerializeField] private bool lockable = true;
    [SerializeField, Min(0.01f)] private float priority = 1f;

    public static IReadOnlyList<TargetLockPoint> ActiveLockPoints => activeLockPoints;
    public bool IsLockable => lockable && isActiveAndEnabled && gameObject.activeInHierarchy;
    public float Priority => Mathf.Max(0.01f, priority);

    private void OnEnable()
    {
        if (!activeLockPoints.Contains(this))
            activeLockPoints.Add(this);
    }

    private void OnDisable()
    {
        activeLockPoints.Remove(this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = lockable ? Color.white : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.12f);
    }
}
