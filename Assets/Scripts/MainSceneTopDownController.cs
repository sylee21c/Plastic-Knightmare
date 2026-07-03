using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class MainSceneTopDownController : MonoBehaviour
{
    [SerializeField] private string playerName = "Player";
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float rotationSpeed = 14f;
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
        {
            sceneCamera = gameObject.AddComponent<Camera>();
        }

        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = orthographicSize;
        targetCameraOffset = cameraOffset;
        UpdateCameraRotation();

        if (GetComponent<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }

        TryAssignMainCameraTag();
        FindPlayer();
    }

    private void Start()
    {
        if (player == null)
        {
            return;
        }

        transform.position = player.position + cameraOffset;
        transform.rotation = cameraRotation;
    }

    private void Update()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }

        HandleCameraRotationInput();

        Vector2 input = ReadMoveInput();
        if (input.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 move = GetCameraRelativeMove(input);
        player.position += move * moveSpeed * Time.deltaTime;

        Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
        player.rotation = Quaternion.Slerp(player.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        cameraOffset = Vector3.SmoothDamp(cameraOffset, targetCameraOffset, ref cameraOffsetVelocity, cameraRotateSmoothTime);
        UpdateCameraRotation();

        Vector3 targetPosition = player.position + cameraOffset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref cameraVelocity, cameraSmoothTime);
        transform.rotation = cameraRotation;
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.Find(playerName);
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void HandleCameraRotationInput()
    {
        int rotationSteps = ReadCameraRotationInput();
        if (rotationSteps == 0)
        {
            return;
        }

        targetCameraOffset = Quaternion.Euler(0f, 90f * rotationSteps, 0f) * targetCameraOffset;
    }

    private int ReadCameraRotationInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.qKey.wasPressedThisFrame) return -1;
            if (keyboard.eKey.wasPressedThisFrame) return 1;
            return 0;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Q)) return -1;
        if (Input.GetKeyDown(KeyCode.E)) return 1;
#endif

        return 0;
    }

    private Vector3 GetCameraRelativeMove(Vector2 input)
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }

        return (forward * input.y + right * input.x).normalized;
    }

    private Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            Vector2 input = Vector2.zero;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed) input.y += 1f;
            return Vector2.ClampMagnitude(input, 1f);
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#else
        return Vector2.zero;
#endif
    }

    private void TryAssignMainCameraTag()
    {
        try
        {
            gameObject.tag = "MainCamera";
        }
        catch (UnityException)
        {
        }
    }

    private void UpdateCameraRotation()
    {
        cameraRotation = Quaternion.LookRotation(-cameraOffset.normalized, Vector3.up);
    }
}
