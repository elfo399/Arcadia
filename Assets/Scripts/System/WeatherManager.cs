using System;
using UnityEngine;

public class WeatherManager : MonoBehaviour
{
    public enum DayPhase
    {
        Dawn = 0,
        Day = 1,
        Sunset = 2,
        Night = 3
    }

    public enum WeatherCondition
    {
        Clear = 0,
        Raining = 1,
        Lightning2 = 3
    }

    [Serializable]
    public class DayPhaseSettings
    {
        public DayPhase phase = DayPhase.Day;
        public string displayName = "Sunny Day";
        [Min(1f)] public float durationSeconds = 60f;
        public Color ambientLightColor = Color.white;
        public Color directionalLightColor = Color.white;
        [Range(0f, 2f)] public float directionalLightIntensity = 1f;
        public Vector3 directionalLightEulerAngles = new Vector3(50f, -30f, 0f);
        public string animatorStateName = "Day";
    }

    [Serializable]
    public class WeatherConditionSettings
    {
        public WeatherCondition condition = WeatherCondition.Clear;
        [Min(0f)] public float weight = 1f;
        public string displayName = "";
        public string animatorStateName = "";
    }

    public static WeatherManager Instance { get; private set; }

    [Header("Cycle")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool useUnscaledTime;
    [SerializeField, Min(0f)] private float timeMultiplier = 1f;
    [SerializeField] private DayPhase startingPhase = DayPhase.Day;
    [SerializeField] private WeatherCondition currentCondition = WeatherCondition.Clear;
    [SerializeField] private DayPhaseSettings[] dayPhases =
    {
        new DayPhaseSettings
        {
            phase = DayPhase.Dawn,
            displayName = "Alba",
            durationSeconds = 45f,
            ambientLightColor = new Color(0.55f, 0.45f, 0.42f, 1f),
            directionalLightColor = new Color(1f, 0.73f, 0.55f, 1f),
            directionalLightIntensity = 0.55f,
            directionalLightEulerAngles = new Vector3(15f, -30f, 0f),
            animatorStateName = "Dawn"
        },
        new DayPhaseSettings
        {
            phase = DayPhase.Day,
            displayName = "Giorno",
            durationSeconds = 90f,
            ambientLightColor = new Color(0.78f, 0.78f, 0.74f, 1f),
            directionalLightColor = new Color(1f, 0.96f, 0.84f, 1f),
            directionalLightIntensity = 1f,
            directionalLightEulerAngles = new Vector3(45f, -30f, 0f),
            animatorStateName = "Day"
        },
        new DayPhaseSettings
        {
            phase = DayPhase.Sunset,
            displayName = "Tramonto",
            durationSeconds = 60f,
            ambientLightColor = new Color(0.5f, 0.34f, 0.36f, 1f),
            directionalLightColor = new Color(1f, 0.48f, 0.32f, 1f),
            directionalLightIntensity = 0.45f,
            directionalLightEulerAngles = new Vector3(115f, -30f, 0f),
            animatorStateName = "Noon"
        },
        new DayPhaseSettings
        {
            phase = DayPhase.Night,
            displayName = "Notte",
            durationSeconds = 75f,
            ambientLightColor = new Color(0.2f, 0.24f, 0.36f, 1f),
            directionalLightColor = new Color(0.46f, 0.56f, 0.9f, 1f),
            directionalLightIntensity = 0.2f,
            directionalLightEulerAngles = new Vector3(135f, -30f, 0f),
            animatorStateName = "Night"
        }
    };

    [Header("Weather Conditions")]
    [SerializeField] private bool autoChangeWeather = true;
    [SerializeField, Min(1f)] private float weatherChangeIntervalSeconds = 45f;
    [SerializeField] private bool rerollWeatherOnPhaseChange = true;
    [SerializeField] private bool avoidRepeatingWeather = true;
    [SerializeField] private WeatherConditionSettings[] weatherConditions =
    {
        new WeatherConditionSettings
        {
            condition = WeatherCondition.Clear,
            weight = 60f,
            displayName = "",
            animatorStateName = ""
        },
        new WeatherConditionSettings
        {
            condition = WeatherCondition.Raining,
            weight = 25f,
            displayName = "Pioggia",
            animatorStateName = "Raining"
        },
        new WeatherConditionSettings
        {
            condition = WeatherCondition.Lightning2,
            weight = 15f,
            displayName = "Tempesta",
            animatorStateName = "Lightning2"
        }
    };

    [Header("Visuals")]
    [SerializeField] private Animator weatherAnimator;
    [SerializeField] private Light directionalLight;
    [SerializeField] private bool driveAnimator = true;
    [SerializeField] private bool driveDirectionalLight = true;
    [SerializeField] private bool driveAmbientLight = true;
    [SerializeField] private bool smoothPhaseLighting = true;

    private bool isRunning;
    private float cycleTimeSeconds;
    private int currentPhaseIndex = -1;
    private DayPhaseSettings currentPhaseSettings;
    private string currentDisplayName = string.Empty;
    private WeatherCondition lastAppliedCondition = (WeatherCondition)(-1);
    private float weatherTimerSeconds;

    public event Action<DayPhase, string> DayPhaseChanged;
    public event Action<WeatherCondition, string> WeatherChanged;
    public event Action<string> DisplayNameChanged;

    public DayPhase CurrentPhase => currentPhaseSettings != null ? currentPhaseSettings.phase : startingPhase;
    public WeatherCondition CurrentCondition => currentCondition;
    public string CurrentDisplayName => currentDisplayName;
    public bool IsRunning => isRunning;
    public float CycleTimeSeconds => cycleTimeSeconds;
    public float CycleNormalized
    {
        get
        {
            float totalDuration = GetTotalCycleDuration();
            return totalDuration > 0f ? cycleTimeSeconds / totalDuration : 0f;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[WeatherManager] Multiple WeatherManager instances found. The latest one will be used.");

        Instance = this;
        ResolveReferences();
        NormalizePhaseSettings();
        SetCycleToPhase(startingPhase);
        isRunning = playOnStart;
        weatherTimerSeconds = 0f;
        ApplyCurrentState(force: true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!isRunning)
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float scaledDeltaTime = deltaTime * timeMultiplier;
        AdvanceCycle(scaledDeltaTime);
        AdvanceWeather(scaledDeltaTime);
        ApplyPhaseLighting();
    }

    public void Play()
    {
        isRunning = true;
    }

    public void Pause()
    {
        isRunning = false;
    }

    public void SetTimeMultiplier(float value)
    {
        timeMultiplier = Mathf.Max(0f, value);
    }

    public void SetDayPhase(DayPhase phase)
    {
        SetCycleToPhase(phase);
        UpdateCurrentPhase(force: true);
    }

    public void SetWeatherCondition(WeatherCondition condition)
    {
        if (currentCondition == condition)
            return;

        currentCondition = condition;
        ApplyCurrentState(force: false);
    }

    private void AdvanceCycle(float deltaSeconds)
    {
        float totalDuration = GetTotalCycleDuration();
        if (totalDuration <= 0f)
            return;

        cycleTimeSeconds = Mathf.Repeat(cycleTimeSeconds + deltaSeconds, totalDuration);
        bool phaseChanged = UpdateCurrentPhase(force: false);

        if (phaseChanged && rerollWeatherOnPhaseChange)
            RollWeatherCondition();
    }

    private bool UpdateCurrentPhase(bool force)
    {
        int nextPhaseIndex = GetPhaseIndexAtTime(cycleTimeSeconds);
        if (nextPhaseIndex < 0 || nextPhaseIndex >= dayPhases.Length)
            return false;

        bool phaseChanged = nextPhaseIndex != currentPhaseIndex;
        if (!force && !phaseChanged && currentCondition == lastAppliedCondition)
            return false;

        currentPhaseIndex = nextPhaseIndex;
        currentPhaseSettings = dayPhases[currentPhaseIndex];
        ApplyCurrentState(force || phaseChanged);
        return phaseChanged;
    }

    private void AdvanceWeather(float deltaSeconds)
    {
        if (!autoChangeWeather || deltaSeconds <= 0f)
            return;

        weatherTimerSeconds += deltaSeconds;
        float interval = Mathf.Max(1f, weatherChangeIntervalSeconds);
        if (weatherTimerSeconds < interval)
            return;

        weatherTimerSeconds = Mathf.Repeat(weatherTimerSeconds, interval);
        RollWeatherCondition();
    }

    private void ApplyCurrentState(bool force)
    {
        if (currentPhaseSettings == null)
            currentPhaseSettings = GetPhaseSettings(startingPhase) ?? dayPhases[0];

        string nextDisplayName = BuildDisplayName();
        bool displayChanged = !string.Equals(currentDisplayName, nextDisplayName, StringComparison.Ordinal);
        currentDisplayName = nextDisplayName;

        if (driveAnimator)
            PlayAnimatorState(GetAnimatorStateName());

        ApplyPhaseLighting();

        if (force || displayChanged)
            DisplayNameChanged?.Invoke(currentDisplayName);

        if (force || currentCondition != lastAppliedCondition)
            WeatherChanged?.Invoke(currentCondition, currentDisplayName);

        if (force)
            DayPhaseChanged?.Invoke(CurrentPhase, currentDisplayName);

        lastAppliedCondition = currentCondition;
    }

    private void ApplyPhaseLighting()
    {
        if (currentPhaseSettings == null)
            currentPhaseSettings = GetPhaseSettings(startingPhase) ?? dayPhases[0];

        DayPhaseSettings nextPhaseSettings = smoothPhaseLighting ? GetNextPhaseSettings() : currentPhaseSettings;
        float phaseProgress = smoothPhaseLighting ? GetCurrentPhaseProgress() : 0f;

        if (driveAmbientLight)
            RenderSettings.ambientLight = Color.Lerp(currentPhaseSettings.ambientLightColor, nextPhaseSettings.ambientLightColor, phaseProgress);

        if (driveDirectionalLight && directionalLight != null)
        {
            directionalLight.color = Color.Lerp(currentPhaseSettings.directionalLightColor, nextPhaseSettings.directionalLightColor, phaseProgress);
            directionalLight.intensity = Mathf.Lerp(currentPhaseSettings.directionalLightIntensity, nextPhaseSettings.directionalLightIntensity, phaseProgress);
            directionalLight.transform.rotation = Quaternion.Euler(InterpolateSunEuler(currentPhaseSettings.directionalLightEulerAngles, nextPhaseSettings.directionalLightEulerAngles, phaseProgress));
        }
    }

    private string BuildDisplayName()
    {
        string phaseDisplayName = ResolveCurrentPhaseDisplayName();
        if (currentCondition == WeatherCondition.Clear)
            return phaseDisplayName;

        WeatherConditionSettings settings = GetWeatherConditionSettings(currentCondition);
        if (settings != null && !string.IsNullOrWhiteSpace(settings.displayName))
            return $"{phaseDisplayName} - {settings.displayName}";

        return $"{phaseDisplayName} - {currentCondition}";
    }

    private string ResolveCurrentPhaseDisplayName()
    {
        if (currentPhaseSettings == null)
            currentPhaseSettings = GetPhaseSettings(startingPhase) ?? dayPhases[0];

        return string.IsNullOrWhiteSpace(currentPhaseSettings.displayName)
            ? currentPhaseSettings.phase.ToString()
            : currentPhaseSettings.displayName;
    }

    private string GetAnimatorStateName()
    {
        if (currentCondition == WeatherCondition.Clear)
            return string.IsNullOrWhiteSpace(currentPhaseSettings.animatorStateName) ? currentPhaseSettings.phase.ToString() : currentPhaseSettings.animatorStateName;

        WeatherConditionSettings settings = GetWeatherConditionSettings(currentCondition);
        if (settings != null && !string.IsNullOrWhiteSpace(settings.animatorStateName))
            return settings.animatorStateName;

        return currentCondition.ToString();
    }

    private void PlayAnimatorState(string stateName)
    {
        ResolveReferences();

        if (weatherAnimator == null || weatherAnimator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(stateName))
            return;

        weatherAnimator.Play(stateName, 0, 0f);
    }

    private void SetCycleToPhase(DayPhase phase)
    {
        NormalizePhaseSettings();

        float time = 0f;
        for (int i = 0; i < dayPhases.Length; i++)
        {
            if (dayPhases[i] != null && dayPhases[i].phase == phase)
            {
                cycleTimeSeconds = time;
                currentPhaseIndex = i;
                currentPhaseSettings = dayPhases[i];
                return;
            }

            time += GetPhaseDuration(dayPhases[i]);
        }

        cycleTimeSeconds = 0f;
        currentPhaseIndex = 0;
        currentPhaseSettings = dayPhases[0];
    }

    private int GetPhaseIndexAtTime(float timeSeconds)
    {
        float elapsed = 0f;
        for (int i = 0; i < dayPhases.Length; i++)
        {
            elapsed += GetPhaseDuration(dayPhases[i]);
            if (timeSeconds < elapsed)
                return i;
        }

        return dayPhases.Length - 1;
    }

    private DayPhaseSettings GetPhaseSettings(DayPhase phase)
    {
        for (int i = 0; i < dayPhases.Length; i++)
        {
            if (dayPhases[i] != null && dayPhases[i].phase == phase)
                return dayPhases[i];
        }

        return null;
    }

    private WeatherConditionSettings GetWeatherConditionSettings(WeatherCondition condition)
    {
        if (weatherConditions == null)
            return null;

        for (int i = 0; i < weatherConditions.Length; i++)
        {
            if (weatherConditions[i] != null && weatherConditions[i].condition == condition)
                return weatherConditions[i];
        }

        return null;
    }

    private void RollWeatherCondition()
    {
        WeatherCondition nextCondition = ChooseWeightedWeatherCondition();
        weatherTimerSeconds = 0f;
        SetWeatherCondition(nextCondition);
    }

    private WeatherCondition ChooseWeightedWeatherCondition()
    {
        if (weatherConditions == null || weatherConditions.Length == 0)
            return WeatherCondition.Clear;

        float totalWeight = 0f;
        int validAlternativeCount = 0;
        for (int i = 0; i < weatherConditions.Length; i++)
        {
            WeatherConditionSettings settings = weatherConditions[i];
            if (settings == null || settings.weight <= 0f)
                continue;

            if (avoidRepeatingWeather && settings.condition == currentCondition && HasWeightedAlternative())
                continue;

            totalWeight += settings.weight;
            validAlternativeCount++;
        }

        if (totalWeight <= 0f || validAlternativeCount == 0)
            return WeatherCondition.Clear;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;
        for (int i = 0; i < weatherConditions.Length; i++)
        {
            WeatherConditionSettings settings = weatherConditions[i];
            if (settings == null || settings.weight <= 0f)
                continue;

            if (avoidRepeatingWeather && settings.condition == currentCondition && HasWeightedAlternative())
                continue;

            cumulativeWeight += settings.weight;
            if (roll <= cumulativeWeight)
                return settings.condition;
        }

        return WeatherCondition.Clear;
    }

    private bool HasWeightedAlternative()
    {
        if (weatherConditions == null)
            return false;

        for (int i = 0; i < weatherConditions.Length; i++)
        {
            WeatherConditionSettings settings = weatherConditions[i];
            if (settings != null && settings.weight > 0f && settings.condition != currentCondition)
                return true;
        }

        return false;
    }

    private DayPhaseSettings GetNextPhaseSettings()
    {
        NormalizePhaseSettings();

        if (currentPhaseIndex < 0 || currentPhaseIndex >= dayPhases.Length)
            return currentPhaseSettings ?? dayPhases[0];

        int nextIndex = (currentPhaseIndex + 1) % dayPhases.Length;
        return dayPhases[nextIndex] ?? currentPhaseSettings;
    }

    private float GetCurrentPhaseProgress()
    {
        NormalizePhaseSettings();

        if (currentPhaseIndex < 0 || currentPhaseIndex >= dayPhases.Length)
            return 0f;

        float phaseStartTime = 0f;
        for (int i = 0; i < currentPhaseIndex; i++)
            phaseStartTime += GetPhaseDuration(dayPhases[i]);

        float phaseDuration = GetPhaseDuration(currentPhaseSettings);
        return Mathf.Clamp01((cycleTimeSeconds - phaseStartTime) / phaseDuration);
    }

    private float GetTotalCycleDuration()
    {
        NormalizePhaseSettings();

        float total = 0f;
        for (int i = 0; i < dayPhases.Length; i++)
            total += GetPhaseDuration(dayPhases[i]);

        return total;
    }

    private static float GetPhaseDuration(DayPhaseSettings settings)
    {
        return settings != null ? Mathf.Max(1f, settings.durationSeconds) : 1f;
    }

    private static Vector3 InterpolateSunEuler(Vector3 from, Vector3 to, float t)
    {
        if (to.x < from.x)
            to.x += 360f;

        return new Vector3(
            Mathf.Lerp(from.x, to.x, t),
            Mathf.LerpAngle(from.y, to.y, t),
            Mathf.LerpAngle(from.z, to.z, t));
    }

    private void NormalizePhaseSettings()
    {
        if (dayPhases == null || dayPhases.Length == 0)
            dayPhases = CreateDefaultPhases();
    }

    private static DayPhaseSettings[] CreateDefaultPhases()
    {
        return new[]
        {
            new DayPhaseSettings { phase = DayPhase.Dawn, displayName = "Alba", animatorStateName = "Dawn" },
            new DayPhaseSettings { phase = DayPhase.Day, displayName = "Giorno", animatorStateName = "Day" },
            new DayPhaseSettings { phase = DayPhase.Sunset, displayName = "Tramonto", animatorStateName = "Noon" },
            new DayPhaseSettings { phase = DayPhase.Night, displayName = "Notte", animatorStateName = "Night" }
        };
    }

    private void ResolveReferences()
    {
        if (directionalLight == null)
            directionalLight = RenderSettings.sun;
    }
}
