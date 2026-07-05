using UnityEngine;

public sealed class CompanionProjectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float hitDistance = 0.35f;
    [SerializeField] private float sweepRadius = 0.12f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private float minHitDelay = 0.08f;
    [SerializeField] private bool addTrailWhenMissing = true;
    [SerializeField] private float trailTime = 0.16f;
    [SerializeField] private float trailStartWidth = 0.035f;
    [SerializeField] private float trailEndWidth = 0.005f;
    [SerializeField] private Vector3 visualRotationOffset = new Vector3(100f, 0f, 0f);

    private Transform target;
    private float damage;
    private float speed;
    private float age;
    private bool initialized;

    private void Awake()
    {
        EnsureTrail();
    }

    public void Init(Transform target, float damage, float speed)
    {
        this.target = target;
        this.damage = damage;
        this.speed = speed;
        initialized = true;

        if (target != null)
        {
            Vector3 direction = target.position - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
                SetVisualRotation(direction.normalized);
        }
    }

    private void Update()
    {
        lifetime -= Time.deltaTime;
        age += Time.deltaTime;
        if (lifetime <= 0f) { Destroy(gameObject); return; }
        if (!initialized) return;

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 start = transform.position;
        Vector3 delta = target.position - start;
        float distanceToTarget = delta.magnitude;

        bool canHit = age >= minHitDelay;
        if (canHit && distanceToTarget <= hitDistance)
        {
            Hit(target.GetComponentInParent<GhostAI>());
            return;
        }

        Vector3 direction = delta / distanceToTarget;
        float moveDistance = Mathf.Max(0f, speed) * Time.deltaTime;
        Vector3 end = start + direction * moveDistance;

        if (canHit && TryHitGhostBetween(start, direction, moveDistance))
            return;

        transform.position = end;
        SetVisualRotation(direction);
    }

    private void SetVisualRotation(Vector3 direction)
    {
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up)
            * Quaternion.Euler(visualRotationOffset);
    }

    private void EnsureTrail()
    {
        if (!addTrailWhenMissing || GetComponent<TrailRenderer>() != null) return;

        TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = Mathf.Max(0.01f, trailTime);
        trail.startWidth = Mathf.Max(0.001f, trailStartWidth);
        trail.endWidth = Mathf.Max(0f, trailEndWidth);
        trail.numCapVertices = 2;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            Material material = new Material(shader);
            material.color = new Color(1f, 0.86f, 0.24f, 0.9f);
            trail.material = material;
        }
    }

    private bool TryHitGhostBetween(Vector3 start, Vector3 direction, float distance)
    {
        if (distance <= 0f) return false;

        RaycastHit[] hits = Physics.SphereCastAll(start, Mathf.Max(0.01f, sweepRadius),
            direction, distance, hitLayers, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            GhostAI ghost = hit.collider.GetComponentInParent<GhostAI>();
            if (ghost == null) continue;

            Hit(ghost);
            return true;
        }

        return false;
    }

    private void Hit(GhostAI ghost)
    {
        if (ghost != null)
            ghost.TakeDamage(damage);

        Destroy(gameObject);
    }
}
