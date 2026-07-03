using UnityEngine;

public sealed class GhostAI : MonoBehaviour
{
    public enum TargetType { Bed, Player }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float hoverAmplitude = 0.12f;
    [SerializeField] private float hoverFrequency = 1.4f;

    [Header("Targeting")]
    [SerializeField] private float aggroRange = 5f;
    [SerializeField] private float deaggroRange = 8f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.3f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Health")]
    [SerializeField] private float maxHealth = 30f;

    public TargetType CurrentTarget { get; private set; } = TargetType.Bed;
    public event System.Action OnDeath;

    private Transform player;
    private Transform bed;
    private Animator animator;
    private float currentHealth;
    private float attackTimer;
    private float baseY;
    private float hoverOffset;
    private bool isDead;

    private void Start()
    {
        currentHealth = maxHealth;
        baseY = transform.position.y;
        hoverOffset = Random.Range(0f, Mathf.PI * 2f); // 유령마다 다른 위상
        animator = GetComponentInChildren<Animator>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;

        GameObject bedObj = GameObject.FindWithTag("Bed");
        if (bedObj != null) bed = bedObj.transform;
    }

    private void Update()
    {
        if (isDead) return;

        attackTimer -= Time.deltaTime;
        UpdateTarget();
        MoveTowardTarget();
        TryAttack();
        ApplyHover();
    }

    private void UpdateTarget()
    {
        if (player == null)
        {
            CurrentTarget = TargetType.Bed;
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        if (CurrentTarget == TargetType.Bed && distToPlayer < aggroRange)
            CurrentTarget = TargetType.Player;
        else if (CurrentTarget == TargetType.Player && distToPlayer > deaggroRange)
            CurrentTarget = TargetType.Bed;
    }

    private void MoveTowardTarget()
    {
        Transform target = GetTargetTransform();
        if (target == null) return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        bool isMoving = distance > attackRange;
        if (animator != null) animator.SetBool("IsMoving", isMoving);

        if (!isMoving) return;

        Vector3 direction = toTarget / distance;
        transform.position += direction * moveSpeed * Time.deltaTime;

        Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    private void TryAttack()
    {
        if (attackTimer > 0f) return;

        Transform target = GetTargetTransform();
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > attackRange) return;

        attackTimer = attackCooldown;

        // 플레이어 가드 중이면 데미지 감소
        PlayerController pc = target.GetComponent<PlayerController>();
        if (pc == null) pc = target.GetComponentInParent<PlayerController>();
        float damage = attackDamage * (pc != null ? pc.GetGuardMultiplier() : 1f);

        Damageable damageable = target.GetComponent<Damageable>();
        if (damageable == null) damageable = target.GetComponentInParent<Damageable>();
        damageable?.TakeDamage(damage);
    }

    private void ApplyHover()
    {
        Vector3 pos = transform.position;
        pos.y = baseY + Mathf.Sin(Time.time * hoverFrequency + hoverOffset) * hoverAmplitude;
        transform.position = pos;
    }

    private Transform GetTargetTransform()
    {
        return CurrentTarget == TargetType.Player ? player : bed;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;
        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            isDead = true;
            if (animator != null) animator.SetBool("IsMoving", false);
            OnDeath?.Invoke();
            Destroy(gameObject, 0.5f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, deaggroRange);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
