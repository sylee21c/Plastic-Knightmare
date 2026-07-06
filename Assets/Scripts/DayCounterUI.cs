using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 좌측 상단에 "Day N 낮" / "Day N 밤" 표시.
// 씬에 UI 계층이 있으면 그것을 사용, 없으면 코드로 자동 생성.
public sealed class DayCounterUI : MonoBehaviour
{
    [Header("Scene References (씬 UI 편집용)")]
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private Text legacyText;
    [SerializeField] private Image phaseIcon;

    [Header("Position Pin (앵커 문제 없이 좌상단 강제 고정)")]
    [Tooltip("이 오브젝트의 RectTransform 을 시작 시 강제로 좌상단 고정 (앵커/피벗 자동 세팅). 비우면 안 함.")]
    [SerializeField] private RectTransform pinnedRoot;
    [SerializeField] private Vector2 pinnedAnchoredPosition = new Vector2(40f, -40f);

    [Header("Phase Icons")]
    [SerializeField] private Sprite dayIcon;
    [SerializeField] private Sprite nightIcon;
    [SerializeField] private Vector2 iconSize = new Vector2(40f, 40f);
    [SerializeField] private Color dayIconTint = Color.white;
    [SerializeField] private Color nightIconTint = Color.white;

    [Header("Format")]
    [Tooltip("{0}=일수, {1}=페이즈 문자열. 예: \"Day {0}\" → \"Day 3\"")]
    [SerializeField] private string format = "Day {0}";
    [SerializeField] private string dayLabel = "낮";
    [SerializeField] private string nightLabel = "밤";

    [Header("Colors")]
    [SerializeField] private Color dayTextColor = new Color(1f, 0.9f, 0.55f, 1f);
    [SerializeField] private Color nightTextColor = new Color(0.65f, 0.75f, 1f, 1f);

    [Header("Fallback UI (씬 참조가 없을 때만 자동 생성)")]
    [Tooltip("체크하면 씬 UI가 없을 때 자동으로 캔버스를 생성. 기본은 OFF - 씬에 직접 UI 만들고 참조 연결 권장")]
    [SerializeField] private bool autoCreateUIIfMissing = false;
    [SerializeField] private Vector2 anchoredPosition = new Vector2(40f, -40f);
    [SerializeField] private Vector2 panelSize = new Vector2(240f, 68f);
    [SerializeField] private int fontSize = 32;
    [SerializeField] private Color panelColor = new Color(0.05f, 0.05f, 0.08f, 0.75f);

    private Canvas createdCanvas;
    private int lastDay = -1;
    private DayNightManager.Phase lastPhase = DayNightManager.Phase.Day;
    private bool phaseInitialized;

    private void Start()
    {
        if (tmpText == null && legacyText == null)
        {
            if (autoCreateUIIfMissing)
                BuildUI();
            else
                Debug.LogWarning("[DayCounterUI] Text 참조가 비어있음. 씬에 UI 만들고 Legacy Text 또는 TMP Text 필드에 드래그하거나, Auto Create UI If Missing 를 체크하세요.");
        }
        else if (legacyText != null && legacyText.font == null)
        {
            legacyText.font = GetDefaultFont();
        }

        AutoDetectPinnedRoot();
        NormalizePinnedCanvas();
        Refresh(true);
    }

    // 매 프레임 위치 강제 고정 → 뭐가 밀어내도 소용없음
    private void LateUpdate()
    {
        PinToTopLeft();
    }

    // pinnedRoot 안 채워져 있으면 씬 UI 계층에서 자동 탐색:
    // Text/Icon 이 붙은 Canvas 의 직속 자식 중 하나를 pinnedRoot 로 사용
    private void AutoDetectPinnedRoot()
    {
        if (pinnedRoot != null) return;

        Transform anchorSource = null;
        if (legacyText != null) anchorSource = legacyText.transform;
        else if (tmpText != null) anchorSource = tmpText.transform;
        else if (phaseIcon != null) anchorSource = phaseIcon.transform;

        if (anchorSource == null) return;

        // Canvas 를 만날 때까지 부모 타고 올라가면서, Canvas 직속 자식을 pinnedRoot 로 지정
        Transform cur = anchorSource;
        Transform lastChild = anchorSource;
        while (cur != null)
        {
            if (cur.GetComponent<Canvas>() != null)
            {
                pinnedRoot = lastChild as RectTransform;
                if (pinnedRoot == null) pinnedRoot = lastChild.GetComponent<RectTransform>();
                return;
            }
            lastChild = cur;
            cur = cur.parent;
        }

        // Canvas 못 찾으면 anchorSource 자체 사용
        pinnedRoot = anchorSource.GetComponent<RectTransform>();
    }

    // 지정한 RectTransform 을 좌상단에 확실히 고정 (앵커/피벗 강제 세팅)
    private void PinToTopLeft()
    {
        if (pinnedRoot == null) return;
        pinnedRoot.anchorMin = new Vector2(0f, 1f);
        pinnedRoot.anchorMax = new Vector2(0f, 1f);
        pinnedRoot.pivot = new Vector2(0f, 1f);
        if (pinnedRoot.anchoredPosition != pinnedAnchoredPosition)
            pinnedRoot.anchoredPosition = pinnedAnchoredPosition;
    }

    private void NormalizePinnedCanvas()
    {
        Canvas canvas = null;
        if (pinnedRoot != null)
            canvas = pinnedRoot.GetComponent<Canvas>();

        if (canvas != null)
            pinnedRoot = GetPrimaryVisualRoot();
        else if (pinnedRoot != null)
            canvas = pinnedRoot.GetComponentInParent<Canvas>();

        if (canvas == null) return;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 46);

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect != null && canvasRect.parent != null)
        {
            canvasRect.SetParent(null, false);
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one;
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = Vector2.zero;
        }
    }

    private RectTransform GetPrimaryVisualRoot()
    {
        Transform visual = null;
        if (tmpText != null) visual = tmpText.transform;
        else if (legacyText != null) visual = legacyText.transform;
        else if (phaseIcon != null) visual = phaseIcon.transform;

        return visual != null ? visual.GetComponent<RectTransform>() : pinnedRoot;
    }

    private void OnDestroy()
    {
        if (createdCanvas != null) Destroy(createdCanvas.gameObject);
    }

    private void Update()
    {
        Refresh(false);
    }

    private void Refresh(bool force)
    {
        DayNightManager mgr = DayNightManager.Instance;
        int day = mgr != null ? mgr.DayCount : 1;
        DayNightManager.Phase phase = mgr != null ? mgr.CurrentPhase : DayNightManager.Phase.Day;

        if (!force && phaseInitialized && day == lastDay && phase == lastPhase) return;
        lastDay = day;
        lastPhase = phase;
        phaseInitialized = true;

        string phaseText = phase == DayNightManager.Phase.Night ? nightLabel : dayLabel;
        string s = string.Format(format, day, phaseText);
        Color c = phase == DayNightManager.Phase.Night ? nightTextColor : dayTextColor;

        if (tmpText != null)   { tmpText.text = s;   tmpText.color = c; }
        if (legacyText != null){ legacyText.text = s; legacyText.color = c; }

        // 아이콘 교체 + 색 틴트
        if (phaseIcon != null)
        {
            Sprite sp = phase == DayNightManager.Phase.Night ? nightIcon : dayIcon;
            if (sp != null)
            {
                phaseIcon.sprite = sp;
                phaseIcon.enabled = true;
                phaseIcon.color = phase == DayNightManager.Phase.Night ? nightIconTint : dayIconTint;
            }
            else
            {
                phaseIcon.enabled = false;
            }
        }
    }

    // ── 자동 생성 폴백 ────────────────────────────────────────────

    private void BuildUI()
    {
        GameObject canvasObj = new GameObject("Day Counter Canvas");
        createdCanvas = canvasObj.AddComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        createdCanvas.sortingOrder = 46;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Day Panel");
        panel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = panelSize;

        Image bg = panel.AddComponent<Image>();
        bg.color = panelColor;
        bg.raycastTarget = false;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        // 텍스트 (좌측)
        GameObject textObj = new GameObject("Value");
        textObj.transform.SetParent(panel.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(14f, 0f);
        textRect.offsetMax = new Vector2(-(iconSize.x + 18f), 0f);

        legacyText = textObj.AddComponent<Text>();
        legacyText.alignment = TextAnchor.MiddleLeft;
        legacyText.font = GetDefaultFont();
        legacyText.fontSize = fontSize;
        legacyText.fontStyle = FontStyle.Bold;
        legacyText.color = dayTextColor;
        legacyText.raycastTarget = false;

        Shadow sh = textObj.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sh.effectDistance = new Vector2(2f, -2f);

        // 아이콘 (우측)
        GameObject iconObj = new GameObject("Phase Icon");
        iconObj.transform.SetParent(panel.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(1f, 0.5f);
        iconRect.anchorMax = new Vector2(1f, 0.5f);
        iconRect.pivot = new Vector2(1f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-10f, 0f);
        iconRect.sizeDelta = iconSize;

        phaseIcon = iconObj.AddComponent<Image>();
        phaseIcon.preserveAspect = true;
        phaseIcon.raycastTarget = false;
        // 스프라이트 없으면 기본은 숨김 → Refresh에서 활성/스프라이트 처리
        phaseIcon.enabled = false;
    }

    private static Font GetDefaultFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
