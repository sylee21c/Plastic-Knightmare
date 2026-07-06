using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 모니터 팝업에 붙는 동료 구매 슬롯. BrickShopItem 과 병렬 구조.
[ExecuteAlways]
public sealed class CompanionShopItem : MonoBehaviour
{
    public static event System.Action OnSuccessfulPurchase;

    [Header("Config")]
    [SerializeField] private CompanionDefinition definition;
    [SerializeField] private int unitPrice = 500;
    [SerializeField] private int quantity = 1;
    [SerializeField, Min(1)] private int minQuantity = 1;
    [SerializeField, Min(1)] private int maxQuantity = 10;

    [Header("UI References (TMP)")]
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
        if (definition == null)
        {
            Debug.LogWarning("[CompanionShopItem] Definition 이 비어있음");
            FlashPurchase(false);
            return;
        }

        int total = TotalPrice;
        CoinWallet.EnsureExists();
        if (CoinWallet.Instance == null)
        {
            FlashPurchase(false);
            return;
        }
        if (!CoinWallet.Instance.TrySpend(total))
        {
            Debug.Log($"[CompanionShopItem/{definition.displayName}] 코인 부족: 필요 {total}, 보유 {CoinWallet.Instance.Coins}");
            FlashPurchase(false);
            return;
        }

        CompanionInventory.EnsureExists();
        int added = 0;
        for (int i = 0; i < quantity; i++)
        {
            if (CompanionInventory.Instance.TryAdd(definition.companionId, 1, definition.preferredSlot))
                added++;
            else
                break; // 슬롯 가득 참 → 남은 수량 만큼 환불
        }

        int refund = (quantity - added) * unitPrice;
        if (refund > 0 && CoinWallet.Instance != null)
            CoinWallet.Instance.Add(refund);

        FlashPurchase(added > 0);
        if (added > 0)
        {
            OnSuccessfulPurchase?.Invoke();
        }
        Debug.Log($"[CompanionShopItem] {definition.displayName} x{added} 구매 완료 (환불 {refund})");
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

    private void OnValidate() { RefreshDisplay(); }
}
