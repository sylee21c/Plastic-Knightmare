using UnityEngine;

public enum CompanionKind { Mobile, Stationary, Trap }

// 동료 장난감 한 종류를 정의. ScriptableObject 로 만들어 자산화.
// Project 창 우클릭 → Create → Toys → Companion Definition
[CreateAssetMenu(fileName = "CompanionDef", menuName = "Toys/Companion Definition", order = 0)]
public sealed class CompanionDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("인벤토리 키. 유니크해야 함. 예: \"Tank\"")]
    public string companionId = "Tank";
    public string displayName = "Tank";
    public Sprite icon;
    [Tooltip("고정 슬롯 (0-based hotbar index). -1 이면 자동 할당")]
    public int preferredSlot = -1;

    [Header("Prefab")]
    [Tooltip("필드에 배치될 실제 오브젝트 프리팹. CompanionToy 서브클래스가 붙어있어야 함")]
    public GameObject prefab;

    [Header("Placement")]
    [Tooltip("배치 시 셀 표면에서 위로 얼마나 띄울지")]
    public float placedYOffset = 0f;
    [Tooltip("배치 시 강제 적용할 uniform scale (0 이하면 미적용)")]
    public float spawnScale = 0f;

    [Header("Kind")]
    public CompanionKind kind = CompanionKind.Mobile;

    [Header("Combat Stats")]
    public float maxHealth = 100f;
    public float aggroRange = 5f;
    public float attackRange = 1.5f;
    public float attackDamage = 15f;
    public float attackCooldown = 1f;
    [Tooltip("Mobile companions only fire when facing the target within this angle.")]
    public float fireMaxAngle = 15f;
    [Tooltip("공격 대상 감지에 사용할 LayerMask")]
    public LayerMask targetLayers = ~0;

    [Header("Mobile Only")]
    public float moveSpeed = 2f;
    [Tooltip("No target patrol speed while scouting.")]
    public float patrolMoveSpeed = 0.6f;
    [Tooltip("How far a mobile companion can patrol from its night start point.")]
    public float patrolRadius = 2f;
    [Tooltip("How close the companion must get before picking a new patrol point.")]
    public float patrolPointReachDistance = 0.2f;
    [Tooltip("Delay before picking the next patrol point.")]
    public float patrolWaitTime = 0.4f;
    public float rotationSpeed = 8f;

    [Header("Projectile")]
    [Tooltip("발사체 프리팹. CompanionProjectile 컴포넌트가 있어야 함")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 8f;
    [Tooltip("Uniform scale applied to spawned projectiles.")]
    public float projectileScale = 0.07f;
    [Tooltip("발사체 스폰 위치 오프셋 (로컬)")]
    public Vector3 muzzleLocalOffset = new Vector3(0f, 0.5f, 0.3f);
    [Tooltip("Visual rotation offset applied after aiming the projectile at its target.")]
    public Vector3 projectileVisualRotationOffset = new Vector3(100f, 0f, 0f);
    [Tooltip("Trail color used when the projectile creates its default trail.")]
    public Color projectileTrailColor = new Color(1f, 0.86f, 0.24f, 0.9f);

    [Header("Trap Only")]
    public bool trapTriggerOnlyAtNight = true;
    public bool trapTriggerOnlyWhenGhostEntersPlacedCell = true;
    public float trapFallbackTriggerRadius = 0.55f;
    public float trapTriggerCooldown = 0.4f;
    public float trapSelfDamageOnTrigger = 25f;
    public float trapEffectRadius = 3f;
    public float trapStunDuration = 1.6f;
    public float trapKnockbackDistance = 1.2f;
    public float trapKnockbackDuration = 0.22f;
    public bool trapKnockbackAgainstGhostMovement = true;
    public float trapSquashDuration = 0.18f;
    public float trapSquashReturnDuration = 0.16f;
    public Vector3 trapTriggeredScaleMultiplier = new Vector3(1.25f, 0.35f, 1.25f);
}
