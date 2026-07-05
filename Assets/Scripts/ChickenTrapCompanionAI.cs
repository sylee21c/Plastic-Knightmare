using System.Collections;
using UnityEngine;

public sealed class ChickenTrapCompanionAI : CompanionToy
{
    // 유령은 트랩을 적으로 인식하지 않음 — 지나가다 밟혀야 발동.
    public override bool IsTargetableByGhost => false;

    // 트랩 전용 사망 SFX.
    protected override SFXManager.Sfx DeathSfx => SFXManager.Sfx.ChickenDeath;

    [Header("Trap")]
    [SerializeField] private bool triggerOnlyAtNight = true;
    [SerializeField] private bool triggerOnlyWhenGhostEntersPlacedCell = true;
    [SerializeField] private float fallbackTriggerRadius = 0.55f;
    [SerializeField] private float triggerCooldown = 0.4f;
    [SerializeField] private float selfDamageOnTrigger = 25f;
    [SerializeField] private float effectRadius = 3f;
    [SerializeField] private float stunDuration = 1.6f;
    [SerializeField] private float knockbackDistance = 1.2f;
    [SerializeField] private float knockbackDuration = 0.22f;
    [SerializeField] private bool knockbackAgainstGhostMovement = true;

    [Header("Visual")]
    [SerializeField] private float squashDuration = 0.18f;
    [SerializeField] private float squashReturnDuration = 0.16f;
    [SerializeField] private Vector3 triggeredScaleMultiplier = new Vector3(1.25f, 0.35f, 1.25f);

    private Vector3 placedScale;
    private Vector2Int placedCell;
    private bool hasPlacedCell;
    private float triggerTimer;
    private bool squashing;

    protected override void Start()
    {
        base.Start();
        ApplyDefinitionSettings();
        placedScale = transform.localScale;
        CapturePlacedCell();
    }

    private void ApplyDefinitionSettings()
    {
        if (definition == null) return;

        triggerOnlyAtNight = definition.trapTriggerOnlyAtNight;
        triggerOnlyWhenGhostEntersPlacedCell = definition.trapTriggerOnlyWhenGhostEntersPlacedCell;
        fallbackTriggerRadius = definition.trapFallbackTriggerRadius;
        triggerCooldown = definition.trapTriggerCooldown;
        selfDamageOnTrigger = definition.trapSelfDamageOnTrigger;
        effectRadius = definition.trapEffectRadius;
        stunDuration = definition.trapStunDuration;
        knockbackDistance = definition.trapKnockbackDistance;
        knockbackDuration = definition.trapKnockbackDuration;
        knockbackAgainstGhostMovement = definition.trapKnockbackAgainstGhostMovement;
        squashDuration = definition.trapSquashDuration;
        squashReturnDuration = definition.trapSquashReturnDuration;
        triggeredScaleMultiplier = definition.trapTriggeredScaleMultiplier;
    }

    protected override void NightBehavior()
    {
        if (triggerTimer > 0f)
            triggerTimer -= Time.deltaTime;

        if (triggerTimer > 0f) return;

        GhostAI triggerGhost = FindTriggeringGhost();
        if (triggerGhost != null)
            TriggerTrap();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!CanTrigger()) return;

        GhostAI ghost = other.GetComponentInParent<GhostAI>();
        if (ghost != null && IsGhostOnTrapCell(ghost))
            TriggerTrap();
    }

    private void TriggerTrap()
    {
        if (!CanTrigger()) return;
        triggerTimer = Mathf.Max(0f, triggerCooldown);

        SFXManager.PlayGlobal(SFXManager.Sfx.ChickenTrap);
        ApplyEffectToGhosts();
        if (!squashing)
            StartCoroutine(SquashRoutine());

        if (damageable != null && selfDamageOnTrigger > 0f)
            damageable.TakeDamage(selfDamageOnTrigger);
    }

    private void ApplyEffectToGhosts()
    {
#if UNITY_2023_1_OR_NEWER
        GhostAI[] ghosts = Object.FindObjectsByType<GhostAI>(FindObjectsSortMode.None);
#else
        GhostAI[] ghosts = Object.FindObjectsOfType<GhostAI>();
#endif
        foreach (GhostAI ghost in ghosts)
        {
            if (ghost == null) continue;

            Vector3 delta = ghost.transform.position - transform.position;
            delta.y = 0f;
            if (delta.magnitude > effectRadius) continue;

            Vector3 knockbackDirection = knockbackAgainstGhostMovement
                ? -ghost.LastMoveDirection
                : delta;
            ghost.ApplyStunAndKnockbackDirection(knockbackDirection, stunDuration,
                knockbackDistance, knockbackDuration);
        }
    }

    private GhostAI FindTriggeringGhost()
    {
#if UNITY_2023_1_OR_NEWER
        GhostAI[] ghosts = Object.FindObjectsByType<GhostAI>(FindObjectsSortMode.None);
#else
        GhostAI[] ghosts = Object.FindObjectsOfType<GhostAI>();
#endif
        foreach (GhostAI ghost in ghosts)
        {
            if (ghost != null && IsGhostOnTrapCell(ghost))
                return ghost;
        }

        return null;
    }

    private bool IsGhostOnTrapCell(GhostAI ghost)
    {
        if (ghost == null) return false;

        if (triggerOnlyWhenGhostEntersPlacedCell && grid != null && grid.CellSize > 0f)
        {
            if (!hasPlacedCell)
                CapturePlacedCell();

            return hasPlacedCell && grid.WorldToCell(ghost.transform.position) == placedCell;
        }

        Vector3 delta = ghost.transform.position - transform.position;
        delta.y = 0f;
        return delta.magnitude <= fallbackTriggerRadius;
    }

    private bool CanTrigger()
    {
        if (triggerTimer > 0f) return false;
        if (triggerOnlyAtNight && !isNight) return false;
        if (damageable != null && damageable.IsDead) return false;
        return true;
    }

    private void CapturePlacedCell()
    {
        if (grid == null)
            grid = FindAnyObjectByType<BuildingGridOverlay>();

        hasPlacedCell = grid != null && grid.CellSize > 0f;
        if (hasPlacedCell)
            placedCell = grid.WorldToCell(transform.position);
    }

    private IEnumerator SquashRoutine()
    {
        squashing = true;
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = new Vector3(
            placedScale.x * triggeredScaleMultiplier.x,
            placedScale.y * triggeredScaleMultiplier.y,
            placedScale.z * triggeredScaleMultiplier.z);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, squashDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        elapsed = 0f;
        duration = Mathf.Max(0.01f, squashReturnDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(targetScale, placedScale, t);
            yield return null;
        }

        transform.localScale = placedScale;
        squashing = false;
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = new Color(1f, 0.95f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, fallbackTriggerRadius);
        Gizmos.color = new Color(1f, 0.35f, 0.05f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, effectRadius);
    }
#endif
}
