using System.Collections.Generic;
using UnityEngine;

// 플레이어가 소지한 브릭 수 관리. 싱글톤.
// key 예시: "2x2", "2x1"
public sealed class BrickInventory : MonoBehaviour
{
    private static BrickInventory instance;
    // 프로퍼티에서 자동 생성 X. 필요한 곳에서 EnsureExists() 명시 호출.
    public static BrickInventory Instance => instance;

    public static void EnsureExists()
    {
        if (instance != null) return;
        BrickInventory found = FindAnyObjectByType<BrickInventory>();
        if (found != null) { instance = found; return; }
        GameObject go = new GameObject("BrickInventory");
        instance = go.AddComponent<BrickInventory>();
    }

    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

    // (key, newCount) 발생 시마다 UI 등이 반응
    public event System.Action<string, int> OnCountChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public int GetCount(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        return counts.TryGetValue(key, out int v) ? v : 0;
    }

    public void Add(string key, int amount)
    {
        if (string.IsNullOrEmpty(key) || amount <= 0) return;
        int current = GetCount(key);
        counts[key] = current + amount;
        OnCountChanged?.Invoke(key, counts[key]);
    }

    public bool TryConsume(string key, int amount)
    {
        if (string.IsNullOrEmpty(key) || amount <= 0) return false;
        int current = GetCount(key);
        if (current < amount) return false;
        counts[key] = current - amount;
        OnCountChanged?.Invoke(key, counts[key]);
        return true;
    }

    public void SetCount(string key, int amount)
    {
        if (string.IsNullOrEmpty(key)) return;
        counts[key] = Mathf.Max(0, amount);
        OnCountChanged?.Invoke(key, counts[key]);
    }
}
