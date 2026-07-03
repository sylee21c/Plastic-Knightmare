using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[ExecuteAlways]
public sealed class BuildingHotbarUI : MonoBehaviour
{
    private static readonly string[] SlotNames = { "2x8", "2x4", "2x2", "2x1" };

    [SerializeField] private string hotbarResourcePath = "UI/toy_hotbar_simple";
    [SerializeField] private Vector2 hotbarSize = new Vector2(360f, 206f);
    [SerializeField] private Vector2 hotbarAnchoredPosition = new Vector2(0f, 6f);
    [SerializeField] private Sprite[] slotIcons = new Sprite[4];
    [SerializeField] private Vector2 iconSize = new Vector2(54f, 54f);
    [SerializeField] private Vector2 firstIconAnchoredPosition = new Vector2(-122f, 2f);
    [SerializeField] private float iconSpacing = 81f;

    public static void EnsureExists()
    {
        BuildingHotbarUI existingHotbar = FindAnyObjectByType<BuildingHotbarUI>();
        if (existingHotbar == null)
        {
            GameObject canvasObject = new GameObject("Building Hotbar Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();
            canvasObject.AddComponent<BuildingHotbarUI>();
        }

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }
    }

    private void Awake()
    {
        RebuildSlots();
    }

    private void OnEnable()
    {
        RebuildSlots();
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        RebuildSlots();
    }

    private void RebuildSlots()
    {
        ClearChildren();
        BuildSlots();
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void BuildSlots()
    {
        RectTransform root = GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Sprite hotbarSprite = Resources.Load<Sprite>(hotbarResourcePath);
        if (hotbarSprite != null)
        {
            CreateSpriteHotbar(hotbarSprite);
            return;
        }

        GameObject bar = new GameObject("Hotbar Slots");
        bar.transform.SetParent(transform, false);

        RectTransform barRect = bar.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0f);
        barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 28f);
        barRect.sizeDelta = new Vector2(268f, 56f);

        HorizontalLayoutGroup layout = bar.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        for (int i = 0; i < SlotNames.Length; i++)
        {
            CreateSlot(bar.transform, SlotNames[i], i == 0);
        }
    }

    private void CreateSpriteHotbar(Sprite hotbarSprite)
    {
        GameObject bar = new GameObject("Toy Hotbar");
        bar.transform.SetParent(transform, false);

        RectTransform barRect = bar.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0f);
        barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = hotbarAnchoredPosition;
        barRect.sizeDelta = hotbarSize;

        Image image = bar.AddComponent<Image>();
        image.sprite = hotbarSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        CreateIconSlots(bar.transform);
    }

    private void CreateIconSlots(Transform parent)
    {
        for (int i = 0; i < SlotNames.Length; i++)
        {
            GameObject iconObject = new GameObject($"Icon {SlotNames[i]}");
            iconObject.transform.SetParent(parent, false);

            RectTransform iconRect = iconObject.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = firstIconAnchoredPosition + new Vector2(iconSpacing * i, 0f);
            iconRect.sizeDelta = iconSize;

            Image iconImage = iconObject.AddComponent<Image>();
            iconImage.sprite = GetSlotIcon(i);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.color = iconImage.sprite == null ? new Color(1f, 1f, 1f, 0f) : Color.white;
        }
    }

    private Sprite GetSlotIcon(int index)
    {
        if (slotIcons == null || index < 0 || index >= slotIcons.Length)
        {
            return null;
        }

        return slotIcons[index];
    }

    private static void CreateSlot(Transform parent, string slotName, bool selected)
    {
        GameObject slot = new GameObject($"Slot {slotName}");
        slot.transform.SetParent(parent, false);

        RectTransform slotRect = slot.AddComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(56f, 56f);

        Image slotImage = slot.AddComponent<Image>();
        slotImage.color = selected ? new Color(1f, 1f, 1f, 0.52f) : new Color(0.03f, 0.035f, 0.04f, 0.56f);
        slotImage.raycastTarget = true;

        Outline outline = slot.AddComponent<Outline>();
        outline.effectColor = selected ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.45f);
        outline.effectDistance = selected ? new Vector2(3f, -3f) : new Vector2(2f, -2f);

        GameObject label = new GameObject("Label");
        label.transform.SetParent(slot.transform, false);

        RectTransform labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text labelText = label.AddComponent<Text>();
        labelText.text = slotName;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = new Color(1f, 1f, 1f, 0.74f);
        labelText.fontSize = 14;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (labelText.font == null)
        {
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        labelText.raycastTarget = false;
    }
}
