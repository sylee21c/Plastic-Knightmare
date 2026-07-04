using UnityEngine;
using UnityEngine.UI;

// 대상 오브젝트 위에 월드 스페이스 HP 바를 표시.
// 캔버스를 부모에 붙이지 않는 독립 오브젝트 → 부모 스케일 무관.
[RequireComponent(typeof(Damageable))]
public sealed class HealthBar : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private Vector2 pixelSize = new Vector2(70f, 8f);
    [SerializeField] private float worldScale = 0.008f;

    [Header("Visibility")]
    [SerializeField] private bool hideDuringDay = true;
    [SerializeField] private bool hideWhenFull = true;

    [Header("Animation")]
    [Tooltip("초당 비율 감소 속도. 2.5면 100%→0% 가 0.4초. 값 클수록 빠름.")]
    [SerializeField] private float animationSpeed = 2.5f;

    [Header("Colors")]
    [SerializeField] private Color fillColor = new Color(0.35f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color midColor  = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private Color lowColor  = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.78f);

    private Damageable damageable;
    private Canvas canvas;
    private Image fillImage;
    private Camera cachedCamera;

    // 애니메이션용
    private float displayedFill = 1f;
    private float targetFill = 1f;

    private static Sprite whiteSprite;
    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        Texture2D tex = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        whiteSprite.name = "HealthBar_White";
        return whiteSprite;
    }

    private void Awake() { damageable = GetComponent<Damageable>(); }

    private void Start()
    {
        BuildBar();
        damageable.OnHealthChanged += HandleChanged;
        damageable.OnDeath += HandleDeath;

        // 초기값은 스냅 (스폰 시 0 → 1 로 채워지는 애니 방지)
        float initial = damageable.MaxHealth > 0f
            ? Mathf.Clamp01(damageable.CurrentHealth / damageable.MaxHealth) : 0f;
        displayedFill = initial;
        targetFill = initial;
        ApplyFillVisual();
    }

    private void OnDestroy()
    {
        if (damageable != null)
        {
            damageable.OnHealthChanged -= HandleChanged;
            damageable.OnDeath -= HandleDeath;
        }
        if (canvas != null) Destroy(canvas.gameObject);
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        if (cachedCamera == null) cachedCamera = Camera.main;
        if (cachedCamera == null) return;

        // 애니메이션: displayedFill 을 targetFill 로 이동
        if (!Mathf.Approximately(displayedFill, targetFill))
        {
            displayedFill = Mathf.MoveTowards(displayedFill, targetFill, animationSpeed * Time.deltaTime);
            ApplyFillVisual();
        }

        bool visible = ShouldShow();
        if (canvas.gameObject.activeSelf != visible)
            canvas.gameObject.SetActive(visible);
        if (!visible) return;

        canvas.transform.position = transform.position + worldOffset;
        canvas.transform.rotation = cachedCamera.transform.rotation;
    }

    private bool ShouldShow()
    {
        if (damageable == null || damageable.IsDead) return false;

        if (hideDuringDay && DayNightManager.Instance != null
            && DayNightManager.Instance.CurrentPhase == DayNightManager.Phase.Day)
            return false;

        if (hideWhenFull && damageable.MaxHealth > 0f
            && damageable.CurrentHealth >= damageable.MaxHealth - 0.01f)
            return false;

        return true;
    }

    private void BuildBar()
    {
        GameObject barObj = new GameObject($"HP Bar ({name})");
        canvas = barObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 30;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = pixelSize;
        canvasRect.localScale = Vector3.one * worldScale;

        Sprite sp = GetWhiteSprite();

        // Background
        GameObject bg = new GameObject("BG");
        bg.transform.SetParent(barObj.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.sprite = sp;
        bgImg.color = backgroundColor;
        bgImg.raycastTarget = false;
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Fill — 부모에 꽉 차게 + Image.Filled + 화이트 스프라이트로 잘림 보장
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(barObj.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(-1f, -1f);

        fillImage = fill.AddComponent<Image>();
        fillImage.sprite = sp;
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;
    }

    private void HandleChanged(float current, float max)
    {
        // 목표만 갱신, 실제 fillImage 는 LateUpdate 에서 서서히 이동
        targetFill = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    private void ApplyFillVisual()
    {
        if (fillImage == null) return;
        fillImage.fillAmount = displayedFill;
        if (displayedFill > 0.6f)      fillImage.color = fillColor;
        else if (displayedFill > 0.3f) fillImage.color = midColor;
        else                            fillImage.color = lowColor;
    }

    private void HandleDeath()
    {
        if (canvas != null) canvas.gameObject.SetActive(false);
    }

    public void Configure(Vector3 offset, Vector2 pixel, float scale)
    {
        worldOffset = offset;
        pixelSize = pixel;
        worldScale = scale;
        if (canvas != null)
        {
            RectTransform r = canvas.GetComponent<RectTransform>();
            r.sizeDelta = pixel;
            r.localScale = Vector3.one * scale;
            if (damageable != null)
                HandleChanged(damageable.CurrentHealth, damageable.MaxHealth);
        }
    }
}
