using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DayOneTutorialHints : MonoBehaviour
{
    [Header("Messages")]
    [TextArea(3, 6)]
    [SerializeField] private string introMessage = "W/A/S/D로 이동\nQ/E로 시점 전환\n모니터로 이동해보세요";
    [TextArea(4, 8)]
    [SerializeField] private string firstPurchaseMessage = "장난감을 배치해보세요.\n1~5: 장난감 슬롯\nR: 장난감 회전\n마우스 좌클릭: 장난감 설치\n마우스 우클릭: 장난감 해체";
    [TextArea(2, 5)]
    [SerializeField] private string readyMessage = "전투 준비가 완료되었다면\n모니터에서 [준비 완료!] 버튼을 클릭하세요";
    [TextArea(2, 5)]
    [SerializeField] private string firstNightMessage = "첫날 밤이 시작되었습니다.\n유령이 침대에 도달하지 못하게 막아보세요.";

    [Header("Timing")]
    [SerializeField, Min(0f)] private float introDuration = 3f;
    [SerializeField, Min(0f)] private float firstPurchaseDuration = 6f;
    [SerializeField, Min(0f)] private float readyPromptDelay = 10f;
    [SerializeField, Min(0f)] private float readyPromptDuration = 5f;
    [SerializeField, Min(0f)] private float firstNightDuration = 4f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

    [Header("Layout")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, -95f);
    [SerializeField] private Vector2 panelSize = new Vector2(780f, 190f);
    [SerializeField] private Vector2 textPadding = new Vector2(42f, 28f);
    [SerializeField, Min(1f)] private float fontSize = 34f;

    [Header("Style")]
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color borderColor = new Color(0f, 0f, 0f, 0.95f);
    [SerializeField] private Vector2 borderDistance = new Vector2(4f, -4f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private TMP_FontAsset fontAsset;

    private CanvasGroup canvasGroup;
    private RectTransform panelRect;
    private Image panelImage;
    private Outline panelOutline;
    private TMP_Text hintText;
    private Coroutine activeHint;
    private bool firstPurchaseShown;
    private bool firstNightShown;
    private bool subscribedToNightBegin;

    private void Awake()
    {
        BuildUi();
        ApplyStyle();
        HideInstant();
    }

    private void OnEnable()
    {
        BrickShopItem.OnSuccessfulPurchase += HandleFirstPurchase;
        CompanionShopItem.OnSuccessfulPurchase += HandleFirstPurchase;
        TrySubscribeToNightBegin();
    }

    private void Start()
    {
        TrySubscribeToNightBegin();

        if (!IsDayOne())
        {
            return;
        }

        ShowHint(introMessage, introDuration);
        StartCoroutine(ShowReadyPromptLater());
    }

    private void OnDisable()
    {
        BrickShopItem.OnSuccessfulPurchase -= HandleFirstPurchase;
        CompanionShopItem.OnSuccessfulPurchase -= HandleFirstPurchase;
        if (subscribedToNightBegin && DayNightManager.Instance != null)
        {
            DayNightManager.Instance.OnNightBegin -= HandleNightBegin;
        }
        subscribedToNightBegin = false;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplyStyle();
        }
    }

    private void HandleFirstPurchase()
    {
        if (firstPurchaseShown || !IsDayOne())
        {
            return;
        }

        firstPurchaseShown = true;
        StartCoroutine(ShowFirstPurchaseWhenIdle());
    }

    private void HandleNightBegin()
    {
        if (firstNightShown || !IsDayOne())
        {
            return;
        }

        firstNightShown = true;
        StartCoroutine(ShowFirstNightWhenIdle());
    }

    private void TrySubscribeToNightBegin()
    {
        if (subscribedToNightBegin || DayNightManager.Instance == null)
        {
            return;
        }

        DayNightManager.Instance.OnNightBegin += HandleNightBegin;
        subscribedToNightBegin = true;
    }

    private IEnumerator ShowFirstPurchaseWhenIdle()
    {
        while (activeHint != null)
        {
            yield return null;
        }

        if (IsDayOne())
        {
            ShowHint(firstPurchaseMessage, firstPurchaseDuration);
        }
    }

    private IEnumerator ShowFirstNightWhenIdle()
    {
        while (activeHint != null)
        {
            yield return null;
        }

        if (IsDayOne())
        {
            ShowHint(firstNightMessage, firstNightDuration);
        }
    }

    private IEnumerator ShowReadyPromptLater()
    {
        yield return new WaitForSeconds(readyPromptDelay);

        while (activeHint != null)
        {
            yield return null;
        }

        if (IsDayOne())
        {
            ShowHint(readyMessage, readyPromptDuration);
        }
    }

    private void ShowHint(string message, float duration)
    {
        if (activeHint != null)
        {
            StopCoroutine(activeHint);
        }

        activeHint = StartCoroutine(ShowHintRoutine(message, duration));
    }

    private IEnumerator ShowHintRoutine(string message, float duration)
    {
        ApplyStyle();
        hintText.text = message;
        yield return Fade(0f, 1f);

        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        yield return Fade(1f, 0f);
        activeHint = null;
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("Tutorial Hint Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 2;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("Tutorial Hint Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        panelRect = panelObject.AddComponent<RectTransform>();
        panelImage = panelObject.AddComponent<Image>();
        panelOutline = panelObject.AddComponent<Outline>();
        canvasGroup = panelObject.AddComponent<CanvasGroup>();

        GameObject textObject = new GameObject("Tutorial Hint Text");
        textObject.transform.SetParent(panelObject.transform, false);
        hintText = textObject.AddComponent<TextMeshProUGUI>();
    }

    private void ApplyStyle()
    {
        if (panelRect == null || hintText == null)
        {
            return;
        }

        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = panelSize;

        panelImage.color = panelColor;
        panelImage.raycastTarget = false;

        panelOutline.effectColor = borderColor;
        panelOutline.effectDistance = borderDistance;
        panelOutline.useGraphicAlpha = true;

        RectTransform textRect = hintText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textPadding;
        textRect.offsetMax = -textPadding;

        hintText.color = textColor;
        hintText.fontSize = fontSize;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.textWrappingMode = TextWrappingModes.Normal;
        hintText.raycastTarget = false;
        if (fontAsset != null)
        {
            hintText.font = fontAsset;
        }
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeDuration <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private void HideInstant()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    private static bool IsDayOne()
    {
        return DayNightManager.Instance == null || DayNightManager.Instance.DayCount == 1;
    }
}
