using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class PauseMenuController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private Key pauseKey = Key.Escape;

    [Header("Look")]
    [SerializeField] private Sprite resumeButtonSprite;
    [SerializeField] private Sprite exitButtonSprite;
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.68f);
    [SerializeField] private Vector2 buttonSize = new Vector2(420f, 128f);
    [SerializeField] private Vector2 resumeButtonPosition = new Vector2(0f, 75f);
    [SerializeField] private Vector2 exitButtonPosition = new Vector2(0f, -95f);

    [Header("Timing")]
    [SerializeField, Min(0f)] private float exitFadeDuration = 1.4f;

    [Header("SFX")]
    [SerializeField] private SFXManager.Sfx buttonSfx = SFXManager.Sfx.Interact;

    private Canvas canvas;
    private Image overlayImage;
    private RectTransform resumeButtonRect;
    private RectTransform exitButtonRect;
    private float previousTimeScale = 1f;
    private bool isPaused;
    private bool isExiting;

    private void Awake()
    {
        BuildPauseUi();
        HidePauseMenu();
    }

    private void Update()
    {
        if (isExiting)
        {
            return;
        }

        if (WasPausePressed())
        {
            if (isPaused) ResumeGame();
            else PauseGame();
            return;
        }

        if (!isPaused || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        if (IsPointerInside(resumeButtonRect, screenPosition))
        {
            ResumeGame();
        }
        else if (IsPointerInside(exitButtonRect, screenPosition))
        {
            ExitGame();
        }
    }

    public void PauseGame()
    {
        if (isPaused || isExiting)
        {
            return;
        }

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isPaused = true;
        canvas.gameObject.SetActive(true);
        overlayImage.color = overlayColor;
    }

    public void ResumeGame()
    {
        if (!isPaused || isExiting)
        {
            return;
        }

        SFXManager.PlayGlobal(buttonSfx);
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        isPaused = false;
        HidePauseMenu();
    }

    public void ExitGame()
    {
        if (isExiting)
        {
            return;
        }

        StartCoroutine(ExitRoutine());
    }

    private IEnumerator ExitRoutine()
    {
        isExiting = true;
        isPaused = true;
        Time.timeScale = 0f;
        canvas.gameObject.SetActive(true);
        SFXManager.PlayGlobal(buttonSfx);

        yield return FadeOverlay(overlayImage.color.a, 1f);
        QuitApplication();
    }

    private void BuildPauseUi()
    {
        GameObject canvasObject = new GameObject("Pause Menu Canvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 1;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        overlayImage = CreateOverlay(canvasObject.transform);
        resumeButtonRect = CreateButton("Resume Button", canvasObject.transform, resumeButtonSprite, resumeButtonPosition, ResumeGame);
        exitButtonRect = CreateButton("Exit Button", canvasObject.transform, exitButtonSprite, exitButtonPosition, ExitGame);
    }

    private Image CreateOverlay(Transform parent)
    {
        GameObject imageObject = new GameObject("Pause Overlay");
        imageObject.transform.SetParent(parent, false);

        RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = imageObject.AddComponent<Image>();
        image.color = overlayColor;
        image.raycastTarget = true;
        return image;
    }

    private RectTransform CreateButton(string objectName, Transform parent, Sprite sprite, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = buttonSize;

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        return rectTransform;
    }

    private IEnumerator FadeOverlay(float fromAlpha, float toAlpha)
    {
        if (exitFadeDuration <= 0f)
        {
            overlayImage.color = new Color(0f, 0f, 0f, toAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < exitFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / exitFadeDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            overlayImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(fromAlpha, toAlpha, eased));
            yield return null;
        }

        overlayImage.color = new Color(0f, 0f, 0f, toAlpha);
    }

    private void HidePauseMenu()
    {
        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    private bool WasPausePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[pauseKey].wasPressedThisFrame;
    }

    private static bool IsPointerInside(RectTransform target, Vector2 screenPosition)
    {
        return target != null && RectTransformUtility.RectangleContainsScreenPoint(target, screenPosition);
    }

    private void OnDestroy()
    {
        if (isPaused && !isExiting)
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        }
    }

    private static void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
