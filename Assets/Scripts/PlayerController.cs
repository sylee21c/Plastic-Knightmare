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
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private float attackCooldown = 0.7f;
    [SerializeField] private float guardDamageMultiplier = 0.35f;

    public bool IsBlocking { get; private set; }

    private Rigidbody rb;
    private Animator animator;
    private CapsuleCollider capsule;

    private Vector3 pendingMoveDirection;
    private bool isGrounded;
    private float attackTimer;
    private bool wasNight;

    private bool lastBlocking;

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
        if (animator == null)
        {
            Debug.LogError("[PlayerController] Animator를 찾을 수 없습니다.");
            return;
        }

        bool hasSpeed = false, hasAttack = false, hasBlocking = false;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == "Speed")      hasSpeed = true;
            if (p.name == "Attack")     hasAttack = true;
            if (p.name == "IsBlocking") hasBlocking = true;
        }

        if (!hasAttack || !hasBlocking)
            Debug.LogWarning($"[PlayerController] KnightCont 컨트롤러에 파라미터 누락 — " +
                             $"Attack:{hasAttack} / IsBlocking:{hasBlocking} / Speed:{hasSpeed}\n" +
                             "Project 창에서 KnightCont.controller 우클릭 → Reimport 하세요.");
    }

    private void Update()
    {
        CheckGrounded();

        Vector2 input = ReadMoveInput();
        float speed = input.magnitude;
        pendingMoveDirection = speed > 0.001f ? GetCameraRelativeMove(input) : Vector3.zero;

        if (animator != null)
            animator.SetFloat("Speed", speed);

        if (isGrounded && WasJumpPressed())
            DoJump();

        attackTimer -= Time.deltaTime;

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

        Vector3 hitCenter = transform.position + transform.forward * attackRange * 0.6f + Vector3.up * 0.5f;
        Collider[] hits = Physics.OverlapSphere(hitCenter, attackRange * 0.5f);
        foreach (Collider hit in hits)
        {
            GhostAI ghost = hit.GetComponentInParent<GhostAI>();
            ghost?.TakeDamage(attackDamage);
        }
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

    // ── 점프 / 접지 체크 ──────────────────────────────────────────

    private void DoJump()
    {
        // 수직 속도 초기화 후 순간 힘으로 점프 → 벽 마찰 영향 최소화
        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 공격 히트박스
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(
            transform.position + transform.forward * attackRange * 0.6f + Vector3.up * 0.5f,
            attackRange * 0.5f);

        // 접지 체크
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.4f);
        Vector3 origin = GetGroundCastOrigin();
        Gizmos.DrawWireSphere(origin + Vector3.down * groundCheckDistance, groundCheckRadius);
    }
#endif
}
