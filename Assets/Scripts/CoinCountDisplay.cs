using TMPro;
using UnityEngine;
using UnityEngine.UI;

// CoinWallet 의 현재 코인 수를 아무 Text/TMP_Text 오브젝트에 표시.
// Monitor Popup 의 TotalMoney 텍스트 등 임의 위치에 붙일 수 있음.
public sealed class CoinCountDisplay : MonoBehaviour
{
    [Header("표시할 텍스트 (하나만 연결해도 됨)")]
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private Text legacyText;

    [Header("포맷")]
    [Tooltip("{0} 자리에 코인 수 들어감. 예: \"{0:N0} 코인\" → \"1,234 코인\"")]
    [SerializeField] private string format = "{0:N0}";

    private void OnEnable()
    {
        CoinWallet.EnsureExists();
        if (CoinWallet.Instance != null)
        {
            CoinWallet.Instance.OnCoinsChanged += UpdateDisplay;
            UpdateDisplay(CoinWallet.Instance.Coins);
        }
    }

    private void OnDisable()
    {
        if (CoinWallet.Instance != null)
            CoinWallet.Instance.OnCoinsChanged -= UpdateDisplay;
    }

    private void UpdateDisplay(int amount)
    {
        string s = string.Format(format, amount);
        if (tmpText != null) tmpText.text = s;
        if (legacyText != null) legacyText.text = s;
    }
}
