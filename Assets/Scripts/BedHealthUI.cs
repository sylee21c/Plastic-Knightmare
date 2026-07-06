using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 침대의 HP를 화면 하단 가운데에 항상 고정으로 표시.
// Bed 오브젝트가 아닌 어디에 붙여도 되고, damageable을 자동 탐색함.
public sealed class BedHealthUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Damageable damageable;
    [SerializeField] private string bedTag = "Bed";
    [SerializeField] private string bedName = "Bed";
    [SerializeField] private string label = "침대 HP";

    [Header("Layout")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, 96f);
    [SerializeField] private Vector2 barSize = new Vector2(440f, 24f);

    [Header("Colors")]
    [SerializeField] private Color fillColor = new Color(0.35f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color midColor  = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private Color lowColor  = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Animation")]
    [Tooltip("초당 비율 감소 속도")]
    [SerializeField] private float animationSpeed = 2.5f;

    [Header("Label Font")]
    [SerializeField] private TMP_FontAsset labelFontAsset;
    [SerializeField] private string labelFontResourcePath = "Fonts & Materials/온글잎 박다현체 SDF";

    private Canvas canvas;
    private Image fillImage;
    private RectTransform fillRect;
    private Text valueText;

    // 애니메이션
    private float displayedFill = 1f;
    private float targetFill = 1f;

    private void Start()
    {
        FindDamageable();
        BuildUI();

        if (damageable != null)
        {
            damageable.OnHealthChanged += UpdateBar;
            float initial = damageable.MaxHealth > 0f
                ? Mathf.Clamp01(damageable.CurrentHealth / damageable.MaxHealth) : 0f;
            displayedFill = initial;
            targetFill = initial;
            ApplyFillVisual();
        }
    }

    private void Update()
    {
        if (fillImage == null) return;

        // 밤에만 표시
        bool isNight = DayNightManager.Instance != null
                       && DayNightManager.Instance.CurrentPhase == DayNightManager.Phase.Night;
        if (canvas != null && canvas.gameObject.activeSelf != isNight)
            canvas.gameObject.SetActive(isNight);

        if (!Mathf.Approximately(displayedFill, targetFill))
        {
            displayedFill = Mathf.MoveTowards(displayedFill, targetFill, animationSpeed * Time.deltaTime);
            ApplyFillVisual();
        }
    }

    private void OnDestroy()
    {
        if (damageable != null) damageable.OnHealthChanged -= UpdateBar;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    private void FindDamageable()
    {
        if (damageable != null) return;

        GameObject bed = null;
        if (!string.IsNullOrEmpty(bedTag))
        {
            try { bed = GameObject.FindWithTag(bedTag); } catch { }
        }
        if (bed == null) bed = GameObject.Find(bedName);
        if (bed != null) damageable = bed.GetComponent<Damageable>();
    }

    private void BuildUI()
    {
        GameObject canvasObj = new GameObject("Bed Health Canvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObj.AddComponent<GraphicRaycaster>();

        // 하단 가운데 패널
        GameObject panel = new GameObject("Bed Health Panel");
        panel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = new Vector2(barSize.x + 28f, barSize.y + 34f);

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
        panelBg.raycastTarget = false;

        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        // 라벨
        GameObject lbl = new GameObject("Label");
        lbl.transform.SetParent(panel.transform, false);
        RectTransform lblRect = lbl.AddComponent<RectTransform>();
        lblRect.anchorMin = new Vector2(0f, 1f);
        lblRect.anchorMax = new Vector2(1f, 1f);
        lblRect.pivot = new Vector2(0.5f, 1f);
        lblRect.anchoredPosition = new Vector2(0f, -4f);
        lblRect.sizeDelta = new Vector2(0f, 20f);
        TMP_Text lblText = lbl.AddComponent<TextMeshProUGUI>();
        lblText.text = label;
        lblText.alignment = TextAlignmentOptions.Center;
        lblText.font = GetLabelFont();
        lblText.fontSize = 20f;
        lblText.fontStyle = FontStyles.Normal;
        lblText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        lblText.raycastTarget = false;

        // 바 배경
        GameObject barBg = new GameObject("Bar BG");
        barBg.transform.SetParent(panel.transform, false);
        RectTransform barBgRect = barBg.AddComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0.5f, 0f);
        barBgRect.anchorMax = new Vector2(0.5f, 0f);
        barBgRect.pivot = new Vector2(0.5f, 0f);
        barBgRect.anchoredPosition = new Vector2(0f, 8f);
        barBgRect.sizeDelta = barSize;
        Image barBgImg = barBg.AddComponent<Image>();
        barBgImg.color = new Color(0.02f, 0.02f, 0.02f, 0.9f);
        barBgImg.raycastTarget = false;

        // Fill — Image.Filled + 화이트 스프라이트
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(barBg.transform, false);
        fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        fillImage = fill.AddComponent<Image>();
        Texture2D wtex = Texture2D.whiteTexture;
        fillImage.sprite = Sprite.Create(wtex, new Rect(0, 0, wtex.width, wtex.height),
                                         new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;

        // 퍼센트 텍스트
        GameObject valObj = new GameObject("Value");
        valObj.transform.SetParent(barBg.transform, false);
        RectTransform valRect = valObj.AddComponent<RectTransform>();
        valRect.anchorMin = Vector2.zero;
        valRect.anchorMax = Vector2.one;
        valRect.offsetMin = Vector2.zero;
        valRect.offsetMax = Vector2.zero;
        valueText = valObj.AddComponent<Text>();
        valueText.alignment = TextAnchor.MiddleCenter;
        valueText.font = GetDefaultFont();
        valueText.fontSize = 14;
        valueText.fontStyle = FontStyle.Bold;
        valueText.color = Color.white;
        valueText.raycastTarget = false;
    }

    private void UpdateBar(float current, float max)
    {
        // 목표만 갱신, 실제 렌더링은 Update 에서 서서히
        targetFill = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    private void ApplyFillVisual()
    {
        if (fillImage == null) return;
        fillImage.fillAmount = displayedFill;

        if (displayedFill > 0.6f)      fillImage.color = fillColor;
        else if (displayedFill > 0.3f) fillImage.color = midColor;
        else                            fillImage.color = lowColor;

        if (valueText != null)
            valueText.text = $"{Mathf.CeilToInt(displayedFill * 100f)}%";
    }

    private static Font GetDefaultFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private TMP_FontAsset GetLabelFont()
    {
        if (labelFontAsset != null) return labelFontAsset;
        if (!string.IsNullOrEmpty(labelFontResourcePath))
            labelFontAsset = Resources.Load<TMP_FontAsset>(labelFontResourcePath);
        return labelFontAsset;
    }
}
