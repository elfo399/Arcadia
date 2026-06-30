using UnityEngine;

public class HubMapZone : MonoBehaviour
{
    [Header("World Bounds")]
    [SerializeField] private Vector2 worldCenterXZ = Vector2.zero;
    [SerializeField] private Vector2 worldSizeXZ = new Vector2(80f, 80f);

    [Header("Visual")]
    [SerializeField] private Sprite mapSprite;
    [SerializeField] private Transform portalMarkerTarget;

    public Vector2 WorldCenterXZ => worldCenterXZ;
    public Vector2 WorldSizeXZ => new Vector2(Mathf.Max(0.01f, worldSizeXZ.x), Mathf.Max(0.01f, worldSizeXZ.y));
    public Sprite MapSprite => mapSprite;
    public Transform PortalMarkerTarget => portalMarkerTarget;

    public Vector2 WorldToNormalized(Vector3 worldPosition)
    {
        Vector2 size = WorldSizeXZ;
        Vector2 min = worldCenterXZ - size * 0.5f;
        float x = Mathf.InverseLerp(min.x, min.x + size.x, worldPosition.x);
        float y = Mathf.InverseLerp(min.y, min.y + size.y, worldPosition.z);
        return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
    }

    public void Configure(Vector2 centerXZ, Vector2 sizeXZ, Transform portalTarget = null)
    {
        worldCenterXZ = centerXZ;
        worldSizeXZ = new Vector2(Mathf.Max(0.01f, sizeXZ.x), Mathf.Max(0.01f, sizeXZ.y));
        portalMarkerTarget = portalTarget;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 size = WorldSizeXZ;
        Vector3 center = new Vector3(worldCenterXZ.x, transform.position.y + 0.05f, worldCenterXZ.y);
        Vector3 boundsSize = new Vector3(size.x, 0.1f, size.y);

        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.25f);
        Gizmos.DrawCube(center, boundsSize);
        Gizmos.color = new Color(0.1f, 0.8f, 1f, 1f);
        Gizmos.DrawWireCube(center, boundsSize);
    }
}
