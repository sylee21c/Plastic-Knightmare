using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public sealed class MonitorInteractionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private string playerName = "Player";
    [SerializeField] private Vector3 promptWorldOffset = new Vector3(0f, 2.25f, 0f);

    [Header("Popup (씬에서 직접 편집)")]
    [SerializeField] private Canvas popupCanvas;

    [Header("Legacy Input")]
    [SerializeField] private KeyCode legacyInteractKey = KeyCode.F;

    private Transform player;
    private Camera sceneCamera;
    private Canvas promptCanvas;
    private RectTransform promptRect;
    private bool playerInRange;
    private bool popupOpen;
    private BuildingModeController buildingController;

    private void Awake()
    {
        FindReferences();
        buildingController = FindAnyObjectByType<BuildingModeController>();
        ConfigureTrigger();
        EnsureEventSystem();
        EnsurePrompt();
        SetPromptVisible(false);
        SetPopupVisible(false);
    }

    private void Start()
    {
        // popupCanvas 미연결 시 이름으로 자동 탐색
        if (popupCanvas == null)
        {
            GameObject found = GameObject.Find("Monitor Popup Canvas");
            if (found != null) popupCanvas = found.GetComponent<Canvas>();
        }

        // Start에서 연결 → Canvas 자식 오브젝트가 모두 준비된 후 실행
        WirePopupButtons();
    }

    private void Update()
    {
        FindReferences();
        UpdatePromptPosition();

        if (playerInRange && WasInteractPressed())
            SetPopupVisible(!popupOpen);

        if (popupOpen && WasCancelPressed())
            SetPopupVisible(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerInRange = true;
        SetPromptVisible(!popupOpen);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerInRange = false;
        SetPromptVisible(false);
        SetPopupVisible(false);
    }

    private void SetPopupVisible(bool visible)
    {
        popupOpen = visible;

        if (popupCanvas != null)
            popupCanvas.gameObject.SetActive(visible);

        // 빌딩 모드: 낮에만 팝업 상태에 따라 토글. 밤에는 DayNightManager가 관리
        bool isDay = DayNightManager.Instance == null
                     || DayNightManager.Instance.CurrentPhase == DayNightManager.Phase.Day;
        if (buildingController != null && isDay)
            buildingController.enabled = !visible;

        SetPromptVisible(playerInRange && !visible);
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(visible);
    }

    // ── 내부 유틸 ────────────────────────────────────────────────

    private void ConfigureTrigger()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        return other.name == playerName
            || other.transform.root.name == playerName
            || other.CompareTag("Player")
            || other.transform.root.CompareTag("Player");
    }

    private void FindReferences()
    {
        if (player == null)
        {
            GameObject obj = GameObject.Find(playerName);
            if (obj != null) player = obj.transform;
        }
        if (sceneCamera == null)
        {
            sceneCamera = Camera.main ?? FindAnyObjectByType<Camera>();
        }
    }

    // ── [F] 프롬프트 (코드 생성, 편집 불필요) ────────────────────

    private void EnsurePrompt()
    {
        if (promptCanvas != null) return;

        GameObject canvasObj = new GameObject("Monitor Prompt Canvas");
        promptCanvas = canvasObj.AddComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        promptCanvas.sortingOrder = 95;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject promptObj = new GameObject("Prompt");
        promptObj.transform.SetParent(canvasObj.transform, false);
        promptRect = promptObj.AddComponent<RectTransform>();
        promptRect.sizeDelta = new Vector2(340f, 52f);

        Image bg = promptObj.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.05f, 0.82f);
        bg.raycastTarget = false;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(promptObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.text = "[F]  상호작용";
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 24;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(0.5f, 1f, 0.7f, 1f);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                  ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.raycastTarget = false;
    }

    private void UpdatePromptPosition()
    {
        if (!playerInRange || promptRect == null || player == null || sceneCamera == null) return;
        promptRect.position = sceneCamera.WorldToScreenPoint(player.position + promptWorldOffset);
    }

    // ── 입력 ─────────────────────────────────────────────────────

    private bool WasInteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null) return kb.fKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(legacyInteractKey);
#else
        return false;
#endif
    }

    private bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null) return kb.escapeKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    // Inspector의 Button onClick에서 직접 연결 가능한 public 메서드
    public void OnReadyButtonClicked()
    {
        SetPopupVisible(false);
        DayNightManager.Instance?.BeginNight();
    }

    public void OnCloseButtonClicked()
    {
        SetPopupVisible(false);
    }

    private void WirePopupButtons()
    {
        if (popupCanvas == null) return;

        foreach (Button btn in popupCanvas.GetComponentsInChildren<Button>(true))
        {
            string n = btn.gameObject.name;
            if (n == "Ready Button")
                btn.onClick.AddListener(OnReadyButtonClicked);
            else if (n == "X Button" || n == "X")
                btn.onClick.AddListener(OnCloseButtonClicked);
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }
}
