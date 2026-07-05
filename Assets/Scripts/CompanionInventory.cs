using System.Collections.Generic;
using UnityEngine;

// 동료 장난감 인벤토리. 슬롯 3~5 (0-based 2~4) 에 동적으로 할당.
// 필드 배치 시 소지량 감소. 0 되면 해당 슬롯 자동 해제.
public sealed class CompanionInventory : MonoBehaviour
{
    private static CompanionInventory instance;
    public static CompanionInventory Instance
    {
        get { if (instance == null) EnsureExists(); return instance; }
    }

    public static void EnsureExists()
    {
        if (instance != null) return;
        CompanionInventory found = FindAnyObjectByType<CompanionInventory>();
        if (found != null) { instance = found; return; }
        GameObject go = new GameObject("CompanionInventory");
        instance = go.AddComponent<CompanionInventory>();
    }

    [Header("Slot Range (0-based hotbar index)")]
    [Tooltip("첫 동료 슬롯 (기본 2 = 핫바 3번칸)")]
    [SerializeField] private int firstSlotIndex = 2;
    [Tooltip("마지막 동료 슬롯 (기본 4 = 핫바 5번칸)")]
    [SerializeField] private int lastSlotIndex = 4;

    // slotIndex → companionId
    private readonly Dictionary<int, string> slotToId = new Dictionary<int, string>();
    // companionId → 소지 수
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

    // 첫번째 빈 슬롯 반환 (없으면 -1)
    public int FindFirstEmptySlot()
    {
        for (int i = firstSlotIndex; i <= lastSlotIndex; i++)
            if (!slotToId.ContainsKey(i)) return i;
        return -1;
    }

    // 동료를 인벤토리에 추가. 이미 있으면 카운트 증가, 없으면 빈 슬롯 할당.
    // preferredSlot >= 0 이고 비어있으면 우선 그 슬롯 사용.
    public bool TryAdd(string id, int amount = 1, int preferredSlot = -1)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0) return false;

        if (GetSlotOfId(id) == -1)
        {
            int targetSlot = -1;
            if (preferredSlot >= firstSlotIndex && preferredSlot <= lastSlotIndex
                && !slotToId.ContainsKey(preferredSlot))
                targetSlot = preferredSlot;
            else
                targetSlot = FindFirstEmptySlot();

            if (targetSlot == -1)
            {
                Debug.LogWarning("[CompanionInventory] 슬롯 가득 참");
                return false;
            }
            slotToId[targetSlot] = id;
        }

        counts[id] = GetCount(id) + amount;
        OnInventoryChanged?.Invoke();
        return true;
    }

    // 필드 배치 시 호출. 소지량 감소. 0 되면 슬롯 해제.
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
