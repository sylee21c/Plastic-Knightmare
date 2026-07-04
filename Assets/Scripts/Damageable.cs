using UnityEngine;

public sealed class Damageable : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;

    public event System.Action<float, float> OnHealthChanged; // current, max
    public event System.Action OnDeath;

    private bool initialized;

    private void Awake()
    {
        if (!initialized)
        {
            CurrentHealth = maxHealth;
            initialized = true;
        }
    }

    // 런타임에 최대 HP 설정 (스폰 시 사용). Awake보다 먼저 호출 가능.
    public void SetMaxHealth(float value, bool refill = true)
    {
        maxHealth = Mathf.Max(1f, value);
        if (refill || !initialized) CurrentHealth = maxHealth;
        else CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
        initialized = true;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void FullHeal()
    {
        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        if (CurrentHealth <= 0f)
            OnDeath?.Invoke();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void Revive(float health)
    {
        CurrentHealth = Mathf.Clamp(health, 1f, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }
}
