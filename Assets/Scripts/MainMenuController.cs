using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "MainScene";
    [SerializeField] private SFXManager.Sfx buttonSfx = SFXManager.Sfx.Interact;
    [SerializeField, Min(0f)] private float actionDelay = 0.12f;
    [SerializeField, Min(0f)] private float fadeDuration = 1.4f;
    [SerializeField] private RectTransform startButtonRect;
    [SerializeField] private RectTransform exitButtonRect;

    private bool isTransitioning;

    private void Update()
    {
        if (isTransitioning || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        if (IsPointerInside(startButtonRect, screenPosition))
        {
            StartGame();
        }
        else if (IsPointerInside(exitButtonRect, screenPosition))
        {
            ExitGame();
        }
    }

    public void StartGame()
    {
        if (isTransitioning) return;
        StartCoroutine(StartGameRoutine());
    }

    public void ExitGame()
    {
        if (isTransitioning) return;
        StartCoroutine(ExitGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        isTransitioning = true;
        DontDestroyOnLoad(gameObject);
        Image fadeImage = CreateFadeImage();

        SFXManager.PlayGlobal(buttonSfx);
        if (actionDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(actionDelay);
        }

        yield return Fade(fadeImage, 0f, 1f);
        SceneManager.LoadScene(gameSceneName);
        yield return null;
        yield return Fade(fadeImage, 1f, 0f);

        if (fadeImage != null)
        {
            Destroy(fadeImage.transform.root.gameObject);
        }

        Destroy(gameObject);
    }

    private IEnumerator ExitGameRoutine()
    {
        isTransitioning = true;
        Image fadeImage = CreateFadeImage();

        SFXManager.PlayGlobal(buttonSfx);
        if (actionDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(actionDelay);
        }

        yield return Fade(fadeImage, 0f, 1f);
        QuitApplication();
    }

    private Image CreateFadeImage()
    {
        GameObject canvasObject = new GameObject("Scene Fade Canvas");
        DontDestroyOnLoad(canvasObject);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new GameObject("Fade Overlay");
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = imageObject.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = false;
        return image;
    }

    private IEnumerator Fade(Image fadeImage, float fromAlpha, float toAlpha)
    {
        if (fadeImage == null)
        {
            yield break;
        }

        if (fadeDuration <= 0f)
        {
            fadeImage.color = new Color(0f, 0f, 0f, toAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(fromAlpha, toAlpha, eased));
            yield return null;
        }

        fadeImage.color = new Color(0f, 0f, 0f, toAlpha);
    }

    private static bool IsPointerInside(RectTransform target, Vector2 screenPosition)
    {
        return target != null && RectTransformUtility.RectangleContainsScreenPoint(target, screenPosition);
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
