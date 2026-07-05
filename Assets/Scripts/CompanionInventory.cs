using System.Collections.Generic;
using UnityEngine;

// Companion toy inventory. Hotbar slots 3-5 map to 0-based indices 2-4.
// Placing a toy consumes one item; when count reaches 0, its hotbar slot is cleared.
public sealed class CompanionInventory : MonoBehaviour
{
    private static CompanionInventory instance;
    // 프로퍼티에서 자동 생성 X. 필요한 곳에서 EnsureExists() 명시 호출.
    public static CompanionInventory Instance => instance;

    public static void EnsureExists()
    {
        if (instance != null) return;
        CompanionInventory found = FindAnyObjectByType<CompanionInventory>();
        if (found != null) { instance = found; return; }
        GameObject go = new GameObject("CompanionInventory");
        instance = go.AddComponent<CompanionInventory>();
    }

    [Header("Slot Range (0-based hotbar index)")]
    [Tooltip("First companion slot. Default 2 = hotbar key 3.")]
    [SerializeField] private int firstSlotIndex = 2;
    [Tooltip("Last companion slot. Default 4 = hotbar key 5.")]
    [SerializeField] private int lastSlotIndex = 4;

    // slotIndex -> companionId
    private readonly Dictionary<int, string> slotToId = new Dictionary<int, string>();
    // companionId -> count
    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

    public event System.Action OnInventoryChanged;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public int FirstSlotIndex => firstSlotIndex;
    public int LastSlotIndex => lastSlotIndex;

    public int GetCount(string companionId)
    {
        if (string.IsNullOrEmpty(companionId)) return 0;
        return counts.TryGetValue(companionId, out int v) ? v : 0;
    }

    public string GetIdInSlot(int slotIndex)
    {
        return slotToId.TryGetValue(slotIndex, out string id) ? id : null;
    }

    public int GetSlotOfId(string id)
    {
        if (string.IsNullOrEmpty(id)) return -1;
        foreach (KeyValuePair<int, string> kv in slotToId)
            if (kv.Value == id) return kv.Key;
        return -1;
    }

    public int FindFirstEmptySlot()
    {
        for (int i = firstSlotIndex; i <= lastSlotIndex; i++)
            if (!slotToId.ContainsKey(i)) return i;
        return -1;
    }

    public bool TryAdd(string id, int amount = 1, int preferredSlot = -1)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0) return false;

        if (GetSlotOfId(id) == -1)
        {
            int targetSlot;
            if (preferredSlot >= firstSlotIndex && preferredSlot <= lastSlotIndex
                && !slotToId.ContainsKey(preferredSlot))
                targetSlot = preferredSlot;
            else
                targetSlot = FindFirstEmptySlot();

            if (targetSlot == -1)
            {
                Debug.LogWarning("[CompanionInventory] No empty companion hotbar slot.");
                return false;
            }
            slotToId[targetSlot] = id;
        }

        counts[id] = GetCount(id) + amount;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool TryConsume(string id, int amount = 1)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0) return false;
        int current = GetCount(id);
        if (current < amount) return false;

        int remaining = current - amount;
        if (remaining <= 0)
        {
            counts.Remove(id);
            int slot = GetSlotOfId(id);
            if (slot != -1) slotToId.Remove(slot);
        }
        else
        {
            counts[id] = remaining;
        }
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool HasAny(string id) => GetCount(id) > 0;
}
