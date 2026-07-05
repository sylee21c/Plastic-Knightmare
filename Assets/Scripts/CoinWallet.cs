using UnityEngine;

// 플레이어가 소지한 총 코인 관리. 싱글톤.
public sealed class CoinWallet : MonoBehaviour
{
    private static CoinWallet instance;
    public static CoinWallet Instance
    {
        get
        {
            if (instance == null) EnsureExists();
            return instance;
        }
    }

    public static void EnsureExists()
    {
        if (instance != null) return;
        CoinWallet found = FindAnyObjectByType<CoinWallet>();
        if (found != null) { instance = found; return; }

        GameObject go = new GameObject("CoinWallet");
        instance = go.AddComponent<CoinWallet>();
    }

    [SerializeField] private int startingCoins = 1000;

    public int Coins { get; private set; }
    public int CoinsEarnedThisNight { get; private set; }
    public event System.Action<int> OnCoinsChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        Coins = startingCoins;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Add(int amount) => Add(amount, false);

    public void Add(int amount, bool countAsNightEarnings)
    {
        if (amount <= 0) return;
        Coins += amount;
        if (countAsNightEarnings)
            CoinsEarnedThisNight += amount;
        OnCoinsChanged?.Invoke(Coins);
    }

    public void ResetNightEarnings()
    {
        CoinsEarnedThisNight = 0;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0 || Coins < amount) return false;
        Coins -= amount;
        OnCoinsChanged?.Invoke(Coins);
        return true;
    }

    public void SetCoins(int amount)
    {
        Coins = Mathf.Max(0, amount);
        OnCoinsChanged?.Invoke(Coins);
    }
}
