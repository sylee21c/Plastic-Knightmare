using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float rotationSpeed = 14f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float groundCheckRadius = 0.1f;
    [SerializeField] private float groundCheckDistance = 0.12f;

    [Header("Combat (Night)")]
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private float attackCooldown = 0.7f;
    [SerializeField] private float guardDamageMultiplier = 0.35f;

    [Header("Attack Hitbox")]
    [Tooltip("공격 애니메이션 시작 후 히트 활성화 시점 (초). 60fps 기준 프레임 10 = 0.167")]
    [SerializeField] private float hitWindowStart = 0.167f;
    [Tooltip("히트 비활성화 시점 (초). 60fps 기준 프레임 28 = 0.467")]
    [SerializeField] private float hitWindowEnd = 0.467f;
    [Tooltip("히트박스 로컬 오프셋 (X:오른쪽, Y:위, Z:앞)")]
    [SerializeField] private Vector3 hitboxOffset = new Vector3(0f, 0.55f, 0.85f);
    [Tooltip("히트박스 구 반경")]
    [SerializeField] private float hitboxRadius = 0.7f;
    [SerializeField] private LayerMask hitboxLayers = ~0;
    [Tooltip("공격 사운드 재생 시점 (초). 60fps 기준 프레임 11 = 0.183")]
    [SerializeField] private float attackSoundDelay = 0.183f;

    [Header("Incapacitation (HP 소진 시)")]
    [SerializeField] private float incapacitationDuration = 5f;
    [SerializeField, Tooltip("초당 깜빡임 횟수 (on/off 왕복 횟수). 6이면 초당 6번 사라졌다 나타남")]
    private float blinkFrequency = 6f;

    public bool IsBlocking { get; private set; }
    public bool IsIncapacitated { get; private set; }

    private Rigidbody rb;
    private Animator animator;
    private CapsuleCollider capsule;
    private Damageable damageable;

    private Vector3 pendingMoveDirection;
    private bool isGrounded;
    private float attackTimer;
    private bool wasNight;

    private bool lastBlocking;

    private float incapacitatedTimer;
    private Renderer[] cachedRenderers;

    // 공격 스윙 상태 — 스윙 시작 후 경과 시간, 이번 스윙에서 이미 맞춘 유령 (중복 방지)
    private bool isSwinging;
    private float swingElapsed;
    private bool attackSoundPlayedThisSwing;
    private readonly System.Collections.Generic.HashSet<GhostAI> hitThisSwing =
        new System.Collections.Generic.HashSet<GhostAI>();

    // 걷기 루프 사운드 (로컬 AudioSource)
    private AudioSource walkAudioSource;
    private float lastHealthForSfx;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        animator = GetComponentInChildren<Animator>();
        capsule = GetComponent<CapsuleCollider>();

        // 마찰력 0 → 벽에 달라붙는 현상 제거
        if (capsule != null)
        {
            PhysicsMaterial noFriction = new PhysicsMaterial("PlayerNoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            capsule.material = noFriction;
        }
    }

    private void Start()
    {
        // Damageable 자동 확보 → HP 소진 시 무력화 처리
        damageable = GetComponent<Damageable>();
        if (damageable == null) damageable = gameObject.AddComponent<Damageable>();
        damageable.OnDeath += HandleDeath;
        damageable.OnHealthChanged += HandleHealthChanged;
        lastHealthForSfx = damageable.CurrentHealth;

        if (GetComponent<HealthBar>() == null)
            gameObject.AddComponent<HealthBar>();

        CacheRenderers();
        EnsureWalkAudioSource();

        if (animator == null) return;

        bool hasAttack = false, hasBlocking = false;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == "Attack")     hasAttack = true;
            if (p.name == "IsBlocking") hasBlocking = true;
        }
        if (!hasAttack || !hasBlocking)
            Debug.LogWarning("[PlayerController] KnightCont에 Attack/IsBlocking 파라미터 누락. " +
                             "Tools > Setup Knight Controller 실행하세요.");
    }

    private void Update()
    {
        // 게임오버: 모든 입력/움직임 정지, 걷기 사운드 정지, 애니메이터 속도 0
        if (GameOverUIController.IsGameOver)
        {
            pendingMoveDirection = Vector3.zero;
            if (animator != null) animator.SetFloat("Speed", 0f);
            StopWalkAudio();
            return;
        }

        // 무력화 상태 진행 처리
        if (IsIncapacitated)
        {
            incapacitatedTimer -= Time.deltaTime;
            pendingMoveDirection = Vector3.zero;
            if (animator != null) animator.SetFloat("Speed", 0f);

            // 깜빡임: 렌더러 enabled 를 매 프레임 토글 → 셰이더 무관하게 동작
            bool visible = ((int)(Time.time * blinkFrequency * 2f)) % 2 == 0;
            SetRenderersVisible(visible);

            if (incapacitatedTimer <= 0f) RecoverFromIncapacitation();
            return;
        }

        CheckGrounded();

        Vector2 input = ReadMoveInput();
        float speed = input.magnitude;
        pendingMoveDirection = speed > 0.001f ? GetCameraRelativeMove(input) : Vector3.zero;

        if (animator != null)
            animator.SetFloat("Speed", speed);

        UpdateWalkAudio(speed, isGrounded);

        if (isGrounded && WasJumpPressed())
            DoJump();

        attackTimer -= Time.deltaTime;
        ProcessAttackSwing();

        // 이벤트 대신 매 프레임 직접 체크 → 이벤트 구독 실패해도 항상 정확
        bool isNight = DayNightManager.Instance != null
                       && DayNightManager.Instance.CurrentPhase == DayNightManager.Phase.Night;

        if (isNight)
        {
            HandleCombat();
        }
        else if (wasNight)
        {
            // 낮으로 전환된 첫 프레임에만 블록 해제
            ApplyBlocking(false);
        }
        wasNight = isNight;
    }

    private void FixedUpdate()
    {
        if (GameOverUIController.IsGameOver)
        {
            // 수평 속도만 정지. 중력(y) 은 유지 (공중이었으면 자연 낙하).
            Vector3 v = rb.linearVelocity;
            v.x = 0f; v.z = 0f;
            rb.linearVelocity = v;
            return;
        }

        Vector3 vel = pendingMoveDirection * moveSpeed;
        vel.y = rb.linearVelocity.y;
        rb.linearVelocity = vel;

        if (pendingMoveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(pendingMoveDirection, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, rotationSpeed * Time.fixedDeltaTime));
        }
    }

    // ── 전투 ──────────────────────────────────────────────────────

    private void HandleCombat()
    {
        ApplyBlocking(IsRightClickHeld());

        if (!IsBlocking && WasLeftClickPressed() && attackTimer <= 0f)
            DoAttack();
    }

    private void DoAttack()
    {
        attackTimer = attackCooldown;

        if (animator != null)
            animator.SetTrigger("Attack");

        // 데미지는 즉시 X, Update 안에서 hitWindowStart~hitWindowEnd 사이에만 판정
        isSwinging = true;
        swingElapsed = 0f;
        attackSoundPlayedThisSwing = false;
        hitThisSwing.Clear();
    }

    // Update에서 매 프레임 호출. 히트 윈도우 진입 시 히트박스로 유령 판정.
    private void ProcessAttackSwing()
    {
        if (!isSwinging) return;

        swingElapsed += Time.deltaTime;

        // 공격 사운드: 11 프레임째 (기본 0.183s) 에 1회 재생
        if (!attackSoundPlayedThisSwing && swingElapsed >= attackSoundDelay)
        {
            attackSoundPlayedThisSwing = true;
            SFXManager.PlayGlobal(SFXManager.Sfx.PlayerAttack);
        }

        // 윈도우 종료 → 스윙 마감
        if (swingElapsed > hitWindowEnd)
        {
            isSwinging = false;
            return;
        }

        // 윈도우 시작 전이면 대기
        if (swingElapsed < hitWindowStart) return;

        Vector3 center = GetHitboxCenter();
        Collider[] hits = Physics.OverlapSphere(center, hitboxRadius, hitboxLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider col in hits)
        {
            GhostAI ghost = col.GetComponentInParent<GhostAI>();
            if (ghost == null || hitThisSwing.Contains(ghost)) continue;
            ghost.TakeDamage(attackDamage);
            hitThisSwing.Add(ghost);
        }
    }

    private Vector3 GetHitboxCenter()
    {
        return transform.position
             + transform.right   * hitboxOffset.x
             + transform.up      * hitboxOffset.y
             + transform.forward * hitboxOffset.z;
    }

    private void ApplyBlocking(bool blocking)
    {
        IsBlocking = blocking;
        // 변경 시에만 Set → 매 프레임 스팸 방지
        if (animator != null && blocking != lastBlocking)
        {
            lastBlocking = blocking;
            animator.SetBool("IsBlocking", blocking);
        }
    }

    public float GetGuardMultiplier() => IsBlocking ? guardDamageMultiplier : 1f;

    public float AttackDamage => attackDamage;
    public void SetAttackDamage(float value) => attackDamage = Mathf.Max(0f, value);

    // ── 점프 / 접지 체크 ──────────────────────────────────────────

    private void DoJump()
    {
        // 수직 속도 초기화 후 순간 힘으로 점프 → 벽 마찰 영향 최소화
        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        SFXManager.PlayGlobal(SFXManager.Sfx.PlayerJump);
    }

    private void CheckGrounded()
    {
        // SphereCast를 아래로 쏘아 수평 벽을 바닥으로 잘못 인식하는 문제 방지
        Vector3 origin = GetGroundCastOrigin();
        if (Physics.SphereCast(origin, groundCheckRadius, Vector3.down,
            out RaycastHit hit, groundCheckDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            // 법선이 위를 향하는 면만 바닥으로 인정 (수직 벽 제외)
            isGrounded = hit.normal.y > 0.5f;
        }
        else
        {
            isGrounded = false;
        }
    }

    private Vector3 GetGroundCastOrigin()
    {
        if (capsule != null)
        {
            // CapsuleCollider 하단 중심보다 살짝 위에서 캐스트 시작
            float bottomLocalY = capsule.center.y - capsule.height * 0.5f + capsule.radius + 0.05f;
            return transform.TransformPoint(0f, bottomLocalY, 0f);
        }
        return transform.position + Vector3.up * 0.1f;
    }

    // ── 카메라 기준 이동 ──────────────────────────────────────────

    private static Vector3 GetCameraRelativeMove(Vector2 input)
    {
        Camera cam = Camera.main;
        if (cam == null) return new Vector3(input.x, 0f, input.y).normalized;

        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 right   = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;

        if (forward.sqrMagnitude <= 0.001f) forward = Vector3.forward;
        if (right.sqrMagnitude   <= 0.001f) right   = Vector3.right;

        return (forward * input.y + right * input.x).normalized;
    }

    // ── 입력 ──────────────────────────────────────────────────────

    private static Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            Vector2 v = Vector2.zero;
            if (kb.aKey.isPressed) v.x -= 1f;
            if (kb.dKey.isPressed) v.x += 1f;
            if (kb.sKey.isPressed) v.y -= 1f;
            if (kb.wKey.isPressed) v.y += 1f;
            return Vector2.ClampMagnitude(v, 1f);
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Vector2.ClampMagnitude(
            new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#else
        return Vector2.zero;
#endif
    }

    private static bool WasJumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null) return kb.spaceKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space);
#else
        return false;
#endif
    }

    private static bool WasLeftClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current;
        if (m != null) return m.leftButton.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private static bool IsRightClickHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current;
        if (m != null) return m.rightButton.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(1);
#else
        return false;
#endif
    }

    // ── 무력화 ────────────────────────────────────────────────────

    private void HandleDeath()
    {
        if (IsIncapacitated) return;
        SFXManager.PlayGlobal(SFXManager.Sfx.PlayerDeath);
        StopWalkAudio();
        EnterIncapacitation();
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (current < lastHealthForSfx - 0.01f)
        {
            // HP 가 실제로 감소한 경우 (힐/부활은 제외).
            // 죽음 사운드는 HandleDeath 에서 별도 재생 → 여기서 중복 안 냄.
            if (current > 0f)
                SFXManager.PlayGlobal(IsBlocking ? SFXManager.Sfx.PlayerGuardHit : SFXManager.Sfx.PlayerHit);
        }
        lastHealthForSfx = current;
    }

    private void EnterIncapacitation()
    {
        IsIncapacitated = true;
        incapacitatedTimer = incapacitationDuration;
        ApplyBlocking(false);
        // 알파는 Update 안에서 매 프레임 깜빡임으로 처리

        // 물리 정지 (중력은 유지)
        Vector3 v = rb.linearVelocity;
        v.x = 0f; v.z = 0f;
        rb.linearVelocity = v;
    }

    private void RecoverFromIncapacitation()
    {
        IsIncapacitated = false;
        SetRenderersVisible(true);
        if (damageable != null) damageable.Revive(damageable.MaxHealth);
    }

    private void SetRenderersVisible(bool visible)
    {
        if (cachedRenderers == null) return;
        foreach (Renderer r in cachedRenderers)
            if (r != null) r.enabled = visible;
    }

    private void CacheRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>();
    }

    // ── 걷기 루프 사운드 ──────────────────────────────────────────

    private void EnsureWalkAudioSource()
    {
        SFXManager.EnsureExists();
        if (walkAudioSource != null) return;

        walkAudioSource = gameObject.AddComponent<AudioSource>();
        walkAudioSource.playOnAwake = false;
        walkAudioSource.loop = true;
        walkAudioSource.spatialBlend = 0f; // 2D (플레이어 시점이라 3D 감쇠 불필요)
    }

    private void UpdateWalkAudio(float inputMagnitude, bool grounded)
    {
        if (walkAudioSource == null) return;

        bool shouldWalk = inputMagnitude > 0.05f && grounded && !IsIncapacitated;

        if (shouldWalk)
        {
            // 클립/볼륨이 SFXManager 에서 변경됐을 수 있으니 매번 확인
            if (SFXManager.Instance != null
                && SFXManager.Instance.TryGetClipData(SFXManager.Sfx.PlayerWalk,
                    out AudioClip clip, out float volume, out _))
            {
                if (walkAudioSource.clip != clip) walkAudioSource.clip = clip;
                walkAudioSource.volume = volume;
                if (!walkAudioSource.isPlaying) walkAudioSource.Play();
            }
        }
        else if (walkAudioSource.isPlaying)
        {
            walkAudioSource.Stop();
        }
    }

    private void StopWalkAudio()
    {
        if (walkAudioSource != null && walkAudioSource.isPlaying)
            walkAudioSource.Stop();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 공격 히트박스 — 스윙 중 윈도우 액티브면 빨강, 아니면 옅은 빨강
        bool activeNow = isSwinging && swingElapsed >= hitWindowStart && swingElapsed <= hitWindowEnd;
        Gizmos.color = activeNow ? new Color(1f, 0.15f, 0.15f, 0.9f)
                                 : new Color(1f, 0.4f, 0.4f, 0.35f);
        Vector3 center = transform.position
                       + transform.right   * hitboxOffset.x
                       + transform.up      * hitboxOffset.y
                       + transform.forward * hitboxOffset.z;
        Gizmos.DrawWireSphere(center, hitboxRadius);

        // 접지 체크
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.4f);
        Vector3 origin = GetGroundCastOrigin();
        Gizmos.DrawWireSphere(origin + Vector3.down * groundCheckDistance, groundCheckRadius);
    }
#endif
}
