using System.Collections;
using UnityEngine;

public sealed class DayNightManager : MonoBehaviour
{
    public enum Phase { Day, Night }

    public static DayNightManager Instance { get; private set; }

    [Header("Lighting - Day")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private Color dayLightColor = new Color(1f, 0.96f, 0.84f, 1f);
    [SerializeField] private float dayLightIntensity = 1.2f;
    [SerializeField] private Color dayAmbientColor = new Color(0.45f, 0.49f, 0.55f, 1f);

    [Header("Lighting - Night")]
    [SerializeField] private Color nightLightColor = new Color(0.18f, 0.22f, 0.45f, 1f);
    [SerializeField] private float nightLightIntensity = 0.12f;
    [SerializeField] private Color nightAmbientColor = new Color(0.04f, 0.04f, 0.12f, 1f);

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 3f;

    public Phase CurrentPhase { get; private set; } = Phase.Day;
    public int DayCount { get; private set; } = 1;

    public event System.Action OnNightBegin;
    public event System.Action OnDayBegin;

    private BuildingModeController buildingController;
    private Canvas hotbarCanvas;
    private Coroutine activeTransition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        buildingController = FindAnyObjectByType<BuildingModeController>();

        BuildingHotbarUI hotbarUI = FindAnyObjectByType<BuildingHotbarUI>();
        if (hotbarUI != null)
        {
            hotbarCanvas = hotbarUI.GetComponent<Canvas>();
            if (hotbarCanvas == null)
                hotbarCanvas = hotbarUI.GetComponentInParent<Canvas>();
        }

        if (directionalLight == null)
            directionalLight = FindAnyObjectByType<Light>();

        ApplyPhaseInstant(Phase.Day);
    }

    // 태블릿 UI의 완료 버튼에서 호출
    public void BeginNight()
    {
        if (CurrentPhase == Phase.Night) return;
        if (activeTransition != null) StopCoroutine(activeTransition);
        activeTransition = StartCoroutine(TransitionRoutine(Phase.Night));
    }

    // 밤 종료 (전투 완료) 시 호출
    public void BeginDay()
    {
        if (CurrentPhase == Phase.Day) return;
        DayCount++;
        if (activeTransition != null) StopCoroutine(activeTransition);
        activeTransition = StartCoroutine(TransitionRoutine(Phase.Day));
    }

    private IEnumerator TransitionRoutine(Phase targetPhase)
    {
        CurrentPhase = targetPhase;

        Color fromLight = directionalLight != null ? directionalLight.color : Color.white;
        float fromIntensity = directionalLight != null ? directionalLight.intensity : 1f;
        Color fromAmbient = RenderSettings.ambientLight;

        Color toLight = targetPhase == Phase.Night ? nightLightColor : dayLightColor;
        float toIntensity = targetPhase == Phase.Night ? nightLightIntensity : dayLightIntensity;
        Color toAmbient = targetPhase == Phase.Night ? nightAmbientColor : dayAmbientColor;

        SetBuildingEnabled(targetPhase == Phase.Day);

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);

            if (directionalLight != null)
            {
                directionalLight.color = Color.Lerp(fromLight, toLight, t);
                directionalLight.intensity = Mathf.Lerp(fromIntensity, toIntensity, t);
            }
            RenderSettings.ambientLight = Color.Lerp(fromAmbient, toAmbient, t);

            yield return null;
        }

        activeTransition = null;

        if (targetPhase == Phase.Night)
        {
            OnNightBegin?.Invoke();
            // 이벤트 구독 실패 대비 직접 호출
            FindAnyObjectByType<GhostSpawner>()?.StartSpawning();
        }
        else
        {
            OnDayBegin?.Invoke();
            FindAnyObjectByType<GhostSpawner>()?.StopSpawning();
        }
    }

    private void SetBuildingEnabled(bool enabled)
    {
        if (buildingController != null)
            buildingController.enabled = enabled;

        if (hotbarCanvas != null)
            hotbarCanvas.gameObject.SetActive(enabled);
    }

    private void ApplyPhaseInstant(Phase phase)
    {
        bool isDay = phase == Phase.Day;

        if (directionalLight != null)
        {
            directionalLight.color = isDay ? dayLightColor : nightLightColor;
            directionalLight.intensity = isDay ? dayLightIntensity : nightLightIntensity;
        }

        RenderSettings.ambientLight = isDay ? dayAmbientColor : nightAmbientColor;
        SetBuildingEnabled(isDay);
    }
}
