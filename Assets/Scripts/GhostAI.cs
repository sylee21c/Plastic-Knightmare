using System.Collections;
using UnityEngine;

// 유령 AI:
// - 기본 타겟: 침대
// - 플레이어가 aggroRange 안이면 플레이어를 쫓아감
// - 경로에 레고 브릭이 있으면 그 브릭을 먼저 공격
// - 공격 시 오뚝이처럼 앞으로 살짝 기울였다가 돌아옴
[RequireComponent(typeof(Damageable))]
public sealed class GhostAI : MonoBehaviour
{
    public enum TargetType { Bed, Player, Companion, Brick }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float hoverAmplitude = 0.12f;
    [SerializeField] private float hoverFrequency = 1.4f;
    [SerializeField] private float flightHeight = 0.25f; // 지면 기준 유령 중심 높이
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Targeting")]
    [SerializeField] private float aggroRange = 5f;
    [SerializeField] private float deaggroRange = 8f;
    [SerializeField] private float brickCheckDistance = 1.6f;
    [SerializeField] private LayerMask brickBlockLayers = ~0;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Attack Tilt Animation")]
    [SerializeField] private float tiltDuration = 0.45f;
    [SerializeField] private float tiltAngle = 32f;

    [Header("Death Animation")]
    [SerializeField] private float deathDuration = 1.1f;
    [SerializeField] private float deathTiltAngle = 70f; // 뒤로 자빠지는 각도

    [Header("Coin Drops")]
    [SerializeField] private GameObject baseCoinPrefab;
    [SerializeField] private int baseCoinValue = 100;
    [SerializeField] private GameObject bonusCoinPrefab;
    [SerializeField] private int bonusCoinValue = 500;
    [Range(0f, 1f)]
    [SerializeField] private float bonusCoinChance = 0.2f;

    public TargetType CurrentTarget { get; private set; } = TargetType.Bed;
    public Vector3 LastMoveDirection => lastMoveDirection;
    public event System.Action OnDeath;

    private Damageable damageable;
    private Transform player;
    private Transform bed;
    private CompanionToy companionTarget;
    private Animator animator;
    private Rigidbody rb;
    private Collider[] ownColliders;
    private Collider[] playerColliders;

    private float attackTimer;
    private float baseY;
    private float hoverPhase;
    private bool isDead;
    private bool ignoringPlayerCollision;
    private float stunTimer;
    private float knockbackTimer;
    private float knockbackDuration;
    private Vector3 knockbackVelocity;
    private Vector3 lastMoveDirection = Vector3.forward;

    // 오뚝이 공격 애니메이션
    private float tiltTimer;

    private float lastHealthForSfx;

    // 경로 차단 브릭
    private BuildingPlacedBrick blockingBrick;
    private Damageable blockingBrickHealth;

    private void Awake()
    {
        damageable = GetComponent<Damageable>();

        // 회전 자기 제어를 위해 Rigidbody 강제 세팅
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        ownColliders = GetComponentsInChildren<Collider>();
    }

    private void Start()
    {
        // BuildingGridOverlay가 이미 바닥 Y를 알고 있으므로 그걸 사용 (천장 오탐 방지)
        BuildingGridOverlay grid = FindAnyObjectByType<BuildingGridOverlay>();
        if (grid != null && grid.CellSize > 0f)
        {
            baseY = grid.SurfaceY + flightHeight;
        }
        else
        {
            // 폴백: 유령 위치에서 아래로 raycast (자기 콜라이더 스킵)
            baseY = FindGroundYFallback();
        }

        // 스폰 위치도 baseY로 스냅
        Vector3 pos = transform.position;
        pos.y = baseY;
        transform.position = pos;

        hoverPhase = Random.Range(0f, Mathf.PI * 2f);
        animator = GetComponentInChildren<Animator>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;
        if (player != null) playerColliders = player.GetComponentsInChildren<Collider>();

        GameObject bedObj = GameObject.FindWithTag("Bed");
        if (bedObj != null) bed = bedObj.transform;

        damageable.OnDeath += HandleDeath;
        damageable.OnHealthChanged += HandleHealthChanged;
        lastHealthForSfx = damageable.CurrentHealth;
    }

    private void OnDestroy()
    {
        SetPlayerCollisionIgnored(false);
        if (damageable != null)
        {
            damageable.OnDeath -= HandleDeath;
            damageable.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (current < lastHealthForSfx - 0.01f && current > 0f)
            SFXManager.PlayGlobal(SFXManager.Sfx.GhostHit);
        lastHealthForSfx = current;
    }

    // 자기 콜라이더를 스킵하고 아래로만 raycast (천장/자기몸통 오탐 방지)
    private float FindGroundYFallback()
    {
        Vector3 rayStart = transform.position;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 30f,
            groundLayers, QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform))
                continue;
            return h.point.y + flightHeight;
        }

        return transform.position.y;
    }

    private void Update()
    {
        if (isDead) return;

        // 게임오버: 이동/공격/hover 전부 정지. 애니메이터도 stop.
        if (GameOverUIController.IsGameOver)
        {
            if (animator != null) SetBoolSafe(animator, "IsMoving", false);
            return;
        }

        attackTimer -= Time.deltaTime;

        if (stunTimer > 0f || knockbackTimer > 0f)
        {
            UpdateCrowdControl();
            ApplyHover();
            return;
        }

        UpdatePlayerCollisionPassThrough();
        UpdateTilt();
        UpdateTarget();
        CheckBrickInPath();
        MoveTowardTarget();
        TryAttack();
        ApplyHover();
    }

    // ── 타겟 결정 ─────────────────────────────────────────────────

    private void UpdateTarget()
    {
        CompanionToy nearestCompanion = FindNearestAliveCompanionInRange(aggroRange);

        if (CurrentTarget == TargetType.Companion)
        {
            if (!IsValidCompanionTarget(companionTarget)
                || Vector3.Distance(transform.position, companionTarget.transform.position) > deaggroRange)
            {
                companionTarget = null;
                CurrentTarget = TargetType.Bed;
            }
            else
            {
                return;
            }
        }

        if (player == null)
        {
            if (nearestCompanion != null)
            {
                companionTarget = nearestCompanion;
                CurrentTarget = TargetType.Companion;
            }
            else
            {
                CurrentTarget = TargetType.Bed;
            }
            return;
        }

        // 플레이어가 무력화 상태면 침대로 향하도록 강제
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null && pc.IsIncapacitated)
        {
            if (nearestCompanion != null)
            {
                companionTarget = nearestCompanion;
                CurrentTarget = TargetType.Companion;
            }
            else
            {
                CurrentTarget = TargetType.Bed;
            }
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        if (CurrentTarget == TargetType.Bed)
        {
            if (distToPlayer < aggroRange
                && (nearestCompanion == null || distToPlayer <= Vector3.Distance(transform.position, nearestCompanion.transform.position)))
            {
                companionTarget = null;
                CurrentTarget = TargetType.Player;
            }
            else if (nearestCompanion != null)
            {
                companionTarget = nearestCompanion;
                CurrentTarget = TargetType.Companion;
            }
        }
        else if (CurrentTarget == TargetType.Player && distToPlayer > deaggroRange)
        {
            CurrentTarget = TargetType.Player;
            if (nearestCompanion != null)
            {
                companionTarget = nearestCompanion;
                CurrentTarget = TargetType.Companion;
            }
            else
            {
                CurrentTarget = TargetType.Bed;
            }
        }
        else if (CurrentTarget == TargetType.Player)
        {
            companionTarget = null;
        }
    }

    private CompanionToy FindNearestAliveCompanionInRange(float range)
    {
#if UNITY_2023_1_OR_NEWER
        CompanionToy[] all = Object.FindObjectsByType<CompanionToy>(FindObjectsSortMode.None);
#else
        CompanionToy[] all = Object.FindObjectsOfType<CompanionToy>();
#endif
        CompanionToy nearest = null;
        float bestDist = float.MaxValue;
        foreach (CompanionToy companion in all)
        {
            if (!IsValidCompanionTarget(companion)) continue;

            float dist = Vector3.Distance(transform.position, companion.transform.position);
            if (dist <= range && dist < bestDist)
            {
                bestDist = dist;
                nearest = companion;
            }
        }

        return nearest;
    }

    private static bool IsValidCompanionTarget(CompanionToy companion)
    {
        if (companion == null) return false;
        // 트랩(꼬꼬닭 등)은 유령이 인식하지 않음 → 지나가다 밟히면 발동만.
        if (!companion.IsTargetableByGhost) return false;

        Damageable health = companion.GetComponent<Damageable>();
        return health != null && !health.IsDead;
    }

    private Transform GetMainTargetTransform()
    {
        if (CurrentTarget == TargetType.Player) return player;
        if (CurrentTarget == TargetType.Companion && IsValidCompanionTarget(companionTarget))
            return companionTarget.transform;

        if (CurrentTarget == TargetType.Companion)
        {
            companionTarget = null;
            CurrentTarget = TargetType.Bed;
        }

        return bed;
    }

    // 진행 방향에 브릭이 있으면 그 브릭을 임시 타겟으로
    private void CheckBrickInPath()
    {
        Transform mainTarget = GetMainTargetTransform();
        if (mainTarget == null) { blockingBrick = null; blockingBrickHealth = null; return; }

        Vector3 toTarget = mainTarget.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        if (dist < 0.01f) return;

        Vector3 origin = transform.position;
        origin.y = baseY; // hover 영향 배제
        Vector3 dir = toTarget / dist;

        float rayLen = Mathf.Min(brickCheckDistance, dist);
        if (Physics.Raycast(origin, dir, out RaycastHit hit, rayLen, brickBlockLayers, QueryTriggerInteraction.Ignore))
        {
            BuildingPlacedBrick brick = hit.collider.GetComponentInParent<BuildingPlacedBrick>();
            if (brick != null)
            {
                // 같은 칸의 최상단(가장 높이 쌓인) 브릭을 대상으로
                BuildingPlacedBrick top = FindTopBrickInStack(brick.Cell);
                if (top != null) brick = top;

                blockingBrick = brick;
                blockingBrickHealth = brick.GetComponent<Damageable>();
                return;
            }
        }

        blockingBrick = null;
        blockingBrickHealth = null;
    }

    private static BuildingPlacedBrick FindTopBrickInStack(Vector2Int cell)
    {
        BuildingPlacedBrick top = null;
        int topIdx = -1;
#if UNITY_2023_1_OR_NEWER
        BuildingPlacedBrick[] all = Object.FindObjectsByType<BuildingPlacedBrick>(FindObjectsSortMode.None);
#else
        BuildingPlacedBrick[] all = Object.FindObjectsOfType<BuildingPlacedBrick>();
#endif
        foreach (BuildingPlacedBrick b in all)
        {
            if (b == null) continue;
            if (b.Cell == cell && b.StackIndex > topIdx)
            {
                topIdx = b.StackIndex;
                top = b;
            }
        }
        return top;
    }

    // ── 이동 ──────────────────────────────────────────────────────

    private void MoveTowardTarget()
    {
        // 공격 기울임 중에는 회전/이동 억제
        if (tiltTimer > 0f) return;

        Transform target = GetActiveTargetTransform();
        if (target == null) return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        bool inAttackRange = distance <= attackRange;
        bool isMoving = !inAttackRange;
        if (animator != null) SetBoolSafe(animator, "IsMoving", isMoving);

        if (!isMoving)
        {
            RotateTowardDirection(toTarget);
            return;
        }

        Vector3 direction = toTarget / distance;
        lastMoveDirection = direction;
        transform.position += direction * moveSpeed * Time.deltaTime;

        RotateTowardDirection(direction);
    }

    // ── 공격 ──────────────────────────────────────────────────────

    private void TryAttack()
    {
        if (attackTimer > 0f || tiltTimer > 0f) return;

        Transform target = GetActiveTargetTransform();
        if (target == null) return;

        Vector3 flatDelta = target.position - transform.position;
        flatDelta.y = 0f;
        float dist = flatDelta.magnitude;
        if (dist > attackRange) return;
        RotateTowardDirection(flatDelta);

        // 데미지 계산
        float damage = attackDamage;

        Damageable targetHealth = null;

        if (blockingBrick != null && blockingBrickHealth != null)
        {
            targetHealth = blockingBrickHealth;
        }
        else
        {
            targetHealth = target.GetComponent<Damageable>();
            if (targetHealth == null) targetHealth = target.GetComponentInParent<Damageable>();

            // 플레이어 가드 감소 반영
            PlayerController pc = target.GetComponent<PlayerController>()
                                  ?? target.GetComponentInParent<PlayerController>();
            if (pc != null) damage *= pc.GetGuardMultiplier();
        }

        if (targetHealth == null) return;

        attackTimer = attackCooldown;
        tiltTimer = tiltDuration;
        SFXManager.PlayGlobal(SFXManager.Sfx.GhostAttack);
        targetHealth.TakeDamage(damage);
    }

    // 오뚝이처럼 앞으로 살짝 숙였다 복귀
    private void UpdateTilt()
    {
        if (tiltTimer <= 0f) return;
        tiltTimer -= Time.deltaTime;

        float progress = 1f - Mathf.Clamp01(tiltTimer / tiltDuration);
        float angle = Mathf.Sin(progress * Mathf.PI) * tiltAngle;

        // 현재 y축 방향(진행 방향)은 유지하고 X축(앞으로 숙임)만 오프셋
        Vector3 e = transform.eulerAngles;
        transform.eulerAngles = new Vector3(angle, GetFacingTargetYaw(), 0f);
    }

    private float GetFacingTargetYaw()
    {
        Transform target = GetActiveTargetTransform();
        if (target == null) return transform.eulerAngles.y;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) return transform.eulerAngles.y;

        return Quaternion.LookRotation(toTarget.normalized, Vector3.up).eulerAngles.y;
    }

    private void RotateTowardDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion faceRot = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, faceRot, rotationSpeed * Time.deltaTime);
    }

    private void UpdatePlayerCollisionPassThrough()
    {
        PlayerController pc = player != null ? player.GetComponent<PlayerController>() : null;
        SetPlayerCollisionIgnored(pc != null && pc.IsIncapacitated);
    }

    private void SetPlayerCollisionIgnored(bool ignored)
    {
        if (ignoringPlayerCollision == ignored) return;
        ignoringPlayerCollision = ignored;

        if (ownColliders == null || playerColliders == null) return;
        foreach (Collider own in ownColliders)
        {
            if (own == null) continue;
            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider == null) continue;
                Physics.IgnoreCollision(own, playerCollider, ignored);
            }
        }
    }

    private void ApplyHover()
    {
        Vector3 pos = transform.position;
        pos.y = baseY + Mathf.Sin(Time.time * hoverFrequency + hoverPhase) * hoverAmplitude;
        transform.position = pos;
    }

    // ── 유틸 ──────────────────────────────────────────────────────

    private Transform GetActiveTargetTransform()
    {
        if (blockingBrick != null) return blockingBrick.transform;
        return GetMainTargetTransform();
    }

    private static void SetBoolSafe(Animator a, string name, bool v)
    {
        foreach (AnimatorControllerParameter p in a.parameters)
            if (p.name == name) { a.SetBool(name, v); return; }
    }

    // 외부 데미지 인입(플레이어 공격 등)
    public void TakeDamage(float amount) => damageable.TakeDamage(amount);

    public void ApplyStunAndKnockback(Vector3 sourcePosition, float stunDuration,
        float knockbackDistance, float knockbackDuration)
    {
        Vector3 direction = transform.position - sourcePosition;
        direction.y = 0f;
        ApplyStunAndKnockbackDirection(direction, stunDuration, knockbackDistance, knockbackDuration);
    }

    public void ApplyStunAndKnockbackDirection(Vector3 knockbackDirection, float stunDuration,
        float knockbackDistance, float knockbackDuration)
    {
        if (isDead) return;

        stunTimer = Mathf.Max(stunTimer, Mathf.Max(0f, stunDuration));
        this.knockbackDuration = Mathf.Max(0.01f, knockbackDuration);
        knockbackTimer = this.knockbackDuration;

        knockbackDirection.y = 0f;
        if (knockbackDirection.sqrMagnitude < 0.001f)
            knockbackDirection = -lastMoveDirection;
        if (knockbackDirection.sqrMagnitude < 0.001f)
            knockbackDirection = -transform.forward;

        knockbackVelocity = knockbackDirection.normalized
            * Mathf.Max(0f, knockbackDistance) / this.knockbackDuration;

        attackTimer = Mathf.Max(attackTimer, stunTimer);
        if (animator != null) SetBoolSafe(animator, "IsMoving", false);
    }

    private void UpdateCrowdControl()
    {
        stunTimer = Mathf.Max(0f, stunTimer - Time.deltaTime);

        if (knockbackTimer > 0f)
        {
            knockbackTimer = Mathf.Max(0f, knockbackTimer - Time.deltaTime);
            float normalizedTime = knockbackDuration <= 0f ? 0f : knockbackTimer / knockbackDuration;
            float falloff = Mathf.Clamp01(normalizedTime);
            transform.position += knockbackVelocity * falloff * Time.deltaTime;
        }

        Vector3 flatVelocity = knockbackVelocity;
        flatVelocity.y = 0f;
        if (flatVelocity.sqrMagnitude > 0.001f)
            RotateTowardDirection(-flatVelocity);
    }

    // GhostSpawner에서 스폰 시 스탯 일괄 세팅
    public void ConfigureStats(float moveSpeed, float attackRange, float attackDamage,
                               float attackCooldown, float aggroRange, float deaggroRange,
                               float flightHeight)
    {
        this.moveSpeed = moveSpeed;
        this.attackRange = attackRange;
        this.attackDamage = attackDamage;
        this.attackCooldown = attackCooldown;
        this.aggroRange = aggroRange;
        this.deaggroRange = deaggroRange;
        this.flightHeight = flightHeight;
    }

    private void HandleDeath()
    {
        if (isDead) return;
        isDead = true;
        SFXManager.PlayGlobal(SFXManager.Sfx.GhostDeath);
        if (animator != null) SetBoolSafe(animator, "IsMoving", false);
        OnDeath?.Invoke();

        // 콜라이더 비활성 → 죽으면서 물리 간섭 없음
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        DropCoins();
        StartCoroutine(DeathRoutine());
    }

    private void DropCoins()
    {
        Vector3 dropPos = transform.position;
        dropPos.y = baseY;

        if (baseCoinPrefab != null)
            SpawnCoin(baseCoinPrefab, dropPos, baseCoinValue);

        if (bonusCoinPrefab != null && Random.value < bonusCoinChance)
        {
            Vector3 offset = new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
            SpawnCoin(bonusCoinPrefab, dropPos + offset, bonusCoinValue);
        }
    }

    private static void SpawnCoin(GameObject prefab, Vector3 pos, int value)
    {
        GameObject coin = Instantiate(prefab, pos, Quaternion.identity);
        CoinPickup cp = coin.GetComponent<CoinPickup>();
        if (cp == null) cp = coin.AddComponent<CoinPickup>();
        cp.Value = value;

        // 인프라 자동 확보 (씬에 CoinWallet/CoinUI 가 없어도 됨)
        CoinWallet.EnsureExists();
        CoinUI.EnsureExists();
    }

    // GhostSpawner에서 티어 코인 세팅
    public void SetCoinDrops(GameObject basePrefab, int baseValue,
                             GameObject bonusPrefab, int bonusValue, float bonusChance)
    {
        baseCoinPrefab = basePrefab;
        baseCoinValue = baseValue;
        bonusCoinPrefab = bonusPrefab;
        bonusCoinValue = bonusValue;
        bonusCoinChance = Mathf.Clamp01(bonusChance);
    }

    private IEnumerator DeathRoutine()
    {
        float elapsed = 0f;
        Quaternion startRot = transform.rotation;
        // 뒤로 자빠지기 → 로컬 X 축 음수 회전
        Quaternion endRot = startRot * Quaternion.Euler(-deathTiltAngle, 0f, 0f);

        Vector3 startScale = transform.localScale;
        Vector3 endScale = startScale * 0.05f;

        // 셰이더가 알파를 지원할 수도 있으니 시도. 안 되면 스케일 축소로 대체.
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        Color[] originalColors = new Color[renderers.Length];
        bool[] canFade = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material.HasProperty("_Color"))
            {
                originalColors[i] = renderers[i].material.color;
                canFade[i] = true;
            }
        }

        while (elapsed < deathDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / deathDuration);

            // 자빠지기는 초반 40% 안에 완료 → 완만한 이지아웃
            float rotT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / (deathDuration * 0.4f)));
            transform.rotation = Quaternion.Slerp(startRot, endRot, rotT);

            // 페이드/축소는 전체 시간에 걸쳐
            float fade = 1f - t;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !canFade[i]) continue;
                Color c = originalColors[i];
                c.a = fade;
                renderers[i].material.color = c;
            }
            transform.localScale = Vector3.Lerp(startScale, endScale, t);

            // Hover 정지 → baseY 로 스냅
            Vector3 pos = transform.position;
            pos.y = baseY;
            transform.position = pos;

            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, deaggroRange);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * brickCheckDistance);
    }
}
