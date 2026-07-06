using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class GameOverUIController : MonoBehaviour
{
    private const string GameOverIconPath = "Assets/UI/GameOverIcon.png";
    private const string ButtonImagePath = "Assets/UI/buttonImage.png";
    private const string CoinIconPath = "Assets/UI/coin_icon.png";

    private static GameOverUIController instance;

    [Header("Scene UI References")]
    [Tooltip("전체 게임오버 UI를 감싸는 CanvasGroup. 보통 GameOver Canvas 루트에 붙이면 됨.")]
    [SerializeField] private CanvasGroup rootGroup;
    [Tooltip("암전용 풀스크린 검정 이미지. 비어있으면 코드로 자동 생성.")]
    [SerializeField] private CanvasGroup backgroundGroup;
    [SerializeField] private Image backgroundImage;
    [Tooltip("GameOverIcon 이미지 오브젝트 또는 그 부모의 CanvasGroup.")]
    [SerializeField] private CanvasGroup iconGroup;
    [Tooltip("Retry 버튼과 보상 코인 표시를 함께 담는 패널의 CanvasGroup.")]
    [SerializeField] private CanvasGroup retryGroup;
    [SerializeField] private Button retryButton;
    [SerializeField] private TMP_Text retryButtonText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private Image gameOverIconImage;
    [SerializeField] private Image retryButtonImage;
    [SerializeField] private Image rewardCoinImage;

    [Header("Default Sprites")]
    [SerializeField] private Sprite gameOverIconSprite;
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private Sprite coinIconSprite;

    [Header("Timing")]
    [SerializeField] private float iconFadeDuration = 0.7f;
    [SerializeField] private float retryDelay = 0.7f;
    [SerializeField] private float retryFadeDuration = 0.35f;
    [SerializeField] private float rewardCountDownDuration = 1.1f;
    [Tooltip("배경이 서서히 완전 암전되는 시간")]
    [SerializeField] private float blackoutFadeDuration = 2.2f;

    [Header("Coin Penalty")]
    [Tooltip("Game over removes this percentage of coins earned during the current night. 0.5 = lose 50%, keep 50%.")]
    [SerializeField, Range(0f, 1f)] private float nightCoinLossPercent = 0.5f;

    [Header("Fallback")]
    [Tooltip("씬에서 레퍼런스를 연결하지 않았을 때만 기본 UI 자식을 자동 생성합니다.")]
    [SerializeField] private bool buildMissingUi = true;
    [Tooltip("Keep existing scene Canvas and CanvasScaler values instead of forcing generated defaults.")]
    [SerializeField] private bool preserveSceneCanvasSettings = true;

    private Damageable bedDamageable;
    private Coroutine subscribeRoutine;
    private Coroutine rewardCountRoutine;
    private int rewardCountFrom;
    private int rewardCountTo;
    private bool showing;

    // 게임오버 진행 중 여부. Player/Ghost/Companion 이 움직임을 멈추기 위해 참조.
    public static bool IsGameOver { get; private set; }

    public static void EnsureExists()
    {
        if (instance != null) return;

        GameOverUIController found = FindAnyObjectByType<GameOverUIController>(FindObjectsInactive.Include);
        if (found != null)
        {
            instance = found;
            return;
        }

        GameObject go = new GameObject("Game Over UI Controller");
        instance = go.AddComponent<GameOverUIController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        LoadDefaultSprites();
        ConfigureForSceneEditing();
        HideInstant();
    }

    private void OnEnable()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(Retry);
            retryButton.onClick.AddListener(Retry);
        }

        if (subscribeRoutine != null) StopCoroutine(subscribeRoutine);
        subscribeRoutine = StartCoroutine(SubscribeBedWhenReady());
    }

    private void OnDisable()
    {
        if (retryButton != null)
            retryButton.onClick.RemoveListener(Retry);

        if (bedDamageable != null)
            bedDamageable.OnDeath -= HandleBedDeath;

        if (subscribeRoutine != null)
        {
            StopCoroutine(subscribeRoutine);
            subscribeRoutine = null;
        }
    }

    private IEnumerator SubscribeBedWhenReady()
    {
        while (bedDamageable == null)
        {
            GameObject bed = GameObject.FindWithTag("Bed");
            if (bed == null) bed = GameObject.Find("Bed");
            if (bed != null) bedDamageable = bed.GetComponent<Damageable>();
            yield return bedDamageable == null ? new WaitForSeconds(0.25f) : null;
        }

        bedDamageable.OnDeath -= HandleBedDeath;
        bedDamageable.OnDeath += HandleBedDeath;

        // 침대 피격 사운드 자동 확보 (씬에 미부착 시 자동 추가)
        if (bedDamageable.GetComponent<BedSfx>() == null)
            bedDamageable.gameObject.AddComponent<BedSfx>();
    }

    private void HandleBedDeath()
    {
        if (showing) return;

        int currentCoins = CoinWallet.Instance != null ? CoinWallet.Instance.Coins : 0;
        int earnedThisNight = CoinWallet.Instance != null ? CoinWallet.Instance.CoinsEarnedThisNight : 0;
        int loss = Mathf.FloorToInt(earnedThisNight * Mathf.Clamp01(nightCoinLossPercent));
        int finalCoins = Mathf.Max(0, currentCoins - loss);

        if (CoinWallet.Instance != null)
            CoinWallet.Instance.SetCoins(finalCoins);

        rewardCountFrom = currentCoins;
        rewardCountTo = finalCoins;

        // 밤 BGM 페이드아웃 → 게임오버 전용 곡 페이드인 (1회 재생)
        BGMManager.Instance?.EnterGameOverMusic();

        StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        showing = true;
        IsGameOver = true;

        SetRootVisible(true);
        SetGroup(backgroundGroup, 0f, false);
        SetGroup(iconGroup, 0f, false);
        SetGroup(retryGroup, 0f, false);
        if (rewardText != null)
            rewardText.text = rewardCountFrom.ToString("N0");

        // 배경 암전은 별도로 병렬 진행 (다른 UI 페이드와 동시에)
        StartCoroutine(FadeGroup(backgroundGroup, 0f, 1f, blackoutFadeDuration));

        yield return FadeGroup(iconGroup, 0f, 1f, iconFadeDuration);
        yield return new WaitForSecondsRealtime(retryDelay);
        yield return FadeGroup(retryGroup, 0f, 1f, retryFadeDuration);

        yield return PlayRewardCountDown();
        SetGroup(retryGroup, 1f, true);
    }

    private void Retry()
    {
        if (!showing) return;

        showing = false;
        IsGameOver = false;
        if (rewardCountRoutine != null)
        {
            StopCoroutine(rewardCountRoutine);
            rewardCountRoutine = null;
        }
        if (rewardText != null)
            rewardText.text = rewardCountTo.ToString("N0");
        HideInstant();
        // 게임오버 곡 페이드아웃 → 낮 BGM 페이드인
        BGMManager.Instance?.ReturnToDayMusic();
        DayNightManager.Instance?.ReturnToCurrentDayAfterGameOver();
    }

    private IEnumerator PlayRewardCountDown()
    {
        if (rewardText == null)
            yield break;

        if (rewardCountRoutine != null)
            StopCoroutine(rewardCountRoutine);

        rewardCountRoutine = StartCoroutine(RewardCountDownRoutine());
        yield return rewardCountRoutine;
        rewardCountRoutine = null;
    }

    private IEnumerator RewardCountDownRoutine()
    {
        float duration = Mathf.Max(0.01f, rewardCountDownDuration);
        float elapsed = 0f;

        rewardText.text = rewardCountFrom.ToString("N0");

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            int value = Mathf.RoundToInt(Mathf.Lerp(rewardCountFrom, rewardCountTo, t));
            rewardText.text = value.ToString("N0");
            yield return null;
        }

        rewardText.text = rewardCountTo.ToString("N0");
    }

    private IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;
        float elapsed = 0f;
        group.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        group.alpha = to;
    }

    private void ConfigureForSceneEditing()
    {
        EnsureEventSystem();
        EnsureCanvasInfrastructure();

        if (buildMissingUi)
            BuildMissingEditableUi();

        AutoAssignMissingReferences();
        ApplyDefaultSprites();

        if (retryButtonText != null)
            retryButtonText.text = "Retry";

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(Retry);
            retryButton.onClick.AddListener(Retry);
        }
    }

    private void EnsureCanvasInfrastructure()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponentInChildren<Canvas>(true);
        bool createdCanvas = false;
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            createdCanvas = true;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        if (createdCanvas || !preserveSceneCanvasSettings)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        if (rootGroup == null)
        {
            rootGroup = GetComponent<CanvasGroup>();
            if (rootGroup == null && canvas.gameObject == gameObject)
                rootGroup = gameObject.AddComponent<CanvasGroup>();
            if (rootGroup == null)
                rootGroup = canvas.gameObject.GetComponent<CanvasGroup>()
                    ?? canvas.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void BuildMissingEditableUi()
    {
        if (backgroundGroup == null && backgroundImage == null)
            CreateBlackoutBackground();

        if (iconGroup == null && gameOverIconImage == null)
            CreateDefaultIcon();

        if (retryGroup == null && retryButton == null && rewardText == null)
            CreateDefaultRetryPanel();
    }

    private void CreateBlackoutBackground()
    {
        GameObject bgObj = FindOrCreateChild("Blackout");
        // 다른 UI 뒤로 오도록 최하단으로
        bgObj.transform.SetSiblingIndex(0);

        RectTransform bgRect = GetOrAdd<RectTransform>(bgObj);
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        backgroundImage = GetOrAdd<Image>(bgObj);
        backgroundImage.color = Color.black;
        backgroundImage.raycastTarget = false;

        backgroundGroup = GetOrAdd<CanvasGroup>(bgObj);
        backgroundGroup.alpha = 0f;
        backgroundGroup.interactable = false;
        backgroundGroup.blocksRaycasts = false;
    }

    private void CreateDefaultIcon()
    {
        GameObject iconObj = FindOrCreateChild("GameOverIcon");
        gameOverIconImage = GetOrAdd<Image>(iconObj);
        gameOverIconImage.preserveAspect = true;
        gameOverIconImage.raycastTarget = false;

        RectTransform iconRect = GetOrAdd<RectTransform>(iconObj);
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 155f);
        iconRect.sizeDelta = new Vector2(620f, 260f);

        iconGroup = GetOrAdd<CanvasGroup>(iconObj);
    }

    private void CreateDefaultRetryPanel()
    {
        GameObject retryObj = FindOrCreateChild("RetryPanel");
        RectTransform retryRect = GetOrAdd<RectTransform>(retryObj);
        retryRect.anchorMin = new Vector2(0.5f, 0.5f);
        retryRect.anchorMax = new Vector2(0.5f, 0.5f);
        retryRect.pivot = new Vector2(0.5f, 0.5f);
        retryRect.anchoredPosition = new Vector2(0f, -235f);
        retryRect.sizeDelta = new Vector2(420f, 180f);
        retryGroup = GetOrAdd<CanvasGroup>(retryObj);

        GameObject buttonObj = FindOrCreateChild("RetryButton", retryObj.transform);
        RectTransform buttonRect = GetOrAdd<RectTransform>(buttonObj);
        buttonRect.anchorMin = new Vector2(0.5f, 1f);
        buttonRect.anchorMax = new Vector2(0.5f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 1f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(320f, 92f);

        retryButtonImage = GetOrAdd<Image>(buttonObj);
        retryButtonImage.type = Image.Type.Sliced;
        retryButtonImage.preserveAspect = true;
        retryButton = GetOrAdd<Button>(buttonObj);
        retryButton.targetGraphic = retryButtonImage;

        GameObject textObj = FindOrCreateChild("Text", buttonObj.transform);
        RectTransform textRect = GetOrAdd<RectTransform>(textObj);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        retryButtonText = GetOrAdd<TextMeshProUGUI>(textObj);
        retryButtonText.fontSize = 38f;
        retryButtonText.alignment = TextAlignmentOptions.Center;
        retryButtonText.color = Color.white;
        retryButtonText.raycastTarget = false;

        GameObject rowObj = FindOrCreateChild("RewardRow", retryObj.transform);
        RectTransform rowRect = GetOrAdd<RectTransform>(rowObj);
        rowRect.anchorMin = new Vector2(0.5f, 0f);
        rowRect.anchorMax = new Vector2(0.5f, 0f);
        rowRect.pivot = new Vector2(0.5f, 0f);
        rowRect.anchoredPosition = new Vector2(0f, 12f);
        rowRect.sizeDelta = new Vector2(260f, 58f);

        HorizontalLayoutGroup layout = GetOrAdd<HorizontalLayoutGroup>(rowObj);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 12f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject coinObj = FindOrCreateChild("CoinIcon", rowObj.transform);
        RectTransform coinRect = GetOrAdd<RectTransform>(coinObj);
        coinRect.sizeDelta = new Vector2(42f, 42f);
        rewardCoinImage = GetOrAdd<Image>(coinObj);
        rewardCoinImage.preserveAspect = true;
        rewardCoinImage.raycastTarget = false;

        GameObject rewardObj = FindOrCreateChild("RewardText", rowObj.transform);
        RectTransform rewardRect = GetOrAdd<RectTransform>(rewardObj);
        rewardRect.sizeDelta = new Vector2(190f, 56f);
        rewardText = GetOrAdd<TextMeshProUGUI>(rewardObj);
        rewardText.fontSize = 34f;
        rewardText.alignment = TextAlignmentOptions.MidlineLeft;
        rewardText.color = Color.white;
        rewardText.raycastTarget = false;
    }

    private void AutoAssignMissingReferences()
    {
        if (backgroundImage == null)
            backgroundImage = transform.Find("Blackout")?.GetComponent<Image>();
        if (backgroundGroup == null && backgroundImage != null)
            backgroundGroup = backgroundImage.GetComponent<CanvasGroup>()
                ?? backgroundImage.gameObject.AddComponent<CanvasGroup>();

        if (gameOverIconImage == null)
            gameOverIconImage = transform.Find("GameOverIcon")?.GetComponent<Image>();
        if (iconGroup == null && gameOverIconImage != null)
            iconGroup = gameOverIconImage.GetComponent<CanvasGroup>()
                ?? gameOverIconImage.gameObject.AddComponent<CanvasGroup>();

        Transform retryPanel = transform.Find("RetryPanel");
        if (retryGroup == null && retryPanel != null)
            retryGroup = retryPanel.GetComponent<CanvasGroup>()
                ?? retryPanel.gameObject.AddComponent<CanvasGroup>();

        if (retryButton == null)
            retryButton = GetComponentInChildren<Button>(true);
        if (retryButtonImage == null && retryButton != null)
            retryButtonImage = retryButton.GetComponent<Image>();
        if (retryButtonText == null && retryButton != null)
            retryButtonText = retryButton.GetComponentInChildren<TMP_Text>(true);

        if (rewardText == null)
        {
            Transform reward = retryPanel != null ? retryPanel.Find("RewardRow/RewardText") : null;
            rewardText = reward != null ? reward.GetComponent<TMP_Text>() : null;
        }

        if (rewardCoinImage == null)
        {
            Transform coin = retryPanel != null ? retryPanel.Find("RewardRow/CoinIcon") : null;
            rewardCoinImage = coin != null ? coin.GetComponent<Image>() : null;
        }
    }

    private void ApplyDefaultSprites()
    {
        if (gameOverIconImage != null && gameOverIconImage.sprite == null)
            gameOverIconImage.sprite = gameOverIconSprite;
        if (retryButtonImage != null && retryButtonImage.sprite == null)
            retryButtonImage.sprite = buttonSprite;
        if (rewardCoinImage != null && rewardCoinImage.sprite == null)
            rewardCoinImage.sprite = coinIconSprite;
    }

    private void HideInstant()
    {
        SetRootVisible(false);
        SetGroup(backgroundGroup, 0f, false);
        SetGroup(iconGroup, 0f, false);
        SetGroup(retryGroup, 0f, false);
    }

    private void SetRootVisible(bool visible)
    {
        if (rootGroup == null) return;
        rootGroup.alpha = visible ? 1f : 0f;
        rootGroup.interactable = visible;
        rootGroup.blocksRaycasts = visible;
    }

    private static void SetGroup(CanvasGroup group, float alpha, bool interactive)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
    }

    private GameObject FindOrCreateChild(string childName, Transform parent = null)
    {
        Transform targetParent = parent != null ? parent : transform;
        Transform existing = targetParent.Find(childName);
        if (existing != null) return existing.gameObject;

        GameObject go = new GameObject(childName);
        go.transform.SetParent(targetParent, false);
        return go;
    }

    private void LoadDefaultSprites()
    {
#if UNITY_EDITOR
        if (gameOverIconSprite == null)
            gameOverIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GameOverIconPath);
        if (buttonSprite == null)
            buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonImagePath);
        if (coinIconSprite == null)
            coinIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CoinIconPath);
#endif
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }
}
