using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[ExecuteAlways]
public sealed class BuildingHotbarUI : MonoBehaviour
{
    private const int SlotCount = 5;
    private const string SlotFillName = "Fill";
    private const string SlotGlowName = "Glow";
    private const string SlotNumberBadgeName = "Number Badge";
    private const string SlotNumberTextName = "Number";
    private static readonly string[] SlotNames = { "2x2", "2x1", "2x4", "2x8", "Empty" };
    private static BuildingHotbarUI instance;
    private static int selectedSlotIndex = 0;

    [SerializeField] private bool preserveSceneAuthoredLayout = true;
    [SerializeField] private string hotbarResourcePath = "";
    [SerializeField] private Vector2 hotbarSize = new Vector2(520f, 126f);
    [SerializeField] private Vector2 hotbarAnchoredPosition = new Vector2(0f, 18f);
    [SerializeField] private Sprite[] slotIcons = new Sprite[SlotCount];
    [SerializeField] private Sprite slotFrameSprite;
    [SerializeField] private Sprite selectedSlotFrameSprite;
    [SerializeField] private Sprite numberBadgeSprite;
    [SerializeField] private Sprite selectedNumberBadgeSprite;
    [SerializeField] private Vector2 iconSize = new Vector2(58f, 58f);
    [SerializeField] private float iconSpacing = 96f;
    [SerializeField] private Vector2 slotBoxSize = new Vector2(92f, 92f);
    [SerializeField] private float selectedSlotScale = 1.18f;
    [SerializeField] private Color slotFillColor = new Color(0.18f, 0.18f, 0.18f, 0.62f);
    [SerializeField] private Color slotBorderColor = new Color(1f, 1f, 1f, 0.88f);
    [SerializeField] private Color selectedSlotBorderColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color selectedSlotGlowColor = new Color(0.36f, 0.82f, 1f, 0.55f);
    [SerializeField] private Color slotNumberBadgeColor = new Color(0.12f, 0.12f, 0.12f, 0.88f);
    [SerializeField] private Color selectedNumberBadgeColor = new Color(0.96f, 0.96f, 0.96f, 0.98f);

    private RectTransform[] slotBoxes;
    private Outline[] slotOutlines;
    private Text[] slotCountTexts;
    private BrickInventory subscribedInventory;

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

        if (existingHotbar != null)
        {
            existingHotbar.EnsureSlotBoxes();
            existingHotbar.RefreshSelectedSlot();
        }
    }

    public static void SetSelectedSlot(int index)
    {
        selectedSlotIndex = Mathf.Clamp(index, 0, SlotCount - 1);
        if (instance == null)
        {
            instance = FindAnyObjectByType<BuildingHotbarUI>();
        }

        if (instance == null)
        {
            return;
        }

        instance.EnsureSlotBoxes();
        instance.RefreshSelectedSlot();
        instance.RefreshAllCounts(); // 슬롯 재구성 후 dim 알파 재적용
    }

    private void Awake()
    {
        instance = this;
        EnsureCanvasRect();
        BuildSlotsIfEmpty();
        EnsureSlotBoxes();
        RefreshSelectedSlot();
    }

    private void OnEnable()
    {
        instance = this;
        EnsureCanvasRect();
        BuildSlotsIfEmpty();
        EnsureSlotBoxes();
        RefreshSelectedSlot();
        SubscribeInventory();
        RefreshAllCounts();
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

#if UNITY_EDITOR
        // OnValidate 안에서 RectTransform 을 건드리면 SendMessage 예외가 뜬다.
        // 에디터가 안전한 시점에 실행되도록 지연 호출로 옮김.
        UnityEditor.EditorApplication.delayCall += DeferredValidate;
#endif
    }

#if UNITY_EDITOR
    private void DeferredValidate()
    {
        UnityEditor.EditorApplication.delayCall -= DeferredValidate;
        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        EnsureCanvasRect();
        BuildSlotsIfEmpty();
        EnsureSlotBoxes();
        RefreshSelectedSlot();
    }
#endif

    private void OnDisable()
    {
        UnsubscribeInventory();
        if (instance == this)
        {
            instance = null;
        }
    }

    private CompanionInventory subscribedCompanionInventory;

    private void SubscribeInventory()
    {
        if (!Application.isPlaying) return;

        // Brick 인벤토리 구독
        BrickInventory.EnsureExists();
        if (subscribedInventory != BrickInventory.Instance)
        {
            if (subscribedInventory != null)
                subscribedInventory.OnCountChanged -= HandleInventoryChanged;
            subscribedInventory = BrickInventory.Instance;
            if (subscribedInventory != null)
                subscribedInventory.OnCountChanged += HandleInventoryChanged;
        }

        // Companion 인벤토리 구독
        CompanionInventory.EnsureExists();
        if (subscribedCompanionInventory != CompanionInventory.Instance)
        {
            if (subscribedCompanionInventory != null)
                subscribedCompanionInventory.OnInventoryChanged -= RefreshCompanionSlots;
            subscribedCompanionInventory = CompanionInventory.Instance;
            if (subscribedCompanionInventory != null)
                subscribedCompanionInventory.OnInventoryChanged += RefreshCompanionSlots;
        }
    }

    private void UnsubscribeInventory()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.OnCountChanged -= HandleInventoryChanged;
            subscribedInventory = null;
        }
        if (subscribedCompanionInventory != null)
        {
            subscribedCompanionInventory.OnInventoryChanged -= RefreshCompanionSlots;
            subscribedCompanionInventory = null;
        }
    }

    private void HandleInventoryChanged(string key, int newCount)
    {
        if (slotCountTexts == null) return;
        for (int i = 0; i < SlotCount && i < SlotNames.Length; i++)
        {
            if (SlotNames[i] == key && slotCountTexts[i] != null)
            {
                slotCountTexts[i].text = newCount.ToString();
                if (slotBoxes != null && i < slotBoxes.Length && slotBoxes[i] != null)
                {
                    Image iconImg = FindIconImage(slotBoxes[i]);
                    if (iconImg != null)
                    {
                        Color c = iconImg.color;
                        // sprite 없으면 완전 투명 (Unity 기본 흰색 스프라이트가 새어나오는 것 방지)
                        c.a = iconImg.sprite == null ? 0f : (newCount > 0 ? 1f : 0.4f);
                        iconImg.color = c;
                    }
                }
                return;
            }
        }
    }

    private void RefreshAllCounts()
    {
        if (slotCountTexts == null) return;
        for (int i = 0; i < SlotCount && i < SlotNames.Length; i++)
        {
            if (slotCountTexts[i] == null) continue;
            int count = 0;
            if (Application.isPlaying && BrickInventory.Instance != null)
                count = BrickInventory.Instance.GetCount(SlotNames[i]);
            slotCountTexts[i].text = count.ToString();
            if (slotBoxes != null && i < slotBoxes.Length && slotBoxes[i] != null)
            {
                Image iconImg = FindIconImage(slotBoxes[i]);
                if (iconImg != null)
                {
                    Color c = iconImg.color;
                    c.a = iconImg.sprite == null ? 0f : (count > 0 ? 1f : 0.4f);
                    iconImg.color = c;
                }
            }
        }
        RefreshCompanionSlots();
    }

    // Companion 슬롯 (3~5): 카운트 갱신. 아이콘은 slotIcons[] 로 정적 지정 유지.
    // 재고 0 이면 dim (반투명), 있으면 opaque.
    private void RefreshCompanionSlots()
    {
        if (!Application.isPlaying) return;
        CompanionInventory inv = CompanionInventory.Instance;
        if (inv == null) return;

        BuildingModeController bmc = FindAnyObjectByType<BuildingModeController>();

        for (int i = inv.FirstSlotIndex; i <= inv.LastSlotIndex && i < SlotCount; i++)
        {
            string id = inv.GetIdInSlot(i);
            int count = string.IsNullOrEmpty(id) ? 0 : inv.GetCount(id);

            // 카운트 텍스트
            if (slotCountTexts != null && i < slotCountTexts.Length && slotCountTexts[i] != null)
                slotCountTexts[i].text = count > 0 ? count.ToString() : "0";

            // 아이콘은 slotIcons[] 로 정적 지정된 것 유지, 재고 0이면 반투명
            if (slotBoxes != null && i < slotBoxes.Length && slotBoxes[i] != null)
            {
                Image iconImg = FindIconImage(slotBoxes[i]);
                if (iconImg != null)
                {
                    // sprite 없으면 완전 투명. 있으면 재고에 따라 opaque/dim.
                    float alpha = iconImg.sprite == null ? 0f : (count > 0 ? 1f : 0.4f);
                    Color c = iconImg.color;
                    c.a = alpha;
                    iconImg.color = c;
                }
            }
        }
    }

    private static Image FindIconImage(RectTransform slot)
    {
        // 슬롯 자식들 중 이름이 "Icon" 으로 시작하는 Image 찾기
        foreach (Transform child in slot)
        {
            if (child.name.StartsWith("Icon") || child.name == "Icon")
            {
                Image img = child.GetComponent<Image>();
                if (img != null) return img;
            }
        }
        return null;
    }

    [ContextMenu("Rebuild Generated Hotbar")]
    private void RebuildGeneratedHotbar()
    {
        ClearChildren();
        EnsureCanvasRect();
        BuildSlots();
    }

    private void BuildSlotsIfEmpty()
    {
        if (transform.childCount > 0)
        {
            return;
        }

        BuildSlots();
    }

    private void EnsureCanvasRect()
    {
        RectTransform root = GetComponent<RectTransform>();
        if (root == null)
        {
            return;
        }

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
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
        Sprite hotbarSprite = string.IsNullOrWhiteSpace(hotbarResourcePath) ? null : Resources.Load<Sprite>(hotbarResourcePath);
        if (hotbarSprite != null)
        {
            CreateSpriteHotbar(hotbarSprite);
            return;
        }

        GameObject bar = new GameObject("Toy Hotbar");
        bar.transform.SetParent(transform, false);

        RectTransform barRect = bar.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0f);
        barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = hotbarAnchoredPosition;
        barRect.sizeDelta = hotbarSize;

        CreateSlotBoxes(bar.transform);
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

        CreateSlotBoxes(bar.transform);
    }

    private void CreateSlotBoxes(Transform parent)
    {
        if (slotBoxes == null || slotBoxes.Length != SlotCount)
        {
            slotBoxes = new RectTransform[SlotCount];
        }

        if (slotOutlines == null || slotOutlines.Length != SlotCount)
        {
            slotOutlines = new Outline[SlotCount];
        }

        for (int i = 0; i < SlotCount; i++)
        {
            RectTransform slotRect = CreateOrUpdateSlotBox(parent, i);

            EnsureSlotIcon(slotRect, i);
        }
    }

    private void EnsureSlotBoxes()
    {
        Transform parent = FindHotbarTransform();
        if (parent == null)
        {
            return;
        }

        bool hasSceneSlots = parent.Find("Slot 1") != null;
        if (!preserveSceneAuthoredLayout || !hasSceneSlots)
        {
            ConfigureHotbarContainer(parent);
        }

        if (slotBoxes == null || slotBoxes.Length != SlotCount)
        {
            slotBoxes = new RectTransform[SlotCount];
        }

        if (slotOutlines == null || slotOutlines.Length != SlotCount)
        {
            slotOutlines = new Outline[SlotCount];
        }

        if (slotCountTexts == null || slotCountTexts.Length != SlotCount)
        {
            slotCountTexts = new Text[SlotCount];
        }

        for (int i = 0; i < SlotCount; i++)
        {
            Transform existing = parent.Find($"Slot {i + 1}");
            if (existing == null && preserveSceneAuthoredLayout && hasSceneSlots)
            {
                slotBoxes[i] = null;
                slotOutlines[i] = null;
                slotCountTexts[i] = null;
                continue;
            }

            CreateOrUpdateSlotBox(parent, i, existing != null && preserveSceneAuthoredLayout);
        }
    }

    private RectTransform CreateOrUpdateSlotBox(Transform parent, int index, bool keepSceneLayout = false)
    {
        string slotName = GetSlotName(index);
        Transform existing = parent.Find($"Slot {index + 1}");
        RectTransform slotRect;
        Image slotImage;
        Outline outline;

        if (existing == null)
        {
            GameObject slotObject = new GameObject($"Slot {index + 1}");
            slotObject.transform.SetParent(parent, false);
            slotRect = slotObject.AddComponent<RectTransform>();
            slotImage = slotObject.AddComponent<Image>();
            outline = slotObject.AddComponent<Outline>();
        }
        else
        {
            slotRect = existing.GetComponent<RectTransform>();
            slotImage = existing.GetComponent<Image>();
            outline = existing.GetComponent<Outline>();
            if (slotImage == null)
            {
                slotImage = existing.gameObject.AddComponent<Image>();
            }
            if (outline == null)
            {
                outline = existing.gameObject.AddComponent<Outline>();
            }
        }

        if (!keepSceneLayout)
        {
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = GetSlotPosition(index);
            slotRect.sizeDelta = slotBoxSize;
        }

        slotImage.sprite = index == selectedSlotIndex && selectedSlotFrameSprite != null
            ? selectedSlotFrameSprite
            : slotFrameSprite;
        slotImage.type = Image.Type.Simple;
        slotImage.preserveAspect = true;
        slotImage.color = Color.white;
        slotImage.raycastTarget = false;

        outline.effectColor = slotBorderColor;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.enabled = false;

        Shadow shadow = slotRect.GetComponent<Shadow>();
        if (shadow == null && !keepSceneLayout)
        {
            shadow = slotRect.gameObject.AddComponent<Shadow>();
        }
        if (shadow != null)
        {
            shadow.enabled = false;
        }

        HideLegacyGeneratedImage(slotRect, SlotGlowName);
        HideLegacyGeneratedImage(slotRect, SlotFillName);
        EnsureSlotNumber(slotRect, index);
        EnsureSlotIcon(slotRect, index);
        EnsureSlotCountText(slotRect, index);
        ReparentLegacyIcon(parent, slotRect, slotName);

        slotBoxes[index] = slotRect;
        slotOutlines[index] = outline;
        return slotRect;
    }

    // 슬롯 우측 상단에 소지 수 표시
    private void EnsureSlotCountText(RectTransform slotRect, int index)
    {
        Transform existing = slotRect.Find("Count");
        Text countText;
        if (existing == null)
        {
            GameObject go = new GameObject("Count");
            go.transform.SetParent(slotRect, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-4f, -4f);
            rt.sizeDelta = new Vector2(48f, 26f);

            countText = go.AddComponent<Text>();
            countText.alignment = TextAnchor.MiddleRight;
            countText.font = GetSlotFont();
            countText.fontSize = 22;
            countText.fontStyle = FontStyle.Bold;
            countText.color = new Color(1f, 1f, 1f, 1f);
            countText.raycastTarget = false;
            countText.text = "0";

            Shadow sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
        }
        else
        {
            countText = existing.GetComponent<Text>();
            if (countText == null) countText = existing.gameObject.AddComponent<Text>();
        }

        if (slotCountTexts != null && index < slotCountTexts.Length)
            slotCountTexts[index] = countText;
    }

    private static Font GetSlotFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void EnsureSlotNumber(RectTransform slotRect, int index)
    {
        Transform existingBadge = slotRect.Find(SlotNumberBadgeName);
        RectTransform badgeRect;
        Image badgeImage;
        if (existingBadge == null)
        {
            GameObject badgeObject = new GameObject(SlotNumberBadgeName);
            badgeObject.transform.SetParent(slotRect, false);
            badgeRect = badgeObject.AddComponent<RectTransform>();
            badgeImage = badgeObject.AddComponent<Image>();
        }
        else
        {
            badgeRect = existingBadge.GetComponent<RectTransform>();
            badgeImage = existingBadge.GetComponent<Image>();
            if (badgeImage == null)
            {
                badgeImage = existingBadge.gameObject.AddComponent<Image>();
            }
        }

        badgeRect.anchorMin = new Vector2(0.5f, 0f);
        badgeRect.anchorMax = new Vector2(0.5f, 0f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(0f, 20f);
        badgeRect.sizeDelta = new Vector2(34f, 25f);

        badgeImage.sprite = index == selectedSlotIndex && selectedNumberBadgeSprite != null
            ? selectedNumberBadgeSprite
            : numberBadgeSprite;
        badgeImage.type = Image.Type.Simple;
        badgeImage.preserveAspect = true;
        badgeImage.color = Color.white;
        badgeImage.raycastTarget = false;

        Transform existingNumber = badgeRect.Find(SlotNumberTextName);
        Text numberText;
        if (existingNumber == null)
        {
            GameObject numberObject = new GameObject(SlotNumberTextName);
            numberObject.transform.SetParent(badgeRect, false);

            RectTransform numberRect = numberObject.AddComponent<RectTransform>();
            numberRect.anchorMin = Vector2.zero;
            numberRect.anchorMax = Vector2.one;
            numberRect.offsetMin = Vector2.zero;
            numberRect.offsetMax = Vector2.zero;

            numberText = numberObject.AddComponent<Text>();
        }
        else
        {
            numberText = existingNumber.GetComponent<Text>();
            if (numberText == null)
            {
                numberText = existingNumber.gameObject.AddComponent<Text>();
            }
        }

        numberText.text = (index + 1).ToString();
        numberText.alignment = TextAnchor.MiddleCenter;
        numberText.color = Color.white;
        numberText.fontSize = 17;
        numberText.fontStyle = FontStyle.Bold;
        numberText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (numberText.font == null)
        {
            numberText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        numberText.raycastTarget = false;

        Outline numberOutline = numberText.GetComponent<Outline>();
        if (numberOutline == null)
        {
            numberOutline = numberText.gameObject.AddComponent<Outline>();
        }
        numberOutline.effectColor = new Color(0f, 0f, 0f, 0.78f);
        numberOutline.effectDistance = new Vector2(1.4f, -1.4f);
        badgeRect.SetAsLastSibling();
    }

    private void EnsureSlotIcon(RectTransform slotRect, int index)
    {
        Transform existingIcon = slotRect.Find("Icon");
        Image iconImage;
        if (existingIcon == null)
        {
            GameObject iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(slotRect, false);
            iconObject.AddComponent<RectTransform>();
            iconImage = iconObject.AddComponent<Image>();
        }
        else
        {
            iconImage = existingIcon.GetComponent<Image>();
            if (iconImage == null)
            {
                iconImage = existingIcon.gameObject.AddComponent<Image>();
            }
        }

        RectTransform iconRect = iconImage.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 7f);
        iconRect.sizeDelta = iconSize;

        iconImage.sprite = GetSlotIcon(index);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        // 스프라이트 없으면 완전 투명. 있으면 기존 알파 값 보존 (dim 상태 유지)
        if (iconImage.sprite == null)
        {
            iconImage.color = new Color(1f, 1f, 1f, 0f);
        }
        else
        {
            Color c = iconImage.color;
            // 처음 생성 시 (알파가 0인 경우) 흰색으로. 이미 있던 경우 알파 유지.
            if (c.a <= 0.001f) c = Color.white;
            else c.r = c.g = c.b = 1f;
            iconImage.color = c;
        }
        iconImage.transform.SetSiblingIndex(Mathf.Max(0, slotRect.childCount - 2));
    }

    private void ReparentLegacyIcon(Transform parent, RectTransform slotRect, string slotName)
    {
        Transform legacyIcon = parent.Find($"Icon {slotName}");
        if (legacyIcon == null)
        {
            return;
        }

        legacyIcon.SetParent(slotRect, false);
        legacyIcon.gameObject.SetActive(false);
    }

    private void RefreshSelectedSlot()
    {
        if (slotBoxes == null)
        {
            return;
        }

        for (int i = 0; i < slotBoxes.Length; i++)
        {
            RectTransform slot = slotBoxes[i];
            if (slot == null)
            {
                continue;
            }

            bool selected = i == selectedSlotIndex;
            float scale = selected ? selectedSlotScale : 1f;
            slot.localScale = new Vector3(scale, scale, 1f);

            Image frameImage = slot.GetComponent<Image>();
            if (frameImage != null)
            {
                frameImage.sprite = selected && selectedSlotFrameSprite != null
                    ? selectedSlotFrameSprite
                    : slotFrameSprite;
                frameImage.type = Image.Type.Simple;
                frameImage.preserveAspect = true;
                frameImage.color = Color.white;
            }

            Image badgeImage = GetChildImage(slot, SlotNumberBadgeName);
            if (badgeImage != null)
            {
                badgeImage.sprite = selected && selectedNumberBadgeSprite != null
                    ? selectedNumberBadgeSprite
                    : numberBadgeSprite;
                badgeImage.type = Image.Type.Simple;
                badgeImage.preserveAspect = true;
                badgeImage.color = Color.white;

                Text numberText = badgeImage.GetComponentInChildren<Text>();
                if (numberText != null)
                {
                    numberText.color = Color.white;
                }

                badgeImage.transform.SetAsLastSibling();
            }

            if (slotOutlines != null && i < slotOutlines.Length && slotOutlines[i] != null)
            {
                slotOutlines[i].effectColor = selected ? selectedSlotBorderColor : slotBorderColor;
                slotOutlines[i].effectDistance = selected ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
            }
        }
    }

    private Transform FindHotbarTransform()
    {
        Transform hotbar = transform.Find("Toy Hotbar");
        return hotbar != null ? hotbar : transform;
    }

    private Vector2 GetSlotPosition(int index)
    {
        float firstX = -iconSpacing * (SlotCount - 1) * 0.5f;
        return new Vector2(firstX + iconSpacing * index, 24f);
    }

    private Sprite GetSlotIcon(int index)
    {
        if (slotIcons == null || index < 0 || index >= slotIcons.Length)
        {
            return null;
        }

        return slotIcons[index];
    }

    private static string GetSlotName(int index)
    {
        return index >= 0 && index < SlotNames.Length ? SlotNames[index] : $"Slot {index + 1}";
    }

    private void ConfigureHotbarContainer(Transform parent)
    {
        RectTransform rect = parent.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = hotbarAnchoredPosition;
            rect.sizeDelta = hotbarSize;
            rect.localScale = Vector3.one;
        }

        Image image = parent.GetComponent<Image>();
        if (image != null && parent.name == "Toy Hotbar")
        {
            image.enabled = false;
            image.raycastTarget = false;
        }

        for (int i = 0; i < SlotNames.Length; i++)
        {
            Transform legacySlot = parent.Find($"Slot {SlotNames[i]}");
            if (legacySlot != null)
            {
                legacySlot.gameObject.SetActive(false);
            }
        }
    }

    private static Image GetChildImage(RectTransform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private bool IsManagedSlotSprite(Sprite sprite)
    {
        return sprite == null || sprite == slotFrameSprite || sprite == selectedSlotFrameSprite;
    }

    private bool IsManagedBadgeSprite(Sprite sprite)
    {
        return sprite == null || sprite == numberBadgeSprite || sprite == selectedNumberBadgeSprite;
    }

    private static void HideLegacyGeneratedImage(RectTransform slotRect, string childName)
    {
        Transform child = slotRect.Find(childName);
        if (child == null)
        {
            return;
        }

        child.gameObject.SetActive(false);
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
