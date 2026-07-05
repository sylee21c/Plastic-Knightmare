using UnityEngine;

// 고정식 동료 (터렛): 배치된 자리에서 회전만 하며 사정거리 안 유령에게 투사체 발사.
public sealed class StationaryCompanionAI : CompanionToy
{
    private GhostAI currentTarget;
    private float attackTimer;

    protected override void NightBehavior()
    {
        if (definition == null) return;

        if (currentTarget == null || currentTarget.gameObject == null
            || Vector3.Distance(transform.position, currentTarget.transform.position) > definition.attackRange * 1.2f)
        {
            currentTarget = FindNearestGhostInRange(definition.attackRange);
        }

        attackTimer -= Time.deltaTime;

        if (currentTarget == null) return;

        RotateTowardTarget(currentTarget.transform);
        TryFire(currentTarget);
    }

    private void RotateTowardTarget(Transform target)
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
        if (definition.projectilePrefab == null)
        {
            // 프리팹 없으면 즉시 피격으로 대체
            attackTimer = definition.attackCooldown;
            SFXManager.PlayGlobal(SFXManager.Sfx.TankFire);
            target.TakeDamage(definition.attackDamage);
            return;
        }

        attackTimer = definition.attackCooldown;
        SFXManager.PlayGlobal(SFXManager.Sfx.TankFire);
        Vector3 muzzlePos = transform.TransformPoint(definition.muzzleLocalOffset);
        GameObject go = Instantiate(definition.projectilePrefab, muzzlePos, transform.rotation);
        go.transform.localScale = Vector3.one * Mathf.Max(0.001f, definition.projectileScale);
        CompanionProjectile proj = go.GetComponent<CompanionProjectile>();
        if (proj == null) proj = go.AddComponent<CompanionProjectile>();
        proj.Init(target.transform, definition.attackDamage, definition.projectileSpeed);
    }
}
