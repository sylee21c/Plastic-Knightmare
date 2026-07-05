using UnityEngine;

public sealed class MobileCompanionAI : CompanionToy
{
    [Header("Projectile")]
    [SerializeField] private Transform projectileSpawnPoint;

    private GhostAI currentTarget;
    private float attackTimer;
    private float patrolWaitTimer;
    private Vector3 patrolAnchor;
    private Vector3 patrolTarget;
    private bool hasPatrolTarget;

    private AudioSource moveAudioSource;

    protected override void Start()
    {
        base.Start();
        patrolAnchor = transform.position;
        EnsureMoveAudioSource();
    }

    protected override void Update()
    {
        Vector3 posBefore = transform.position;
        base.Update();
        UpdateMoveAudio(posBefore);
    }

    private void EnsureMoveAudioSource()
    {
        SFXManager.EnsureExists();
        if (moveAudioSource != null) return;

        moveAudioSource = gameObject.AddComponent<AudioSource>();
        moveAudioSource.playOnAwake = false;
        moveAudioSource.loop = true;
        moveAudioSource.spatialBlend = 1f;   // 3D — 위치 기반 감쇠
        moveAudioSource.rolloffMode = AudioRolloffMode.Linear;
        moveAudioSource.minDistance = 2f;
        moveAudioSource.maxDistance = 18f;
    }

    private void UpdateMoveAudio(Vector3 posBefore)
    {
        if (moveAudioSource == null) return;

        bool alive = damageable != null && !damageable.IsDead;
        float moved = (transform.position - posBefore).sqrMagnitude;
        bool moving = alive && isNight && moved > 0.000004f; // ≈ 0.002 유닛/프레임 이상

        if (moving)
        {
            if (SFXManager.Instance != null
                && SFXManager.Instance.TryGetClipData(SFXManager.Sfx.TankMove,
                    out AudioClip clip, out float volume, out _))
            {
                if (moveAudioSource.clip != clip) moveAudioSource.clip = clip;
                moveAudioSource.volume = volume;
                if (!moveAudioSource.isPlaying) moveAudioSource.Play();
            }
        }
        else if (moveAudioSource.isPlaying)
        {
            moveAudioSource.Stop();
        }
    }

    protected override void OnPhaseChanged(bool night)
    {
        base.OnPhaseChanged(night);

        currentTarget = null;
        hasPatrolTarget = false;
        patrolWaitTimer = 0f;
        patrolAnchor = transform.position;
    }

    protected override void NightBehavior()
    {
        if (definition == null) return;

        UpdateTarget();
        attackTimer -= Time.deltaTime;

        if (currentTarget == null)
        {
            Patrol();
            return;
        }

        MoveToward(currentTarget.transform, definition.moveSpeed, definition.attackRange);
        RotateToward(currentTarget.transform);
        TryFire(currentTarget);
    }

    private void UpdateTarget()
    {
        if (currentTarget == null || currentTarget.gameObject == null
            || Vector3.Distance(transform.position, currentTarget.transform.position) > definition.aggroRange * 1.4f)
        {
            currentTarget = FindNearestGhostInRange(definition.aggroRange);
        }
    }

    private void Patrol()
    {
        patrolWaitTimer -= Time.deltaTime;
        if (!hasPatrolTarget && patrolWaitTimer <= 0f)
        {
            patrolTarget = PickPatrolPoint();
            hasPatrolTarget = true;
        }

        if (!hasPatrolTarget) return;

        MoveToward(patrolTarget, definition.patrolMoveSpeed,
            Mathf.Max(0.01f, definition.patrolPointReachDistance));

        Vector3 flatDelta = patrolTarget - transform.position;
        flatDelta.y = 0f;
        if (flatDelta.magnitude <= Mathf.Max(0.01f, definition.patrolPointReachDistance))
        {
            hasPatrolTarget = false;
            patrolWaitTimer = Mathf.Max(0f, definition.patrolWaitTime);
        }
    }

    private Vector3 PickPatrolPoint()
    {
        float radius = Mathf.Max(0f, definition.patrolRadius);
        if (radius <= 0.01f) return patrolAnchor;

        // 최대 8회 재시도해서 그리드 유효 셀 위의 랜덤 점을 뽑음.
        // 못 뽑으면 anchor 로 폴백.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = new Vector3(
                patrolAnchor.x + offset.x,
                transform.position.y,
                patrolAnchor.z + offset.y);
            if (IsPositionOnFloor(candidate)) return candidate;
        }
        return patrolAnchor;
    }

    private void MoveToward(Transform target, float speed, float stopDistance)
    {
        MoveToward(target.position, speed, stopDistance);
    }

    private void MoveToward(Vector3 worldPosition, float speed, float stopDistance)
    {
        Vector3 delta = worldPosition - transform.position;
        delta.y = 0f;
        float dist = delta.magnitude;
        if (dist <= stopDistance) return;

        Vector3 dir = delta / dist;
        Vector3 displacement = dir * Mathf.Max(0f, speed) * Time.deltaTime;
        transform.position += MoveWithCollision(displacement);

        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
            definition.rotationSpeed * Time.deltaTime);
    }

    private void RotateToward(Transform target)
    {
        Vector3 delta = target.position - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(delta.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
            definition.rotationSpeed * Time.deltaTime);
    }

    private void TryFire(GhostAI target)
    {
        if (attackTimer > 0f) return;

        Vector3 delta = target.transform.position - transform.position;
        delta.y = 0f;
        if (delta.magnitude > definition.attackRange) return;
        if (!IsFacingTarget(delta.normalized)) return;

        attackTimer = definition.attackCooldown;
        SFXManager.PlayGlobal(SFXManager.Sfx.TankFire);

        if (definition.projectilePrefab == null)
        {
            target.TakeDamage(definition.attackDamage);
            return;
        }

        Transform spawnPoint = projectileSpawnPoint;
        Vector3 spawnPosition = spawnPoint != null
            ? spawnPoint.position
            : transform.TransformPoint(definition.muzzleLocalOffset);
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        GameObject go = Instantiate(definition.projectilePrefab, spawnPosition, spawnRotation);
        go.transform.localScale = Vector3.one * Mathf.Max(0.001f, definition.projectileScale);

        CompanionProjectile projectile = go.GetComponent<CompanionProjectile>();
        if (projectile == null) projectile = go.AddComponent<CompanionProjectile>();
        projectile.Init(target.transform, definition.attackDamage, definition.projectileSpeed);
    }

    private bool IsFacingTarget(Vector3 targetDirection)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) return true;

        float angle = Vector3.Angle(forward.normalized, targetDirection);
        return angle <= Mathf.Max(0f, definition.fireMaxAngle);
    }
}
