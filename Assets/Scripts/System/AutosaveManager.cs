using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class AutosaveManager : MonoBehaviour
{
    [Header("Autosave")]
    [SerializeField, Min(30f)] private float periodicIntervalSeconds = 120f;
    [SerializeField, Min(0f)] private float floorEntryDelaySeconds = 0.35f;

    [Header("Feedback UI")]
    [SerializeField, Min(0.1f)] private float indicatorMinimumSeconds = 1.875f;
    [SerializeField, Min(1)] private int indicatorCycles = 3;
    [SerializeField] private GameObject savingIndicator;
    [SerializeField] private Animator savingAnimator;

    private CoreGenerator subscribedGenerator;
    private Coroutine autosaveRoutine;
    private float activeGameplaySeconds;
    private bool saveRequestedWhileBusy;

    private void Awake()
    {
        SetIndicatorVisible(false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SubscribeToCurrentGenerator();
    }

    private void Start()
    {
        // OnEnable avviene prima degli Start della scena: questo secondo tentativo
        // copre anche generatori creati dinamicamente durante l'inizializzazione.
        SubscribeToCurrentGenerator();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnsubscribeFromGenerator();

        if (autosaveRoutine != null)
        {
            StopCoroutine(autosaveRoutine);
            autosaveRoutine = null;
        }

        saveRequestedWhileBusy = false;
        SetIndicatorVisible(false);
    }

    private void Update()
    {
        if (subscribedGenerator != CoreGenerator.Instance)
            SubscribeToCurrentGenerator();

        // Il timer usa tempo reale: l'autosave resta attivo anche durante la pausa,
        // ma solo finche' e' presente un dungeon valido.
        if (subscribedGenerator == null)
            return;

        activeGameplaySeconds += Time.unscaledDeltaTime;
        if (activeGameplaySeconds < periodicIntervalSeconds)
            return;

        activeGameplaySeconds = 0f;
        RequestAutosave(0f);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        activeGameplaySeconds = 0f;
        SubscribeToCurrentGenerator();
    }

    private void HandleFloorGenerated(int floor)
    {
        activeGameplaySeconds = 0f;
        RequestAutosave(floorEntryDelaySeconds);
    }

    private void SubscribeToCurrentGenerator()
    {
        CoreGenerator currentGenerator = CoreGenerator.Instance;
        if (subscribedGenerator == currentGenerator)
            return;

        UnsubscribeFromGenerator();
        subscribedGenerator = currentGenerator;

        if (subscribedGenerator != null)
            subscribedGenerator.FloorGenerated += HandleFloorGenerated;
    }

    private void UnsubscribeFromGenerator()
    {
        if (subscribedGenerator != null)
            subscribedGenerator.FloorGenerated -= HandleFloorGenerated;

        subscribedGenerator = null;
    }

    private void RequestAutosave(float delaySeconds)
    {
        if (autosaveRoutine != null)
        {
            saveRequestedWhileBusy = true;
            return;
        }

        autosaveRoutine = StartCoroutine(PerformAutosave(delaySeconds));
    }

    private IEnumerator PerformAutosave(float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        SetIndicatorVisible(true);
        ResetIndicatorAnimation();
        yield return null;

        PlayerStats stats = PlayerStats.instance;
        if (stats != null)
            stats.SaveStatsImmediate();
        else
            Debug.LogWarning("[AutosaveManager] PlayerStats non disponibile: autosave ignorato.");

        yield return WaitForIndicatorCycles();
        SetIndicatorVisible(false);
        autosaveRoutine = null;

        if (!saveRequestedWhileBusy)
            yield break;

        saveRequestedWhileBusy = false;
        autosaveRoutine = StartCoroutine(PerformAutosave(0f));
    }

    private void SetIndicatorVisible(bool visible)
    {
        if (savingIndicator != null && savingIndicator.activeSelf != visible)
            savingIndicator.SetActive(visible);
    }

    private void ResetIndicatorAnimation()
    {
        if (savingAnimator == null || !savingAnimator.isActiveAndEnabled)
            return;

        savingAnimator.Play(0, 0, 0f);
        savingAnimator.Update(0f);
    }

    private IEnumerator WaitForIndicatorCycles()
    {
        if (savingAnimator == null || !savingAnimator.isActiveAndEnabled)
        {
            yield return new WaitForSecondsRealtime(indicatorMinimumSeconds);
            yield break;
        }

        AnimatorStateInfo state = savingAnimator.GetCurrentAnimatorStateInfo(0);
        int targetCycles = Mathf.Max(1, indicatorCycles);
        float cycleLength = Mathf.Max(0.1f, state.length);
        float timeout = cycleLength * targetCycles + 1f;
        float startedAt = Time.realtimeSinceStartup;

        while (savingAnimator != null && savingAnimator.isActiveAndEnabled)
        {
            state = savingAnimator.GetCurrentAnimatorStateInfo(0);
            if (state.normalizedTime >= targetCycles)
                yield break;

            if (Time.realtimeSinceStartup - startedAt >= timeout)
            {
                Debug.LogWarning("[AutosaveManager] Timeout durante l'animazione di salvataggio.");
                yield break;
            }

            yield return null;
        }
    }
}
