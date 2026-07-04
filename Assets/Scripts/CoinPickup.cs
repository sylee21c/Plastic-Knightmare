using UnityEngine;

public sealed class CoinPickup : MonoBehaviour
{
    [Header("Value")]
    [SerializeField] private int value = 100;

    [Header("Visuals")]
    [SerializeField] private float uniformScale = 9.5f;
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
        // 크기/자세 강제 세팅
        transform.localScale = Vector3.one * uniformScale;
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
            sc.radius = pickupRadius / uniformScale; // 스케일 반영
            sc.isTrigger = true;
        }
        else
        {
            foreach (Collider c in cols) c.isTrigger = true;
        }
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
        CoinWallet.Instance?.Add(value);
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
