using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class NightOnlyFader : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private float overrideFadeDuration = 0f;
    [SerializeField] private bool startInvisibleOnDay = true;
    [SerializeField] private bool useMaterialFade = false;

    [Header("Debug")]
    [SerializeField] private bool logShaderInfo = false;

    private Renderer[] renderers;
    private Material[] cachedMaterials;
    private Animator[] animators;
    private Coroutine activeFade;
    private DayNightManager.Phase lastPhase = DayNightManager.Phase.Day;
    private float currentAlpha = 1f;
    private bool subscribedToPhaseEvents;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        animators = GetComponentsInChildren<Animator>(true);

        foreach (Animator animator in animators)
        {
            if (animator != null)
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        List<Material> mats = new List<Material>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            foreach (Material material in renderer.materials)
            {
                if (useMaterialFade)
                    ForceTransparentSurface(material);
                mats.Add(material);
            }
        }

        cachedMaterials = mats.ToArray();
    }

    private void Start()
    {
        TrySubscribeToPhaseEvents();

        DayNightManager manager = DayNightManager.Instance;
        DayNightManager.Phase phase = manager != null
            ? manager.CurrentPhase
            : DayNightManager.Phase.Day;

        lastPhase = phase;
        bool visible = phase == DayNightManager.Phase.Night || !startInvisibleOnDay;
        ApplyVisibility(visible ? 1f : 0f);
        SetRenderersEnabled(visible);
    }

    private void Update()
    {
        TrySubscribeToPhaseEvents();

        DayNightManager manager = DayNightManager.Instance;
        if (manager == null) return;

        if (manager.CurrentPhase != lastPhase)
        {
            lastPhase = manager.CurrentPhase;
            float target = lastPhase == DayNightManager.Phase.Night ? 1f : 0f;
            StartFade(target, GetFadeDuration(manager));
        }

        if (manager.CurrentPhase == DayNightManager.Phase.Night
            && activeFade == null
            && (!AnyRendererEnabled() || currentAlpha < 0.999f))
        {
            SetFullyVisible();
        }
    }

    private void OnDestroy()
    {
        if (subscribedToPhaseEvents && DayNightManager.Instance != null)
        {
            DayNightManager.Instance.OnNightBegin -= HandleNightBegin;
            DayNightManager.Instance.OnDayBegin -= HandleDayBegin;
        }

        subscribedToPhaseEvents = false;
    }

    private void TrySubscribeToPhaseEvents()
    {
        if (subscribedToPhaseEvents || DayNightManager.Instance == null) return;

        DayNightManager.Instance.OnNightBegin += HandleNightBegin;
        DayNightManager.Instance.OnDayBegin += HandleDayBegin;
        subscribedToPhaseEvents = true;
    }

    private void HandleNightBegin()
    {
        lastPhase = DayNightManager.Phase.Night;
        SetFullyVisible();
    }

    private void HandleDayBegin()
    {
        lastPhase = DayNightManager.Phase.Day;
        StartFade(0f, GetFadeDuration(DayNightManager.Instance));
    }

    private void SetFullyVisible()
    {
        if (activeFade != null)
        {
            StopCoroutine(activeFade);
            activeFade = null;
        }

        ApplyVisibility(1f);
        SetRenderersEnabled(true);
    }

    private bool AnyRendererEnabled()
    {
        if (renderers == null) return false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.enabled)
                return true;
        }

        return false;
    }

    private float GetFadeDuration(DayNightManager manager)
    {
        if (overrideFadeDuration > 0f) return overrideFadeDuration;
        if (manager != null && manager.TransitionDuration > 0f)
            return manager.TransitionDuration;
        return 3f;
    }

    private void StartFade(float target, float duration)
    {
        if (!useMaterialFade)
        {
            currentAlpha = target;
            SetRenderersEnabled(target > 0.001f);
            return;
        }

        if (activeFade != null)
            StopCoroutine(activeFade);

        SetRenderersEnabled(true);
        activeFade = StartCoroutine(FadeRoutine(currentAlpha, target, duration));
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            ApplyVisibility(Mathf.Lerp(from, to, t));
            yield return null;
        }

        ApplyVisibility(to);
        if (to <= 0.001f)
            SetRenderersEnabled(false);

        activeFade = null;
    }

    private void ApplyVisibility(float visibility)
    {
        currentAlpha = visibility;
        if (!useMaterialFade || cachedMaterials == null) return;

        foreach (Material material in cachedMaterials)
        {
            if (material == null) continue;

            if (material.HasProperty(BaseColorId))
            {
                Color color = material.GetColor(BaseColorId);
                color.a = visibility;
                material.SetColor(BaseColorId, color);
            }

            if (material.HasProperty(ColorId))
            {
                Color color = material.GetColor(ColorId);
                color.a = visibility;
                material.SetColor(ColorId, color);
            }

            if (material.HasProperty(TintColorId))
            {
                Color color = material.GetColor(TintColorId);
                color.a = visibility;
                material.SetColor(TintColorId, color);
            }
        }
    }

    private void SetRenderersEnabled(bool visible)
    {
        if (renderers == null) return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    private void ForceTransparentSurface(Material material)
    {
        if (material == null) return;

        if (logShaderInfo && material.shader != null)
            Debug.Log($"[NightOnlyFader] Material '{material.name}' shader before: {material.shader.name}");

        if (material.HasProperty("_Surface"))
        {
            SetupUrpTransparent(material);
            return;
        }

        if (material.HasProperty("_Mode"))
        {
            SetupBuiltinFade(material);
            return;
        }

        if (logShaderInfo)
            Debug.LogWarning($"[NightOnlyFader] Material '{material.name}' does not expose a supported alpha property.");
    }

    private static void SetupUrpTransparent(Material material)
    {
        material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);

        material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_ALPHABLEND_ON");

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static void SetupBuiltinFade(Material material)
    {
        material.SetFloat("_Mode", 2f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }
}
