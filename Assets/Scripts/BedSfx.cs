using UnityEngine;

// Bed 공격받을 때 효과음 재생.
// Bed GameObject 에 붙이면 됨. 같은 오브젝트의 Damageable 을 자동 구독.
[RequireComponent(typeof(Damageable))]
public sealed class BedSfx : MonoBehaviour
{
    private Damageable damageable;
    private float lastHealth;

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
    }

    private void OnEnable()
    {
        if (damageable == null) return;
        damageable.OnHealthChanged += HandleHealthChanged;
        lastHealth = damageable.CurrentHealth;
    }

    private void OnDisable()
    {
        if (damageable != null)
            damageable.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(float current, float max)
    {
        // HP 가 실제로 감소했을 때만 재생 (초기화/힐 제외).
        // 침대 파괴 시 별도 사운드는 없음 → 게임오버 곡이 페이드인되며 대체.
        if (current < lastHealth - 0.01f && current > 0f)
            SFXManager.PlayGlobal(SFXManager.Sfx.BedHit);
        lastHealth = current;
    }
}
