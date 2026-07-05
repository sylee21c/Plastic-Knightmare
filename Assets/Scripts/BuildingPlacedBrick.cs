using UnityEngine;

public sealed class BuildingPlacedBrick : MonoBehaviour
{
    public Vector2Int Cell { get; set; }
    public int StackIndex { get; set; }
    public string InventoryKey { get; set; }

    private Damageable damageable;
    private float lastHealth;
    private bool subscribed;

    private void Start()
    {
        TrySubscribe();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (damageable != null)
        {
            damageable.OnHealthChanged -= HandleHealthChanged;
            damageable.OnDeath -= HandleDeath;
        }
        subscribed = false;
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        damageable = GetComponent<Damageable>();
        if (damageable == null) return;

        damageable.OnHealthChanged += HandleHealthChanged;
        damageable.OnDeath += HandleDeath;
        lastHealth = damageable.CurrentHealth;
        subscribed = true;
    }

    // BuildingModeController.SpawnBrick 에서 배치 직후 호출.
    public void HandlePlaced()
    {
        SFXManager.PlayGlobal(SFXManager.Sfx.BrickPlace);
    }

    private void HandleHealthChanged(float current, float max)
    {
        // HP 가 실제로 감소한 경우만 피격 사운드 (초기화/힐 제외).
        // 파괴 사운드는 HandleDeath 에서 별도 재생 → 중복 방지.
        if (current < lastHealth - 0.01f && current > 0f)
            SFXManager.PlayGlobal(SFXManager.Sfx.BrickHit);
        lastHealth = current;
    }

    private void HandleDeath()
    {
        SFXManager.PlayGlobal(SFXManager.Sfx.BrickBreak);
    }
}
