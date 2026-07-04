using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 카메라 전용 — 이동/전투는 PlayerController 담당.
// 회전: yaw 각도만 스무딩 → 궤도 반경/높이 일정 유지.
// 위치: 플레이어 추종을 별도 SmoothDamp 로 처리해 결정론적 조합.
public sealed class MainSceneTopDownController : MonoBehaviour
{
    [SerializeField] private string playerName = "Player";
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 9f, -7f);
    [Tooltip("플레이어 이동에 카메라가 따라오는 부드러움 정도 (초)")]
    [SerializeField] private float playerFollowSmoothTime = 0.12f;
    [Tooltip("Q/E 회전 부드러움 정도 (초). 크면 더 느긋하고 부드러움")]
    [SerializeField] private float cameraRotateSmoothTime = 0.28f;
    [SerializeField] private float orthographicSize = 5.5f;

    private Transform player;
    private Camera sceneCamera;
    private Quaternion cameraRotation;

    // 회전: Yaw 각도만 스무딩
    private Vector3 baseCameraOffset;
    private float currentYaw;
    private float targetYaw;
    private float yawVelocity;

    // 위치: 플레이어 위치를 별도 스무딩 (물리 스텝 지터 제거)
    private Vector3 smoothedPlayerPos;
    private Vector3 playerFollowVelocity;

    private void Awake()
    {
        sceneCamera = GetComponent<Camera>();
        if (sceneCamera == null)
            sceneCamera = gameObject.AddComponent<Camera>();

        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = orthographicSize;

        baseCameraOffset = cameraOffset;
        currentYaw = 0f;
        targetYaw = 0f;
        UpdateCameraRotation(cameraOffset);

        if (GetComponent<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();

        TryAssignMainCameraTag();
        FindPlayer();
    }

    private void Start()
    {
        if (player == null) return;
        smoothedPlayerPos = player.position;
        transform.position = smoothedPlayerPos + baseCameraOffset;
        transform.rotation = cameraRotation;
    }

    private void Update()
    {
        if (player == null) { FindPlayer(); return; }
        HandleCameraRotationInput();
    }

    private void LateUpdate()
    {
        if (player == null) return;

        // 1) 플레이어 위치 스무딩 (물리 지터 흡수)
        smoothedPlayerPos = Vector3.SmoothDamp(
            smoothedPlayerPos, player.position, ref playerFollowVelocity, playerFollowSmoothTime);

        // 2) Yaw 각도 스무딩 (반경/높이 유지)
        currentYaw = Mathf.SmoothDampAngle(
            currentYaw, targetYaw, ref yawVelocity, cameraRotateSmoothTime);

        // 3) 결정론적 조합: 두 스무딩이 서로 다른 경로로 어긋나지 않음
        Vector3 offset = Quaternion.Euler(0f, currentYaw, 0f) * baseCameraOffset;
        UpdateCameraRotation(offset);
        transform.position = smoothedPlayerPos + offset;
        transform.rotation = cameraRotation;
    }

    private void FindPlayer()
    {
        GameObject obj = GameObject.Find(playerName);
        if (obj == null) return;

        player = obj.transform;
        smoothedPlayerPos = player.position;

        // 플레이어 Rigidbody 를 Interpolate 로 → 물리 스텝 사이 시각적 부드러움 확보
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null && rb.interpolation == RigidbodyInterpolation.None)
            rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void HandleCameraRotationInput()
    {
        int steps = ReadCameraRotationInput();
        if (steps == 0) return;
        targetYaw += 90f * steps;
    }

    private int ReadCameraRotationInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.qKey.wasPressedThisFrame) return -1;
            if (kb.eKey.wasPressedThisFrame) return 1;
            return 0;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Q)) return -1;
        if (Input.GetKeyDown(KeyCode.E)) return 1;
#endif
        return 0;
    }

    private void TryAssignMainCameraTag()
    {
        try { gameObject.tag = "MainCamera"; }
        catch (UnityException) { }
    }

    private void UpdateCameraRotation(Vector3 offset)
    {
        cameraRotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);
    }
}
