using UnityEngine;

public class PlayerStatDynamicBar : MonoBehaviour
{
    public enum TrackedStat
    {
        Health,
        Mana,
        Stamina
    }

    [Header("References")]
    public PlayerStats playerStats;
    public DynamicBar dynamicBar;

    [Header("Tracking")]
    public TrackedStat trackedStat = TrackedStat.Health;

    [Header("Fill")]
    [SerializeField] private Color fillColor = Color.red;
    [SerializeField] private bool applyFillColor = true;

    [Header("Sizing")]
    [SerializeField] private bool useTrackedStatDefaultSizing = true;
    [SerializeField] private float baseWidth = 140f;
    [SerializeField] private float widthPerPoint = 0.9f;
    [SerializeField] private float widthScale = 0.82f;
    [SerializeField] private float minWidth = 100f;
    [SerializeField] private float maxWidth = 320f;

    private float lastCurrent = -1f;
    private float lastMax = -1f;
    private Color lastAppliedFillColor;
    private bool fillColorApplied;

    private void Awake()
    {
        ResolveReferences();
        ApplyFillColorIfNeeded(force: true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyFillColorIfNeeded(force: true);
        ForceRefresh();
    }

    private void Update()
    {
        if (!ResolveReferences())
            return;

        ApplyFillColorIfNeeded(force: false);

        float max = GetMaxValue();
        float current = GetCurrentValue();

        if (!Mathf.Approximately(max, lastMax))
        {
            dynamicBar.SetMax(max, CalculateWidth(max));
            lastMax = max;
            lastCurrent = -1f;
        }

        if (!Mathf.Approximately(current, lastCurrent))
        {
            dynamicBar.SetCurrent(current);
            lastCurrent = current;
        }
    }

    public void ForceRefresh()
    {
        lastCurrent = -1f;
        lastMax = -1f;
    }

    private void OnValidate()
    {
        if (dynamicBar == null)
            dynamicBar = GetComponent<DynamicBar>();

        ApplyFillColorIfNeeded(force: true);
        ForceRefresh();
    }

    private bool ResolveReferences()
    {
        if (playerStats == null)
            playerStats = PlayerStats.instance;

        if (dynamicBar == null)
            dynamicBar = GetComponent<DynamicBar>();

        return playerStats != null && dynamicBar != null;
    }

    private void ApplyFillColorIfNeeded(bool force)
    {
        if (!applyFillColor || dynamicBar == null)
            return;

        if (!force && fillColorApplied && lastAppliedFillColor == fillColor)
            return;

        dynamicBar.SetFillColor(fillColor);
        lastAppliedFillColor = fillColor;
        fillColorApplied = true;
    }

    private float GetCurrentValue()
    {
        switch (trackedStat)
        {
            case TrackedStat.Mana:
                return playerStats.currentMana;
            case TrackedStat.Stamina:
                return playerStats.currentStamina;
            default:
                return playerStats.currentHealth;
        }
    }

    private float GetMaxValue()
    {
        switch (trackedStat)
        {
            case TrackedStat.Mana:
                return playerStats.maxMana;
            case TrackedStat.Stamina:
                return playerStats.maxStamina;
            default:
                return playerStats.maxHealth;
        }
    }

    private float CalculateWidth(float maxValue)
    {
        float resolvedBaseWidth = ResolveBaseWidth();
        float resolvedWidthPerPoint = ResolveWidthPerPoint();

        float width = resolvedBaseWidth + Mathf.Max(1f, maxValue) * resolvedWidthPerPoint;
        width *= Mathf.Max(0.1f, widthScale);
        return Mathf.Clamp(width, minWidth, maxWidth);
    }

    private float ResolveBaseWidth()
    {
        if (!useTrackedStatDefaultSizing)
            return baseWidth;

        switch (trackedStat)
        {
            case TrackedStat.Mana:
                return 120f;
            default:
                return 140f;
        }
    }

    private float ResolveWidthPerPoint()
    {
        if (!useTrackedStatDefaultSizing)
            return widthPerPoint;

        switch (trackedStat)
        {
            case TrackedStat.Stamina:
                return 0.7f;
            default:
                return 0.9f;
        }
    }
}
