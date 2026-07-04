using UnityEngine;
using UnityEngine.UI;

// 우측 상단 코인 UI.
// 씬에 UI 계층이 미리 있으면 그것을 사용, 없으면 자동 생성.
public sealed class CoinUI : MonoBehaviour
{
    public static void EnsureExists()
    {
        if (FindAnyObjectByType<CoinUI>() != null) return;
        GameObject go = new GameObject("CoinUI");
        go.AddComponent<CoinUI>();
    }

    [Header("Scene References (씬에서 드래그해서 연결)")]
    [SerializeField] private Text valueText;
    [SerializeField] private Image iconImage;

    [Header("Fallback: 씬 참조가 비어있을 때 자동 생성용")]
    [SerializeField] private Sprite coinIcon;
    [SerializeField] private string iconResourcePath = "UI/coin_icon";
    [SerializeField] private Vector2 iconSize = new Vector2(56f, 56f);
    [SerializeField] private Vector2 anchoredPosition = new Vector2(-40f, -40f);
    [SerializeField] private Vector2 panelSize = new Vector2(260f, 72f);
    [SerializeField] private int fontSize = 34;
    [SerializeField] private Color panelColor = new Color(0.05f, 0.05f, 0.08f, 0.78f);
    [SerializeField] private Color textColor = new Color(1f, 0.92f, 0.5f, 1f);

    private Canvas createdCanvas;

    private void Start()
    {
        // 씬에 텍스트 참조가 없으면 UI 계층 전체 자동 생성
        if (valueText == null)
        {
            BuildUI();
        }
        else
        {
            // 폰트가 씬에서 미지정이면 기본 폰트 자동 할당
            if (valueText.font == null)
                valueText.font = GetDefaultFont();

            // 아이콘 이미지가 있는데 sprite가 비어있으면 자동 로드 시도
            if (iconImage != null && iconImage.sprite == null)
            {
                if (coinIcon != null) iconImage.sprite = coinIcon;
                else if (!string.IsNullOrEmpty(iconResourcePath))
                    iconImage.sprite = Resources.Load<Sprite>(iconResourcePath);
            }
        }

        CoinWallet.EnsureExists();
        if (CoinWallet.Instance != null)
        {
            CoinWallet.Instance.OnCoinsChanged += UpdateText;
            UpdateText(CoinWallet.Instance.Coins);
        }
        else
        {
            UpdateText(0);
        }
    }

    private void OnDestroy()
    {
        if (CoinWallet.Instance != null)
            CoinWallet.Instance.OnCoinsChanged -= UpdateText;
        if (createdCanvas != null) Destroy(createdCanvas.gameObject);
    }

    private void UpdateText(int amount)
    {
        if (valueText != null)
            valueText.text = amount.ToString("N0");
    }

    // ── 자동 생성 폴백 ────────────────────────────────────────────

    private void BuildUI()
    {
        if (coinIcon == null && !string.IsNullOrEmpty(iconResourcePath))
            coinIcon = Resources.Load<Sprite>(iconResourcePath);

        GameObject canvasObj = new GameObject("Coin UI Canvas");
        createdCanvas = canvasObj.AddComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        createdCanvas.sortingOrder = 45;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Coin Panel");
        panel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = panelSize;

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = panelColor;
        panelBg.raycastTarget = false;

        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        GameObject icon = new GameObject("Coin Icon");
        icon.transform.SetParent(panel.transform, false);
        RectTransform iconRect = icon.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(10f, 0f);
        iconRect.sizeDelta = iconSize;
        iconImage = icon.AddComponent<Image>();
        iconImage.sprite = coinIcon;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        if (coinIcon == null) iconImage.color = new Color(1f, 0.85f, 0.2f, 1f);

        GameObject value = new GameObject("Value");
        value.transform.SetParent(panel.transform, false);
        RectTransform valueRect = value.AddComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(0.5f, 0.5f);
        valueRect.offsetMin = new Vector2(iconSize.x + 20f, 0f);
        valueRect.offsetMax = new Vector2(-14f, 0f);
        valueText = value.AddComponent<Text>();
        valueText.alignment = TextAnchor.MiddleRight;
        valueText.font = GetDefaultFont();
        valueText.fontSize = fontSize;
        valueText.fontStyle = FontStyle.Bold;
        valueText.color = textColor;
        valueText.raycastTarget = false;

        Shadow sh = value.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sh.effectDistance = new Vector2(2f, -2f);
    }

    private static Font GetDefaultFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
