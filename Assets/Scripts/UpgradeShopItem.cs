using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 강화 구매 슬롯. 단계별 목표 수치와 비용을 배열로 관리.
// 현재 단계 값 → 다음 단계 값 표시, 구매 시 코인 차감 + 플레이어 스탯 적용.
[ExecuteAlways]
public sealed class UpgradeShopItem : MonoBehaviour
{
    public enum UpgradeType { AttackDamage, MaxHealth }

    [System.Serializable]
    public class UpgradeLevel
    {
        [Tooltip("이 단계로 강화됐을 때의 최종 수치")]
        public float value;
        [Tooltip("이 단계로 강화하는 데 드는 코인 비용")]
        public int cost;
    }

    [Header("Config")]
    [SerializeField] private UpgradeType upgradeType = UpgradeType.AttackDamage;
    [Tooltip("아무 강화도 안 됐을 때의 기본 수치")]
    [SerializeField] private float baseValue = 25f;
    [Tooltip("단계별 목표값과 비용 배열. Element 0 = 첫 강화")]
    [SerializeField] private UpgradeLevel[] levels;
    [Tooltip("현재 도달한 단계. 0 = 아직 강화 안 됨, N = levels[N-1] 까지 강화됨")]
    [SerializeField] private int currentLevelIndex = 0;

    [Header("UI References (TMP)")]
    [SerializeField] private TMP_Text currentValueText;
    [SerializeField] private TMP_Text upgradedValueText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonLabel;

    private PurchaseButtonFlash buttonFlash;

    [Header("Labels")]
    [SerializeField] private string valueSuffix = "";
    [SerializeField] private string buyButtonFormat = "강화 ({0:N0})";
    [SerializeField] private string maxedText = "MAX";

    public bool IsMaxed => levels == null || currentLevelIndex >= levels.Length;
    public float CurrentValue => (currentLevelIndex <= 0 || levels == null)
        ? baseValue : levels[Mathf.Clamp(currentLevelIndex - 1, 0, levels.Length - 1)].value;
    public float UpgradedValue => IsMaxed ? CurrentValue : levels[currentLevelIndex].value;
    public int CurrentCost => IsMaxed ? 0 : levels[currentLevelIndex].cost;

    private void OnEnable()
    {
        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(Buy);
            buyButton.onClick.AddListener(Buy);
        }
        EnsureButtonFlash();
        RefreshDisplay();
    }

    public void Buy()
    {
        if (IsMaxed)
        {
            Debug.Log($"[UpgradeShopItem/{upgradeType}] 이미 최대 단계");
            FlashPurchase(false);
            return;
        }
        if (CoinWallet.Instance == null)
        {
            Debug.LogWarning("[UpgradeShopItem] CoinWallet 없음");
            FlashPurchase(false);
            return;
        }

        int cost = CurrentCost;
        if (!CoinWallet.Instance.TrySpend(cost))
        {
            Debug.Log($"[UpgradeShopItem/{upgradeType}] 코인 부족: 필요 {cost}, 보유 {CoinWallet.Instance.Coins}");
            FlashPurchase(false);
            return;
        }

        currentLevelIndex++;
        ApplyCurrentStatToPlayer();
        RefreshDisplay();
        FlashPurchase(true);
        Debug.Log($"[Upgrade] {upgradeType} → {CurrentValue} (Lv {currentLevelIndex})");
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

    private void ApplyCurrentStatToPlayer()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc == null) return;

        float value = CurrentValue;
        switch (upgradeType)
        {
            case UpgradeType.AttackDamage:
                pc.SetAttackDamage(value);
                break;
            case UpgradeType.MaxHealth:
                Damageable dmg = pc.GetComponent<Damageable>();
                if (dmg != null) dmg.SetMaxHealth(value, refill: true);
                break;
        }
    }

    private void RefreshDisplay()
    {
        if (currentValueText != null)
            currentValueText.text = Mathf.RoundToInt(CurrentValue).ToString() + valueSuffix;

        if (IsMaxed)
        {
            if (upgradedValueText != null) upgradedValueText.text = maxedText;
            if (costText != null) costText.text = "";
            if (buyButtonLabel != null) buyButtonLabel.text = maxedText;
            if (buyButton != null) buyButton.interactable = false;
        }
        else
        {
            if (upgradedValueText != null)
                upgradedValueText.text = Mathf.RoundToInt(UpgradedValue).ToString() + valueSuffix;
            if (costText != null)
                costText.text = CurrentCost.ToString("N0");
            if (buyButtonLabel != null)
                buyButtonLabel.text = string.Format(buyButtonFormat, CurrentCost);
            if (buyButton != null)
                buyButton.interactable = true;
        }
    }

    private void OnValidate()
    {
        if (levels != null)
            currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, levels.Length);
        RefreshDisplay();
    }

    // 씬 시작 시 플레이어 스탯을 이 강화 단계에 맞춰 동기화
    public void SyncStatToPlayer() => ApplyCurrentStatToPlayer();

    // ── Inspector 컨텍스트 메뉴로 프리셋 자동 채우기 ──

    [ContextMenu("Preset: Attack Damage (25 → 220)")]
    private void PresetAttack()
    {
        upgradeType = UpgradeType.AttackDamage;
        baseValue = 25f;
        levels = new UpgradeLevel[]
        {
            new UpgradeLevel { value = 35f,  cost = 300 },
            new UpgradeLevel { value = 50f,  cost = 800 },
            new UpgradeLevel { value = 70f,  cost = 2000 },
            new UpgradeLevel { value = 95f,  cost = 5000 },
            new UpgradeLevel { value = 130f, cost = 12000 },
            new UpgradeLevel { value = 170f, cost = 25000 },
            new UpgradeLevel { value = 220f, cost = 50000 },
        };
        currentLevelIndex = 0;
        RefreshDisplay();
    }

    [ContextMenu("Preset: Max HP (100 → 520)")]
    private void PresetHP()
    {
        upgradeType = UpgradeType.MaxHealth;
        baseValue = 100f;
        levels = new UpgradeLevel[]
        {
            new UpgradeLevel { value = 130f, cost = 250 },
            new UpgradeLevel { value = 170f, cost = 700 },
            new UpgradeLevel { value = 220f, cost = 1800 },
            new UpgradeLevel { value = 280f, cost = 4500 },
            new UpgradeLevel { value = 350f, cost = 10000 },
            new UpgradeLevel { value = 430f, cost = 22000 },
            new UpgradeLevel { value = 520f, cost = 45000 },
        };
        currentLevelIndex = 0;
        RefreshDisplay();
    }
}
