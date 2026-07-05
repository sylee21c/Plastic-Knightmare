using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 이 오브젝트는 밤에만 나타남.
// 낮 → 밤: 페이드 인. 밤 → 낮: 페이드 아웃.
// DayNightManager 의 transitionDuration 에 자동 동기화.
public sealed class NightOnlyFader : MonoBehaviour
{
    [Header("Fade")]
    [Tooltip("비어있으면 DayNightManager.transitionDuration 을 자동 사용")]
    [SerializeField] private float overrideFadeDuration = 0f;
    [Tooltip("게임 시작 시 낮이면 즉시 투명하게")]
    [SerializeField] private bool startInvisibleOnDay = true;

    [Header("Debug")]
    [SerializeField] private bool logShaderInfo = false;

    private Renderer[] renderers;
    private Material[] cachedMaterials;
    private Coroutine activeFade;
    private DayNightManager.Phase lastPhase = DayNightManager.Phase.Day;
    private float currentAlpha = 1f;

    private void Awake()
    {
        // 자식 포함 모든 렌더러 수집 → 각 렌더러의 sub-material 인스턴스 캐시
        renderers = GetComponentsInChildren<Renderer>(true);
        List<Material> mats = new List<Material>();
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            foreach (Material mat in r.materials) // 인스턴스 자동 생성 (다른 오브젝트 영향 X)
            {
                ForceTransparentSurface(mat);
                mats.Add(mat);
            }
        }
        cachedMaterials = mats.ToArray();
    }

    private void Start()
    {
        DayNightManager mgr = DayNightManager.Instance;
        DayNightManager.Phase phase = mgr != null ? mgr.CurrentPhase : DayNightManager.Phase.Day;
        lastPhase = phase;

        float initialAlpha = phase == DayNightManager.Phase.Night ? 1f : (startInvisibleOnDay ? 0f : 1f);
        ApplyVisibility(initialAlpha);
        SetRenderersEnabled(initialAlpha > 0.001f);
    }

    private void Update()
    {
        DayNightManager mgr = DayNightManager.Instance;
        if (mgr == null) return;

        if (mgr.CurrentPhase != lastPhase)
        {
            lastPhase = mgr.CurrentPhase;
            float target = lastPhase == DayNightManager.Phase.Night ? 1f : 0f;
            StartFade(target, GetFadeDuration(mgr));
        }
    }

    private float GetFadeDuration(DayNightManager mgr)
    {
        if (overrideFadeDuration > 0f) return overrideFadeDuration;
        if (mgr != null && mgr.TransitionDuration > 0f) return mgr.TransitionDuration;
        return 3f;
    }

    private void StartFade(float target, float duration)
    {
        if (activeFade != null) StopCoroutine(activeFade);
        SetRenderersEnabled(true); // 페이드 진행 중엔 항상 켬
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
        if (to <= 0.001f) SetRenderersEnabled(false); // 완전 사라졌으면 렌더 비활성
        activeFade = null;
    }

    // ── 시각적 페이드 적용 (알파만, 스케일은 절대 건드리지 않음) ────

    // visibility: 0=hidden, 1=visible
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    private void ApplyVisibility(float visibility)
    {
        currentAlpha = visibility;

        if (cachedMaterials == null) return;

        foreach (Material mat in cachedMaterials)
        {
            if (mat == null) continue;
            if (mat.HasProperty(BaseColorId))
            {
                Color c = mat.GetColor(BaseColorId);
                c.a = visibility;
                mat.SetColor(BaseColorId, c);
            }
            if (mat.HasProperty(ColorId))
            {
                Color c = mat.GetColor(ColorId);
                c.a = visibility;
                mat.SetColor(ColorId, c);
            }
            if (mat.HasProperty(TintColorId))
            {
                Color c = mat.GetColor(TintColorId);
                c.a = visibility;
                mat.SetColor(TintColorId, c);
            }
        }
    }

    private void SetRenderersEnabled(bool enabled)
    {
        if (renderers == null) return;
        foreach (Renderer r in renderers)
            if (r != null) r.enabled = enabled;
    }

    // URP Lit / Built-in Standard 를 Transparent 모드로 강제 → 알파값 실제 반영
    // 알파 미지원 커스텀 셰이더면 URP Lit 로 자동 교체.
    private void ForceTransparentSurface(Material mat)
    {
        if (mat == null) return;

        if (logShaderInfo && mat.shader != null)
            Debug.Log($"[NightOnlyFader] Material '{mat.name}' shader before: {mat.shader.name}");

        if (mat.HasProperty("_Surface"))
        {
            SetupUrpTransparent(mat);
            return;
        }
        if (mat.HasProperty("_Mode"))
        {
            SetupBuiltinFade(mat);
            return;
        }

        // 알파 지원 프로퍼티가 하나도 없는 셰이더 → URP Lit 로 교체하고 텍스처/컬러 복원
        Shader replacement = Shader.Find("Universal Render Pipeline/Lit");
        if (replacement == null) replacement = Shader.Find("Standard");
        if (replacement == null)
        {
            if (logShaderInfo)
                Debug.LogWarning($"[NightOnlyFader] '{mat.name}' 셰이더가 알파 페이드를 미지원, 대체 셰이더도 못 찾음.");
            return;
        }

        Texture mainTex = null;
        if (mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
        else if (mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");

        Color mainColor = Color.white;
        if (mat.HasProperty("_BaseColor")) mainColor = mat.GetColor("_BaseColor");
        else if (mat.HasProperty("_Color")) mainColor = mat.color;
        else if (mat.HasProperty("_TintColor")) mainColor = mat.GetColor("_TintColor");

        mat.shader = replacement;

        if (mainTex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mainTex);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mainColor);
        if (mat.HasProperty("_Color")) mat.color = mainColor;

        if (mat.HasProperty("_Surface")) SetupUrpTransparent(mat);
        else if (mat.HasProperty("_Mode")) SetupBuiltinFade(mat);

        if (logShaderInfo)
            Debug.Log($"[NightOnlyFader] Material '{mat.name}' shader after swap: {mat.shader.name}");
    }

    private static void SetupUrpTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f); // 0=Opaque, 1=Transparent
        if (mat.HasProperty("_Blend"))     mat.SetFloat("_Blend", 0f);     // Alpha
        if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite"))   mat.SetInt("_ZWrite", 0);

        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");

        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static void SetupBuiltinFade(Material mat)
    {
        mat.SetFloat("_Mode", 2f); // Fade
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }
}
