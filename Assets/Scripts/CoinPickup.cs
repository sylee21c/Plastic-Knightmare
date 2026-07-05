using UnityEngine;

public sealed class CoinPickup : MonoBehaviour
{
    [Header("Value")]
    [SerializeField] private int value = 100;

    [Header("Visuals")]
    [Tooltip("모든 코인의 최종 시각적 지름 (월드 단위). 메쉬 크기 무관하게 자동 정규화")]
    [SerializeField] private float targetDiameter = 0.55f;
    [Tooltip("targetDiameter 를 못 쓸 때 폴백으로 사용할 균일 스케일")]
    [SerializeField] private float fallbackUniformScale = 9.5f;
    [SerializeField] private float floatHeight = 0.4f;
    [SerializeField] private float rotateSpeedY = 180f; // deg/sec
    [SerializeField] private float bobAmplitude = 0.05f;
    [SerializeField] private float bobFrequency = 2f;

    [Header("Pickup")]
    [SerializeField] private float pickupRadius = 0.6f;
    [SerializeField] private string playerName = "Player";

    private float baseY;
    private float bobPhase;
    private float spinYaw;

    public int Value { get => value; set => this.value = value; }

    private void Start()
    {
        // 각 코인의 mesh 크기가 달라도 실제 렌더 지름이 동일하도록 자동 정규화
        NormalizeVisualSize();
        spinYaw = 0f;

        // 지면 높이 찾기
        BuildingGridOverlay grid = FindAnyObjectByType<BuildingGridOverlay>();
        float groundY;
        if (grid != null && grid.CellSize > 0f)
        {
            groundY = grid.SurfaceY;
        }
        else if (Physics.Raycast(transform.position + Vector3.up * 3f, Vector3.down,
                 out RaycastHit hit, 15f, ~0, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
        }
        else
        {
            groundY = transform.position.y;
        }
        baseY = groundY + floatHeight;

        Vector3 pos = transform.position;
        pos.y = baseY;
        transform.position = pos;

        bobPhase = Random.Range(0f, Mathf.PI * 2f);

        // 코인끼리 콜리전 안 나도록 모든 콜라이더를 트리거로. 없으면 SphereCollider 추가.
        Collider[] cols = GetComponentsInChildren<Collider>();
        if (cols.Length == 0)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            float effectiveScale = Mathf.Max(0.001f, transform.localScale.x);
            sc.radius = pickupRadius / effectiveScale;
            sc.isTrigger = true;
        }
        else
        {
            foreach (Collider c in cols) c.isTrigger = true;
        }
    }

    // Bronze/Silver/Gold 메쉬 크기가 서로 달라도 최종 지름을 targetDiameter 로 통일.
    // Mesh.bounds (mesh-local) 를 직접 읽어 자식 스케일 누적까지 반영 → 안정적.
    private void NormalizeVisualSize()
    {
        if (targetDiameter <= 0f)
        {
            transform.localScale = Vector3.one * fallbackUniformScale;
            return;
        }

        MeshFilter[] mfs = GetComponentsInChildren<MeshFilter>();
        if (mfs.Length == 0)
        {
            // SkinnedMeshRenderer 등 있을 수도
            SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null)
                ApplyScaleFromMesh(smr.sharedMesh.bounds.size, smr.transform);
            else
                transform.localScale = Vector3.one * fallbackUniformScale;
            return;
        }

        // 여러 mesh 합쳐서 최대 크기 산정
        Vector3 combinedSize = Vector3.zero;
        Transform chosenChild = null;
        float bestDiameter = 0f;
        foreach (MeshFilter mf in mfs)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            Vector3 s = mf.sharedMesh.bounds.size;
            Vector3 accScale = GetAccumulatedScaleFromRoot(mf.transform);
            Vector3 worldSpaceSize = Vector3.Scale(s, accScale);
            float d = Mathf.Max(worldSpaceSize.x, worldSpaceSize.z);
            if (d <= 0.001f) d = Mathf.Max(worldSpaceSize.y, 0.001f);
            if (d > bestDiameter) { bestDiameter = d; chosenChild = mf.transform; combinedSize = worldSpaceSize; }
        }

        if (bestDiameter <= 0.001f || chosenChild == null)
        {
            transform.localScale = Vector3.one * fallbackUniformScale;
            return;
        }

        float rootScale = targetDiameter / bestDiameter;
        if (rootScale <= 0f || float.IsInfinity(rootScale) || float.IsNaN(rootScale))
            rootScale = fallbackUniformScale;

        transform.localScale = Vector3.one * rootScale;
    }

    private void ApplyScaleFromMesh(Vector3 meshSize, Transform meshTransform)
    {
        Vector3 accScale = GetAccumulatedScaleFromRoot(meshTransform);
        Vector3 sizeAtRootUnit = Vector3.Scale(meshSize, accScale);
        float d = Mathf.Max(sizeAtRootUnit.x, sizeAtRootUnit.z);
        if (d <= 0.001f) d = Mathf.Max(sizeAtRootUnit.y, 0.001f);
        if (d <= 0.001f)
        {
            transform.localScale = Vector3.one * fallbackUniformScale;
            return;
        }
        transform.localScale = Vector3.one * (targetDiameter / d);
    }

    // 루트(this.transform)의 localScale=1 을 가정하고, 대상 transform까지 누적된 자식 스케일
    private Vector3 GetAccumulatedScaleFromRoot(Transform target)
    {
        Vector3 acc = Vector3.one;
        Transform t = target;
        while (t != null && t != transform)
        {
            acc = Vector3.Scale(acc, t.localScale);
            t = t.parent;
        }
        return acc;
    }

    private void Update()
    {
        // Y축 회전, Z=-90 유지
        spinYaw += rotateSpeedY * Time.deltaTime;
        transform.rotation = Quaternion.Euler(0f, spinYaw, -90f);

        // 위아래 살짝 부양
        Vector3 pos = transform.position;
        pos.y = baseY + Mathf.Sin(Time.time * bobFrequency + bobPhase) * bobAmplitude;
        transform.position = pos;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        CoinWallet.Instance?.Add(value, true);
        SFXManager.PlayGlobal(SFXManager.Sfx.CoinPickup);
        Destroy(gameObject);
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        return other.name == playerName
            || other.transform.root.name == playerName
            || other.CompareTag("Player")
            || other.transform.root.CompareTag("Player");
    }

#if UNITY_EDITOR
    // 씬 뷰에서 항상 옅게 표시 → 여러 코인이 있어도 반경 파악 가능
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.28f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }

    // 선택 시 진하게 + 채운 반투명
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.18f);
        Gizmos.DrawSphere(transform.position, pickupRadius);
    }
#endif
}
