using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 브릭 구매 슬롯. 화살표 버튼으로 수량 조절, 총 가격 자동 계산, 구매 시 코인 차감.
[ExecuteAlways]
public sealed class BrickShopItem : MonoBehaviour
{
    [Header("Config (Inspector 조정)")]
    [SerializeField] private string itemName = "2x2 Brick";
    [Tooltip("BrickInventory 저장 키. BuildingHotbarUI/BrickDefinition displayName 과 일치해야 함 (예: \"2x2\", \"2x1\")")]
    [SerializeField] private string inventoryKey = "2x2";
    [SerializeField] private int unitPrice = 50;
    [SerializeField] private int quantity = 1;
    [SerializeField, Min(1)] private int minQuantity = 1;
    [SerializeField, Min(1)] private int maxQuantity = 99;

    [Header("UI References (씬에서 드래그, TMP)")]
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text totalPriceText;
    [SerializeField] private Button decreaseButton;
    [SerializeField] private Button increaseButton;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonLabel;

    private PurchaseButtonFlash buttonFlash;

    public int UnitPrice { get => unitPrice; set { unitPrice = Mathf.Max(0, value); RefreshDisplay(); } }
    public int Quantity => quantity;
    public int TotalPrice => unitPrice * quantity;

    private void OnEnable()
    {
        if (decreaseButton != null) { decreaseButton.onClick.RemoveListener(Decrease); decreaseButton.onClick.AddListener(Decrease); }
        if (increaseButton != null) { increaseButton.onClick.RemoveListener(Increase); increaseButton.onClick.AddListener(Increase); }
        if (buyButton != null)     { buyButton.onClick.RemoveListener(Buy);         buyButton.onClick.AddListener(Buy); }
        EnsureButtonFlash();
        RefreshDisplay();
    }

    public void Increase()
    {
        quantity = Mathf.Clamp(quantity + 1, minQuantity, maxQuantity);
        RefreshDisplay();
    }

    public void Decrease()
    {
        quantity = Mathf.Clamp(quantity - 1, minQuantity, maxQuantity);
        RefreshDisplay();
    }

    public void Buy()
    {
        int total = TotalPrice;
        if (CoinWallet.Instance == null)
        {
            Debug.LogWarning($"[BrickShopItem/{itemName}] CoinWallet.Instance 가 없음");
            FlashPurchase(false);
            return;
        }
        if (!CoinWallet.Instance.TrySpend(total))
        {
            Debug.Log($"[BrickShopItem/{itemName}] 코인 부족: 필요 {total}, 보유 {CoinWallet.Instance.Coins}");
            FlashPurchase(false);
            return;
        }
        // 인벤토리에 지급
        BrickInventory.EnsureExists();
        BrickInventory.Instance.Add(inventoryKey, quantity);
        FlashPurchase(true);
        Debug.Log($"[BrickShopItem] {itemName} x{quantity} 구매 완료 ({total} coins) → 보유 {BrickInventory.Instance.GetCount(inventoryKey)}개");
    }

    private void EnsureButtonFlash()
    {
        if (buyButton == null || buttonFlash != null) return;
        buttonFlash = buyButton.GetComponent<PurchaseButtonFlash>();
        if (buttonFlash == null) buttonFlash = buyButton.gameObject.AddComponent<PurchaseButtonFlash>();
    }

    private void FlashPurchase(bool success)
    {
        EnsureButtonFlash();
        if (buttonFlash != null) buttonFlash.Flash(buyButton, success);
        SFXManager.PlayGlobal(success ? SFXManager.Sfx.Purchase : SFXManager.Sfx.PurchaseFail);
    }

    private void RefreshDisplay()
    {
        quantity = Mathf.Clamp(quantity, minQuantity, maxQuantity);
        if (quantityText != null)   quantityText.text = quantity.ToString();
        if (totalPriceText != null) totalPriceText.text = TotalPrice.ToString("N0");
        if (buyButtonLabel != null) buyButtonLabel.text = $"구매 ({TotalPrice:N0})";
    }

    private void OnValidate() { RefreshDisplay(); } // Editor에서 값 바꾸면 즉시 반영
}
