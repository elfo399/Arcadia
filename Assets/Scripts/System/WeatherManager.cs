using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    [SerializeField] private bool suppressWorldWeatherEffectsInIndoorScenes = true;
    [SerializeField] private string[] indoorSceneNames = { "GameScene" };
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

    [Header("Weather Transitions")]
    [SerializeField, Min(0.05f)] private float weatherEffectFadeInSeconds = 2.5f;
    [SerializeField, Min(0.05f)] private float weatherEffectFadeOutSeconds = 4f;

    [Header("World Effects")]
    [SerializeField] private string rainEffectObjectName = "RainFX";
    [SerializeField] private Transform rainEffectRoot;
    [SerializeField] private ParticleSystem[] rainParticleSystems;
    [SerializeField, Min(1f)] private float rainBaseEmissionRate = 1400f;
    [SerializeField, Min(0f)] private float rainingEmissionMultiplier = 1f;
    [SerializeField, Min(0f)] private float stormEmissionMultiplier = 1.35f;

    [Header("World Wind")]
    [SerializeField] private Vector2 rainWindVelocity = new Vector2(1.4f, 0.35f);
    [SerializeField] private Vector2 stormWindVelocity = new Vector2(4.5f, 1.2f);

    [Header("World Lightning")]
    [SerializeField] private bool enableLightningEffects = true;
    [SerializeField] private string lightningEffectObjectName = "LightningFX";
    [SerializeField] private Transform lightningRoot;
    [SerializeField] private LineRenderer lightningLine;
    [SerializeField] private Light lightningFlashLight;
    [SerializeField, Min(0.1f)] private float lightningMinIntervalSeconds = 2.5f;
    [SerializeField, Min(0.1f)] private float lightningMaxIntervalSeconds = 7f;
    [SerializeField, Min(0.02f)] private float lightningBoltDurationSeconds = 0.12f;
    [SerializeField, Min(1f)] private float lightningSpawnRadius = 28f;
    [SerializeField] private float lightningSkyHeight = 28f;
    [SerializeField] private float lightningGroundHeight = 1.5f;
    [SerializeField, Min(2)] private int lightningSegmentCount = 9;
    [SerializeField, Min(0.01f)] private float lightningWidth = 0.08f;
    [SerializeField] private Color lightningColor = new Color(0.62f, 0.82f, 1f, 1f);
    [SerializeField, Min(0f)] private float lightningFlashIntensity = 4f;
    [SerializeField, Min(1f)] private float lightningFlashRange = 65f;

    [Header("Wet Surface Example")]
    [SerializeField] private Transform wetSurfaceRoot;
    [SerializeField] private LineRenderer[] wetSurfaceLineRenderers;
    [SerializeField] private Color wetSurfaceColor = new Color(0.23f, 0.42f, 0.55f, 0.42f);
    [SerializeField] private Vector2 wetSurfaceSize = new Vector2(3.2f, 1.5f);
    [SerializeField, Min(0.01f)] private float wetSurfaceLineWidth = 0.22f;
    [SerializeField, Min(8)] private int wetSurfaceSegments = 28;
    [SerializeField, Range(0f, 1f)] private float wetSurfaceShapeIrregularity = 0.65f;
    [SerializeField, Min(0f)] private float wetSurfaceDryDelaySeconds = 8f;
    [SerializeField, Min(0.1f)] private float wetSurfaceDryFadeSeconds = 12f;
    [SerializeField, Range(0.05f, 1f)] private float wetSurfaceMinimumDryScale = 0.2f;

    private bool isRunning;
    private float cycleTimeSeconds;
    private int currentPhaseIndex = -1;
    private DayPhaseSettings currentPhaseSettings;
    private string currentDisplayName = string.Empty;
    private WeatherCondition lastAppliedCondition = (WeatherCondition)(-1);
    private float weatherTimerSeconds;
    private Coroutine lightningRoutine;
    private float rainEffectIntensity;
    private float wetSurfaceIntensity;
    private float wetSurfaceDryTimerSeconds;
    private Vector2 rainWindCurrentVelocity;
    private Mesh wetSurfaceMesh;
    private MeshFilter wetSurfaceMeshFilter;
    private MeshRenderer wetSurfaceMeshRenderer;
    private Material wetSurfaceMaterial;
    private bool wetSurfaceBaseScaleInitialized;
    private Vector3 wetSurfaceBaseLocalScale = Vector3.one;
    private int lastRainResolveSceneHandle = -1;
    private int lastLightningResolveSceneHandle = -1;

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
        rainEffectIntensity = 0f;
        wetSurfaceIntensity = 0f;
        wetSurfaceDryTimerSeconds = 0f;
        StopRainEffect();
        ApplyWetSurfaceEffect(suppressWorldEffects: false);
        RestoreWetSurfaceScale();
        DestroyRuntimeWetSurface();
        StopLightningEffect();

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
        ApplyWorldEffects();
    }

    public void Play()
    {
        isRunning = true;
    }

    public void Pause()
    {
        isRunning = false;
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
        ApplyWorldEffects();

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

    private bool ShouldSuppressWorldWeatherEffects()
    {
        if (!suppressWorldWeatherEffectsInIndoorScenes || indoorSceneNames == null || indoorSceneNames.Length == 0)
            return false;

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrWhiteSpace(activeSceneName))
            return false;

        for (int i = 0; i < indoorSceneNames.Length; i++)
        {
            string sceneName = indoorSceneNames[i];
            if (!string.IsNullOrWhiteSpace(sceneName)
                && string.Equals(activeSceneName, sceneName.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void PlayAnimatorState(string stateName)
    {
        ResolveReferences();

        if (weatherAnimator == null || weatherAnimator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(stateName))
            return;

        if (!weatherAnimator.isActiveAndEnabled || !weatherAnimator.gameObject.activeInHierarchy)
            return;

        weatherAnimator.Play(stateName, 0, 0f);
    }

    private void ApplyWorldEffects()
    {
        bool suppressWorldEffects = ShouldSuppressWorldWeatherEffects();
        ApplyRainEffect(suppressWorldEffects);
        ApplyWetSurfaceEffect(suppressWorldEffects);
        ApplyLightningEffect(suppressWorldEffects);
    }

    private void ApplyRainEffect(bool suppressWorldEffects)
    {
        ResolveRainReferences();

        float targetIntensity = suppressWorldEffects ? 0f : GetRainTargetIntensity();
        float transitionSeconds = targetIntensity > rainEffectIntensity ? weatherEffectFadeInSeconds : weatherEffectFadeOutSeconds;
        float transitionStep = GetWeatherEffectDeltaTime() / Mathf.Max(0.05f, transitionSeconds) * Mathf.Max(1f, targetIntensity);
        rainEffectIntensity = Mathf.MoveTowards(rainEffectIntensity, targetIntensity, transitionStep);

        Vector2 targetWind = suppressWorldEffects ? Vector2.zero : GetRainTargetWindVelocity();
        float windStep = GetWeatherEffectDeltaTime() / Mathf.Max(0.05f, transitionSeconds) * Mathf.Max(1f, targetWind.magnitude);
        rainWindCurrentVelocity = Vector2.MoveTowards(rainWindCurrentVelocity, targetWind, windStep);

        if (rainEffectIntensity <= 0.001f)
        {
            StopRainEffect();
            return;
        }

        if (rainEffectRoot != null && !rainEffectRoot.gameObject.activeSelf)
            rainEffectRoot.gameObject.SetActive(true);

        if (rainParticleSystems == null)
            return;

        for (int i = 0; i < rainParticleSystems.Length; i++)
        {
            ParticleSystem particles = rainParticleSystems[i];
            if (particles == null)
                continue;

            ConfigureRainParticleSystem(particles);
            if (!particles.isPlaying)
                particles.Play(true);
        }
    }

    private float GetWeatherEffectDeltaTime()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        return Mathf.Max(deltaTime, 0f);
    }

    private float GetRainTargetIntensity()
    {
        if (currentCondition == WeatherCondition.Raining)
            return rainingEmissionMultiplier;

        if (currentCondition == WeatherCondition.Lightning2)
            return stormEmissionMultiplier;

        return 0f;
    }

    private Vector2 GetRainTargetWindVelocity()
    {
        if (currentCondition == WeatherCondition.Raining)
            return rainWindVelocity;

        if (currentCondition == WeatherCondition.Lightning2)
            return stormWindVelocity;

        return Vector2.zero;
    }

    private void ConfigureRainParticleSystem(ParticleSystem particles)
    {
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(rainBaseEmissionRate * rainEffectIntensity);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(rainWindCurrentVelocity.x);
        velocity.z = new ParticleSystem.MinMaxCurve(rainWindCurrentVelocity.y);
    }

    private void ApplyLightningEffect(bool suppressWorldEffects)
    {
        bool storming = currentCondition == WeatherCondition.Lightning2 && !suppressWorldEffects;
        if (!storming || !enableLightningEffects)
        {
            StopLightningEffect();
            return;
        }

        ResolveLightningReferences();
        if (lightningLine == null || lightningFlashLight == null)
            return;

        if (lightningRoutine == null)
            lightningRoutine = StartCoroutine(LightningRoutine());
    }

    private void ResolveRainReferences()
    {
        if (rainEffectRoot != null && HasValidRainParticleSystem())
            return;

        if (rainEffectRoot == null && string.IsNullOrWhiteSpace(rainEffectObjectName))
            return;

        int activeSceneHandle = SceneManager.GetActiveScene().handle;
        if (rainEffectRoot == null && rainParticleSystems != null && rainParticleSystems.Length == 0 && lastRainResolveSceneHandle == activeSceneHandle)
            return;

        lastRainResolveSceneHandle = activeSceneHandle;

        string targetName = string.IsNullOrWhiteSpace(rainEffectObjectName) ? string.Empty : rainEffectObjectName.Trim();

        if (rainEffectRoot == null && !string.IsNullOrWhiteSpace(targetName))
        {
            Transform[] transforms = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && string.Equals(candidate.name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    rainEffectRoot = candidate;
                    break;
                }
            }
        }

        if (rainEffectRoot != null)
        {
            rainParticleSystems = rainEffectRoot.GetComponentsInChildren<ParticleSystem>(true);
            return;
        }

        ParticleSystem[] allParticles = FindObjectsOfType<ParticleSystem>(true);
        var matches = new List<ParticleSystem>();
        Transform matchedRoot = null;

        for (int i = 0; i < allParticles.Length; i++)
        {
            ParticleSystem particles = allParticles[i];
            if (particles == null)
                continue;

            Transform current = particles.transform;
            while (current != null)
            {
                if (string.Equals(current.name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    if (matchedRoot == null)
                        matchedRoot = current;

                    matches.Add(particles);
                    break;
                }

                current = current.parent;
            }
        }

        rainEffectRoot = matchedRoot;
        rainParticleSystems = matches.ToArray();
    }

    private void StopRainEffect()
    {
        if (rainParticleSystems != null)
        {
            for (int i = 0; i < rainParticleSystems.Length; i++)
            {
                ParticleSystem particles = rainParticleSystems[i];
                if (particles == null)
                    continue;

                if (particles.isPlaying || particles.particleCount > 0)
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (rainEffectRoot != null && rainEffectRoot.gameObject.activeSelf)
            rainEffectRoot.gameObject.SetActive(false);
    }

    private void ApplyWetSurfaceEffect(bool suppressWorldEffects)
    {
        ResolveWetSurfaceReferences();

        float wetIntensity = UpdateWetSurfaceIntensity(suppressWorldEffects);
        bool visible = wetIntensity > 0.001f;

        if (wetSurfaceRoot != null && wetSurfaceRoot.gameObject.activeSelf != visible)
            wetSurfaceRoot.gameObject.SetActive(visible);

        ApplyWetSurfaceScale(wetIntensity, visible);
        ConfigureWetSurfaceMesh(wetIntensity, visible);

        if (wetSurfaceLineRenderers == null)
            return;

        for (int i = 0; i < wetSurfaceLineRenderers.Length; i++)
        {
            LineRenderer puddle = wetSurfaceLineRenderers[i];
            if (puddle == null)
                continue;

            ConfigureWetSurfaceLine(puddle, wetIntensity);
        }
    }

    private float UpdateWetSurfaceIntensity(bool suppressWorldEffects)
    {
        if (suppressWorldEffects)
        {
            wetSurfaceIntensity = 0f;
            wetSurfaceDryTimerSeconds = 0f;
            return wetSurfaceIntensity;
        }

        float deltaTime = GetWeatherEffectDeltaTime();
        bool wetWeatherActive = currentCondition == WeatherCondition.Raining || currentCondition == WeatherCondition.Lightning2;
        float rainWetness = Mathf.Clamp01(rainEffectIntensity);

        if (wetWeatherActive || rainWetness > wetSurfaceIntensity)
        {
            wetSurfaceIntensity = Mathf.Max(wetSurfaceIntensity, rainWetness);
            wetSurfaceDryTimerSeconds = wetSurfaceDryDelaySeconds;
            return wetSurfaceIntensity;
        }

        if (wetSurfaceDryTimerSeconds > 0f)
        {
            wetSurfaceDryTimerSeconds = Mathf.Max(0f, wetSurfaceDryTimerSeconds - deltaTime);
            return wetSurfaceIntensity;
        }

        float dryStep = deltaTime / Mathf.Max(0.1f, wetSurfaceDryFadeSeconds);
        wetSurfaceIntensity = Mathf.MoveTowards(wetSurfaceIntensity, 0f, dryStep);
        return wetSurfaceIntensity;
    }

    private void ResolveWetSurfaceReferences()
    {
        if (wetSurfaceLineRenderers != null && wetSurfaceLineRenderers.Length > 0)
            return;

        if (wetSurfaceRoot == null)
            return;

        wetSurfaceLineRenderers = wetSurfaceRoot.GetComponentsInChildren<LineRenderer>(true);
    }

    private void ConfigureWetSurfaceLine(LineRenderer puddle, float intensity)
    {
        int segmentCount = Mathf.Max(8, wetSurfaceSegments);
        puddle.useWorldSpace = false;
        puddle.loop = true;
        puddle.positionCount = segmentCount;
        puddle.textureMode = LineTextureMode.Stretch;
        puddle.numCornerVertices = 4;
        puddle.numCapVertices = 4;
        float dryScale = GetWetSurfaceDryScale(intensity);
        float lineWidth = wetSurfaceLineWidth * Mathf.Lerp(0.18f, 0.55f, dryScale);
        puddle.startWidth = lineWidth;
        puddle.endWidth = lineWidth;

        Color color = wetSurfaceColor;
        color.a *= intensity * 0.85f;
        puddle.startColor = color;
        puddle.endColor = color;
        puddle.enabled = intensity > 0.001f;

        for (int i = 0; i < segmentCount; i++)
            puddle.SetPosition(i, GenerateWetSurfacePoint(i, segmentCount, 0.015f));
    }

    private void ConfigureWetSurfaceMesh(float intensity, bool visible)
    {
        if (wetSurfaceRoot == null)
            return;

        if (!visible)
        {
            if (wetSurfaceMeshRenderer != null)
                wetSurfaceMeshRenderer.enabled = false;

            return;
        }

        EnsureWetSurfaceMeshRuntime();

        if (wetSurfaceMeshRenderer != null)
            wetSurfaceMeshRenderer.enabled = true;

        if (wetSurfaceMesh == null)
            return;

        int segmentCount = Mathf.Max(8, wetSurfaceSegments);
        Vector3[] vertices = new Vector3[segmentCount + 1];
        int[] triangles = new int[segmentCount * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i < segmentCount; i++)
            vertices[i + 1] = GenerateWetSurfacePoint(i, segmentCount, 0f);

        for (int i = 0; i < segmentCount; i++)
        {
            int next = (i + 1) % segmentCount;
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = next + 1;
            triangles[triangleIndex + 2] = i + 1;
        }

        wetSurfaceMesh.Clear();
        wetSurfaceMesh.vertices = vertices;
        wetSurfaceMesh.triangles = triangles;
        wetSurfaceMesh.RecalculateBounds();
        wetSurfaceMesh.RecalculateNormals();

        if (wetSurfaceMaterial != null)
        {
            Color fillColor = wetSurfaceColor;
            fillColor.a *= intensity * 0.6f;
            SetMaterialColor(wetSurfaceMaterial, fillColor);
        }
    }

    private void ApplyWetSurfaceScale(float intensity, bool visible)
    {
        if (wetSurfaceRoot == null)
            return;

        EnsureWetSurfaceBaseScale();

        if (!visible)
        {
            wetSurfaceRoot.localScale = wetSurfaceBaseLocalScale;
            return;
        }

        wetSurfaceRoot.localScale = wetSurfaceBaseLocalScale * GetWetSurfaceDryScale(intensity);
    }

    private float GetWetSurfaceDryScale(float intensity)
    {
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(intensity));
        return Mathf.Lerp(wetSurfaceMinimumDryScale, 1f, t);
    }

    private void EnsureWetSurfaceBaseScale()
    {
        if (wetSurfaceBaseScaleInitialized || wetSurfaceRoot == null)
            return;

        wetSurfaceBaseLocalScale = wetSurfaceRoot.localScale;
        wetSurfaceBaseScaleInitialized = true;
    }

    private void RestoreWetSurfaceScale()
    {
        if (wetSurfaceRoot != null && wetSurfaceBaseScaleInitialized)
            wetSurfaceRoot.localScale = wetSurfaceBaseLocalScale;
    }

    private void EnsureWetSurfaceMeshRuntime()
    {
        if (wetSurfaceRoot == null)
            return;

        if (wetSurfaceMeshFilter == null)
            wetSurfaceMeshFilter = wetSurfaceRoot.GetComponent<MeshFilter>();

        if (wetSurfaceMeshFilter == null)
            wetSurfaceMeshFilter = wetSurfaceRoot.gameObject.AddComponent<MeshFilter>();

        if (wetSurfaceMeshRenderer == null)
            wetSurfaceMeshRenderer = wetSurfaceRoot.GetComponent<MeshRenderer>();

        if (wetSurfaceMeshRenderer == null)
            wetSurfaceMeshRenderer = wetSurfaceRoot.gameObject.AddComponent<MeshRenderer>();

        if (wetSurfaceMesh == null)
        {
            wetSurfaceMesh = new Mesh
            {
                name = "Runtime Wet Surface Mesh",
                hideFlags = HideFlags.DontSave
            };
        }

        if (wetSurfaceMeshFilter == null || wetSurfaceMeshRenderer == null)
            return;

        wetSurfaceMeshFilter.sharedMesh = wetSurfaceMesh;

        if (wetSurfaceMaterial == null)
            wetSurfaceMaterial = CreateWetSurfaceMaterial();

        wetSurfaceMeshRenderer.sharedMaterial = wetSurfaceMaterial;
        wetSurfaceMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        wetSurfaceMeshRenderer.receiveShadows = false;
    }

    private Material CreateWetSurfaceMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            name = "Runtime Wet Surface Material",
            hideFlags = HideFlags.DontSave,
            renderQueue = 3000
        };

        ConfigureTransparentMaterial(material);
        SetMaterialColor(material, wetSurfaceColor);
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private Vector3 GenerateWetSurfacePoint(int index, int segmentCount, float y)
    {
        float angle = (index / (float)segmentCount) * Mathf.PI * 2f;
        float irregularity = Mathf.Clamp01(wetSurfaceShapeIrregularity);
        float lobe =
            Mathf.Sin(angle * 2f + 0.45f) * 0.18f +
            Mathf.Sin(angle * 3f + 2.1f) * 0.12f +
            Mathf.Sin(angle * 5f + 1.35f) * 0.08f +
            Mathf.Sin(angle * 7f + 3.8f) * 0.05f;
        float edge = Mathf.Max(0.55f, 1f + lobe * irregularity * 1.7f);

        float x = Mathf.Cos(angle) * wetSurfaceSize.x * 0.5f * edge;
        float z = Mathf.Sin(angle) * wetSurfaceSize.y * 0.5f * edge;

        x += Mathf.Sin(angle * 1.5f + 2.4f) * wetSurfaceSize.x * 0.035f * irregularity;
        z += Mathf.Sin(angle * 2.5f + 0.9f) * wetSurfaceSize.y * 0.03f * irregularity;

        return new Vector3(x, y, z);
    }

    private void DestroyRuntimeWetSurface()
    {
        if (wetSurfaceMesh != null)
        {
            Destroy(wetSurfaceMesh);
            wetSurfaceMesh = null;
        }

        if (wetSurfaceMaterial != null)
        {
            Destroy(wetSurfaceMaterial);
            wetSurfaceMaterial = null;
        }
    }

    private bool HasValidRainParticleSystem()
    {
        if (rainParticleSystems == null || rainParticleSystems.Length == 0)
            return false;

        for (int i = 0; i < rainParticleSystems.Length; i++)
        {
            if (rainParticleSystems[i] != null)
                return true;
        }

        return false;
    }

    private IEnumerator LightningRoutine()
    {
        ResolveLightningReferences();

        while (currentCondition == WeatherCondition.Lightning2 && !ShouldSuppressWorldWeatherEffects())
        {
            float minInterval = Mathf.Max(0.1f, lightningMinIntervalSeconds);
            float maxInterval = Mathf.Max(minInterval, lightningMaxIntervalSeconds);
            yield return new WaitForSeconds(UnityEngine.Random.Range(minInterval, maxInterval));

            if (currentCondition != WeatherCondition.Lightning2 || ShouldSuppressWorldWeatherEffects())
                break;

            yield return PlayLightningStrike();
        }

        lightningRoutine = null;
        HideLightningRuntimeObjects();
    }

    private IEnumerator PlayLightningStrike()
    {
        ResolveLightningReferences();
        if (lightningLine == null || lightningFlashLight == null)
            yield break;

        Vector3 origin = ResolveWorldEffectOrigin();
        Vector2 offset = UnityEngine.Random.insideUnitCircle * lightningSpawnRadius;
        Vector3 start = origin + new Vector3(offset.x, lightningSkyHeight, offset.y);
        Vector3 end = new Vector3(start.x + UnityEngine.Random.Range(-3f, 3f), lightningGroundHeight, start.z + UnityEngine.Random.Range(-3f, 3f));

        int segmentCount = Mathf.Max(2, lightningSegmentCount);
        lightningLine.positionCount = segmentCount + 1;
        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            Vector3 point = Vector3.Lerp(start, end, t);
            float jitter = Mathf.Lerp(2.2f, 0.35f, t);
            if (i > 0 && i < segmentCount)
            {
                point.x += UnityEngine.Random.Range(-jitter, jitter);
                point.z += UnityEngine.Random.Range(-jitter, jitter);
            }

            lightningLine.SetPosition(i, point);
        }

        if (lightningRoot != null && !lightningRoot.gameObject.activeSelf)
            lightningRoot.gameObject.SetActive(true);

        lightningLine.startWidth = lightningWidth;
        lightningLine.endWidth = lightningWidth * 0.35f;
        lightningLine.startColor = lightningColor;
        lightningLine.endColor = new Color(lightningColor.r, lightningColor.g, lightningColor.b, 0.15f);
        lightningLine.enabled = true;
        lightningFlashLight.transform.position = Vector3.Lerp(start, end, 0.35f);
        lightningFlashLight.color = lightningColor;
        lightningFlashLight.range = lightningFlashRange;
        lightningFlashLight.intensity = lightningFlashIntensity;
        lightningFlashLight.enabled = true;

        yield return new WaitForSeconds(lightningBoltDurationSeconds);
        HideLightningRuntimeObjects();
    }

    private void ResolveLightningReferences()
    {
        if (lightningLine != null && lightningFlashLight != null)
        {
            ConfigureLightningReferences();
            return;
        }

        int activeSceneHandle = SceneManager.GetActiveScene().handle;
        if (lastLightningResolveSceneHandle == activeSceneHandle && lightningRoot == null)
            return;

        lastLightningResolveSceneHandle = activeSceneHandle;

        if (lightningRoot == null && !string.IsNullOrWhiteSpace(lightningEffectObjectName))
        {
            string targetName = lightningEffectObjectName.Trim();
            Transform[] transforms = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && string.Equals(candidate.name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    lightningRoot = candidate;
                    break;
                }
            }
        }

        if (lightningRoot == null)
            return;

        if (lightningLine == null)
            lightningLine = lightningRoot.GetComponentInChildren<LineRenderer>(true);

        if (lightningFlashLight == null)
            lightningFlashLight = lightningRoot.GetComponentInChildren<Light>(true);

        ConfigureLightningReferences();
        HideLightningRuntimeObjects();
    }

    private void ConfigureLightningReferences()
    {
        if (lightningLine != null)
        {
            lightningLine.useWorldSpace = true;
            lightningLine.textureMode = LineTextureMode.Stretch;
            lightningLine.alignment = LineAlignment.View;
            lightningLine.startWidth = lightningWidth;
            lightningLine.endWidth = lightningWidth * 0.35f;
            lightningLine.startColor = lightningColor;
            lightningLine.endColor = new Color(lightningColor.r, lightningColor.g, lightningColor.b, 0.15f);
        }

        if (lightningFlashLight != null)
        {
            lightningFlashLight.type = LightType.Point;
            lightningFlashLight.shadows = LightShadows.None;
            lightningFlashLight.color = lightningColor;
            lightningFlashLight.range = lightningFlashRange;
            lightningFlashLight.intensity = lightningFlashIntensity;
        }
    }

    private Vector3 ResolveWorldEffectOrigin()
    {
        if (PlayerStats.instance != null)
            return PlayerStats.instance.transform.position;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            return mainCamera.transform.position;

        return transform.position;
    }

    private void StopLightningEffect()
    {
        if (lightningRoutine != null)
        {
            StopCoroutine(lightningRoutine);
            lightningRoutine = null;
        }

        HideLightningRuntimeObjects();
    }

    private void HideLightningRuntimeObjects()
    {
        if (lightningLine != null)
            lightningLine.enabled = false;

        if (lightningFlashLight != null)
            lightningFlashLight.enabled = false;

        if (lightningRoot != null && lightningRoot.gameObject.activeSelf)
            lightningRoot.gameObject.SetActive(false);
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
