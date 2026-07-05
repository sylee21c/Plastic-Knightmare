using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Damageable))]
public abstract class CompanionToy : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] protected CompanionDefinition definition;

    [Header("Death")]
    [SerializeField] private float deathFadeDuration = 1.4f;
    [SerializeField] private float deathFallAngle = 90f;

    protected Damageable damageable;
    protected Vector3 originalPosition;
    protected Quaternion originalRotation;
    protected bool isNight;
    protected BuildingGridOverlay grid;

    private bool phaseInitialized;
    private bool deathStarted;
    private Rigidbody body;
    private Collider mainCollider;

    public CompanionDefinition Definition => definition;

    // 서브클래스가 오버라이드해서 자기 사망 SFX 지정 가능. 기본은 TankDeath.
    protected virtual SFXManager.Sfx DeathSfx => SFXManager.Sfx.TankDeath;

    // 유령이 이 컴패니언을 적으로 인식하고 쫓아올지 여부. 트랩류는 false 로 오버라이드.
    public virtual bool IsTargetableByGhost => true;

    protected virtual void Awake()
    {
        damageable = GetComponent<Damageable>();
        EnsurePhysicalCollision();
    }

    protected virtual void Start()
    {
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        grid = FindAnyObjectByType<BuildingGridOverlay>();

        if (definition != null && damageable != null)
            damageable.SetMaxHealth(definition.maxHealth);

        if (damageable != null)
            damageable.OnDeath += HandleDeath;
    }

    // 다음 위치가 그리드 유효 셀 위에 있는지 검사. 그리드 없으면 항상 true.
    protected bool IsPositionOnFloor(Vector3 worldPosition)
    {
        if (grid == null || grid.CellSize <= 0f) return true;
        return grid.IsCellOnFloor(grid.WorldToCell(worldPosition));
    }

    protected virtual void OnDestroy()
    {
        if (damageable != null)
            damageable.OnDeath -= HandleDeath;
    }

    protected virtual void Update()
    {
        if (damageable != null && damageable.IsDead) return;

        // 게임오버: 모든 행동 정지.
        if (GameOverUIController.IsGameOver) return;

        DayNightManager mgr = DayNightManager.Instance;
        bool nowNight = mgr != null && mgr.CurrentPhase == DayNightManager.Phase.Night;

        if (!phaseInitialized || nowNight != isNight)
        {
            isNight = nowNight;
            phaseInitialized = true;
            OnPhaseChanged(isNight);
        }

        if (isNight) NightBehavior();
        else DayBehavior();
    }

    protected virtual void OnPhaseChanged(bool night)
    {
        if (!night)
        {
            transform.position = originalPosition;
            transform.rotation = originalRotation;
        }
    }

    protected virtual void DayBehavior() { }

    protected abstract void NightBehavior();

    protected Vector3 MoveWithCollision(Vector3 displacement)
    {
        if (displacement.sqrMagnitude <= 0.000001f) return Vector3.zero;
        if (mainCollider == null) return displacement;

        Vector3 direction = displacement.normalized;
        float distance = displacement.magnitude;
        float skin = 0.03f;

        Bounds bounds = mainCollider.bounds;
        float radius = Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, bounds.extents.z));
        Vector3 origin = bounds.center;
        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction,
            distance + skin, ~0, QueryTriggerInteraction.Ignore);
        float allowedDistance = distance;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger) continue;
            if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform)) continue;
            if (Mathf.Abs(hits[i].normal.y) > 0.5f) continue;
            if (hits[i].distance <= skin) continue;

            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hits[i].distance - skin));
        }

        // 그리드 경계 검사: 다음 위치가 유효 셀 밖이면 이동 거부.
        // (플레이 영역 벽이 없거나 얇아도 셀 기준으로 확실히 막음.)
        Vector3 candidate = transform.position + direction * allowedDistance;
        if (!IsPositionOnFloor(candidate))
        {
            // 축별로 슬라이드 재시도 → 벽에 붙어서 옆으로 미끄러지듯 진행 가능.
            Vector3 sX = new Vector3(direction.x, 0f, 0f) * allowedDistance;
            Vector3 sZ = new Vector3(0f, 0f, direction.z) * allowedDistance;
            if (IsPositionOnFloor(transform.position + sX)) return sX;
            if (IsPositionOnFloor(transform.position + sZ)) return sZ;
            return Vector3.zero;
        }

        return direction * allowedDistance;
    }

    public void Configure(CompanionDefinition def)
    {
        definition = def;
        if (damageable == null) damageable = GetComponent<Damageable>();
        if (damageable != null) damageable.SetMaxHealth(def.maxHealth);
    }

    private void HandleDeath()
    {
        if (deathStarted) return;
        deathStarted = true;
        SFXManager.PlayGlobal(DeathSfx);
        StopAllCoroutines();
        StartCoroutine(DeathRoutine());
    }

    private void EnsurePhysicalCollision()
    {
        mainCollider = GetComponentInChildren<Collider>();
        if (mainCollider == null)
            mainCollider = CreateBoundsCollider();

        if (mainCollider != null)
            mainCollider.isTrigger = false;

        body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        if (mainCollider != null && mainCollider.material == null)
        {
            mainCollider.material = new PhysicsMaterial("CompanionNoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }
    }

    private Collider CreateBoundsCollider()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return gameObject.AddComponent<BoxCollider>();

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            if (renderers[i] != null) bounds.Encapsulate(renderers[i].bounds);

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = transform.InverseTransformVector(bounds.size);
        box.center = localCenter;
        box.size = new Vector3(
            Mathf.Max(0.15f, Mathf.Abs(localSize.x)),
            Mathf.Max(0.15f, Mathf.Abs(localSize.y)),
            Mathf.Max(0.15f, Mathf.Abs(localSize.z)));
        return box;
    }

    private IEnumerator DeathRoutine()
    {
        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        Color[] originalColors = new Color[renderers.Length];
        bool[] canFade = new bool[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            Material material = renderer.material;
            if (material != null && material.HasProperty("_Color"))
            {
                PrepareTransparentMaterial(material);
                originalColors[i] = material.color;
                canFade[i] = true;
            }
        }

        float duration = Mathf.Max(0.01f, deathFadeDuration);
        float elapsed = 0f;
        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0f, 0f, deathFallAngle);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - t;
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (!canFade[i] || renderers[i] == null) continue;
                Color color = originalColors[i];
                color.a = alpha;
                renderers[i].material.color = color;
            }

            transform.rotation = Quaternion.Slerp(startRotation, endRotation, easedT);
            yield return null;
        }

        Destroy(gameObject);
    }

    private static void PrepareTransparentMaterial(Material material)
    {
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 2f);

        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    protected GhostAI FindNearestGhostInRange(float range)
    {
#if UNITY_2023_1_OR_NEWER
        GhostAI[] all = Object.FindObjectsByType<GhostAI>(FindObjectsSortMode.None);
#else
        GhostAI[] all = Object.FindObjectsOfType<GhostAI>();
#endif
        GhostAI nearest = null;
        float bestDist = float.MaxValue;
        foreach (GhostAI ghost in all)
        {
            if (ghost == null) continue;
            float dist = Vector3.Distance(transform.position, ghost.transform.position);
            if (dist <= range && dist < bestDist)
            {
                bestDist = dist;
                nearest = ghost;
            }
        }
        return nearest;
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        if (definition == null) return;
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, definition.aggroRange);
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, definition.attackRange);
    }
#endif
}
