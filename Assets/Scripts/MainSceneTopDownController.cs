using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 카메라 전용 — 이동/전투는 PlayerController가 담당
public sealed class MainSceneTopDownController : MonoBehaviour
{
    [SerializeField] private string playerName = "Player";
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 9f, -7f);
    [SerializeField] private float cameraSmoothTime = 0.12f;
    [SerializeField] private float orthographicSize = 5.5f;
    [SerializeField] private float cameraRotateSmoothTime = 0.18f;

    private Transform player;
    private Camera sceneCamera;
    private Vector3 cameraVelocity;
    private Vector3 targetCameraOffset;
    private Vector3 cameraOffsetVelocity;
    private Quaternion cameraRotation;

    private void Awake()
    {
        sceneCamera = GetComponent<Camera>();
        if (sceneCamera == null)
            sceneCamera = gameObject.AddComponent<Camera>();

        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = orthographicSize;
        targetCameraOffset = cameraOffset;
        UpdateCameraRotation();

        if (GetComponent<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();

        TryAssignMainCameraTag();
        FindPlayer();
    }

    private void Start()
    {
        if (player == null) return;
        transform.position = player.position + cameraOffset;
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

        cameraOffset = Vector3.SmoothDamp(
            cameraOffset, targetCameraOffset, ref cameraOffsetVelocity, cameraRotateSmoothTime);
        UpdateCameraRotation();

        Vector3 targetPos = player.position + cameraOffset;
        transform.position = Vector3.SmoothDamp(
            transform.position, targetPos, ref cameraVelocity, cameraSmoothTime);
        transform.rotation = cameraRotation;
    }

    private void FindPlayer()
    {
        GameObject obj = GameObject.Find(playerName);
        if (obj != null) player = obj.transform;
    }

    private void HandleCameraRotationInput()
    {
        int steps = ReadCameraRotationInput();
        if (steps == 0) return;
        targetCameraOffset = Quaternion.Euler(0f, 90f * steps, 0f) * targetCameraOffset;
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

    private void UpdateCameraRotation()
    {
        cameraRotation = Quaternion.LookRotation(-cameraOffset.normalized, Vector3.up);
    }
}
