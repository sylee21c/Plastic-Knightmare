using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class BuildingModeController : MonoBehaviour
{
    [SerializeField] private string playerName = "Player";
    [SerializeField] private BuildingGridOverlay gridOverlay;
    [SerializeField] private GameObject brickPrefab;
    [SerializeField] private string sceneBrickTemplateName = "Lego_Part_3";
    [SerializeField] private float brickYOffset = 0f;
    [SerializeField] private float stackOverlapY = 0.0473111f;
    [SerializeField] private bool requireDetectedFloorCell = false;
    [SerializeField] private Color previewColor = new Color(1f, 1f, 1f, 0.36f);
    [SerializeField] private Color blockedPreviewColor = new Color(1f, 0.25f, 0.2f, 0.3f);

    private readonly Dictionary<Vector2Int, List<GameObject>> placedBricks = new Dictionary<Vector2Int, List<GameObject>>();

    private Transform player;
    private Camera sceneCamera;
    private GameObject previewBrick;
    private Material previewMaterial;
    private GameObject previewTemplate;
    private Vector2Int highlightedCell;
    private bool hasHighlightedCell;
    private bool canBuildOnHighlightedCell;
    private bool previousLeftMousePressed;
    private bool previousRightMousePressed;
    private bool queuedLeftClickBuild;
    private bool queuedRightClickDestroy;

    private void Awake()
    {
        FindReferences();
        RefreshPreviewBrick();
        BuildingHotbarUI.EnsureExists();
    }

    private void Update()
    {
        FindReferences();
        RefreshPreviewBrick();
        UpdateHighlight();
        HandleBuildInput();
        HandleDestroyInput();
    }

    private void OnGUI()
    {
        Event current = Event.current;
        if (current == null || current.type != EventType.MouseDown)
        {
            return;
        }

        if (current.button == 0)
        {
            queuedLeftClickBuild = true;
        }
        else if (current.button == 1)
        {
            queuedRightClickDestroy = true;
        }
        else
        {
            return;
        }

        current.Use();
    }

    private void FindReferences()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.Find(playerName);
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (gridOverlay == null)
        {
            gridOverlay = FindAnyObjectByType<BuildingGridOverlay>();
        }

        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                sceneCamera = FindAnyObjectByType<Camera>();
            }
        }
    }

    private void UpdateHighlight()
    {
        hasHighlightedCell = TryGetTargetCell(out highlightedCell);
        canBuildOnHighlightedCell =
            hasHighlightedCell &&
            (!requireDetectedFloorCell || gridOverlay.IsCellOnFloor(highlightedCell));

        if (previewBrick == null)
        {
            return;
        }

        previewBrick.SetActive(hasHighlightedCell);
        if (!hasHighlightedCell)
        {
            return;
        }

        PlaceBrickOnCell(previewBrick, highlightedCell, GetStackCount(highlightedCell));
        previewMaterial.color = canBuildOnHighlightedCell ? previewColor : blockedPreviewColor;
    }

    private bool TryGetTargetCell(out Vector2Int targetCell)
    {
        targetCell = default;
        if (player == null || gridOverlay == null || gridOverlay.CellSize <= 0f)
        {
            return false;
        }

        if (sceneCamera == null)
        {
            return false;
        }

        Ray ray = sceneCamera.ScreenPointToRay(GetMousePosition());
        Plane floorPlane = new Plane(Vector3.up, new Vector3(0f, gridOverlay.SurfaceY, 0f));
        if (!floorPlane.Raycast(ray, out float enter))
        {
            return false;
        }

        Vector3 floorPoint = ray.GetPoint(enter);
        targetCell = gridOverlay.WorldToCell(floorPoint);
        return true;
    }

    private void HandleBuildInput()
    {
        if (!WasLeftMousePressed() && !queuedLeftClickBuild)
        {
            return;
        }

        queuedLeftClickBuild = false;

        GameObject template = ResolveBrickTemplate();
        if (!canBuildOnHighlightedCell || template == null)
        {
            return;
        }

        Vector3 position = gridOverlay.CellToWorldCenter(highlightedCell);
        GameObject brick = Instantiate(template, position, Quaternion.identity);
        brick.name = $"{template.name}_Built";
        brick.SetActive(true);
        int stackIndex = GetStackCount(highlightedCell);
        PlaceBrickOnCell(brick, highlightedCell, stackIndex);

        BuildingPlacedBrick marker = brick.GetComponent<BuildingPlacedBrick>();
        if (marker == null)
        {
            marker = brick.AddComponent<BuildingPlacedBrick>();
        }

        marker.Cell = highlightedCell;
        marker.StackIndex = stackIndex;
        EnsureClickableCollider(brick);
        GetOrCreateStack(highlightedCell).Add(brick);
    }

    private void PlaceBrickOnCell(GameObject brick, Vector2Int cell, int stackIndex)
    {
        brick.transform.position = gridOverlay.CellToWorldCenter(cell);
        FitBrickToCell(brick, gridOverlay.CellSize);
        MoveBottomToSurface(brick, gridOverlay.SurfaceY + brickYOffset);

        if (stackIndex > 0)
        {
            float stackStep = Mathf.Max(0f, GetBrickHeight(brick) - stackOverlapY);
            brick.transform.position += Vector3.up * (stackStep * stackIndex);
        }
    }

    private GameObject ResolveBrickTemplate()
    {
        if (brickPrefab != null)
        {
            return brickPrefab;
        }

        if (string.IsNullOrWhiteSpace(sceneBrickTemplateName))
        {
            return null;
        }

        GameObject sceneTemplate = GameObject.Find(sceneBrickTemplateName);
        if (sceneTemplate != null && sceneTemplate.GetComponent<BuildingPlacedBrick>() == null)
        {
            return sceneTemplate;
        }

        return null;
    }

    private static void FitBrickToCell(GameObject brick, float cellSize)
    {
        if (cellSize <= 0f)
        {
            return;
        }

        Renderer[] renderers = brick.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
        if (footprint <= Mathf.Epsilon)
        {
            return;
        }

        float scaleFactor = cellSize / footprint;
        brick.transform.localScale *= scaleFactor;
    }

    private static void MoveBottomToSurface(GameObject brick, float surfaceY)
    {
        Renderer[] renderers = brick.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Vector3 position = brick.transform.position;
            position.y = surfaceY;
            brick.transform.position = position;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        brick.transform.position += Vector3.up * (surfaceY - bounds.min.y);
    }

    private void HandleDestroyInput()
    {
        if ((!WasRightMousePressed() && !queuedRightClickDestroy) || !hasHighlightedCell)
        {
            return;
        }

        queuedRightClickDestroy = false;

        if (!placedBricks.TryGetValue(highlightedCell, out List<GameObject> stack) || stack.Count == 0)
        {
            placedBricks.Remove(highlightedCell);
            return;
        }

        int topIndex = stack.Count - 1;
        GameObject brick = stack[topIndex];
        stack.RemoveAt(topIndex);

        if (stack.Count == 0)
        {
            placedBricks.Remove(highlightedCell);
        }

        if (brick != null)
        {
            Destroy(brick);
        }
    }

    private int GetStackCount(Vector2Int cell)
    {
        return placedBricks.TryGetValue(cell, out List<GameObject> stack) ? stack.Count : 0;
    }

    private List<GameObject> GetOrCreateStack(Vector2Int cell)
    {
        if (!placedBricks.TryGetValue(cell, out List<GameObject> stack))
        {
            stack = new List<GameObject>();
            placedBricks.Add(cell, stack);
        }

        return stack;
    }

    private static float GetBrickHeight(GameObject brick)
    {
        Renderer[] renderers = brick.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return 0f;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.size.y;
    }

    private static void EnsureClickableCollider(GameObject brick)
    {
        if (brick.GetComponentInChildren<Collider>() != null)
        {
            return;
        }

        Renderer[] renderers = brick.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            brick.AddComponent<BoxCollider>();
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        BoxCollider collider = brick.AddComponent<BoxCollider>();
        collider.center = brick.transform.InverseTransformPoint(bounds.center);
        Vector3 localMin = brick.transform.InverseTransformPoint(bounds.min);
        Vector3 localMax = brick.transform.InverseTransformPoint(bounds.max);
        collider.size = new Vector3(
            Mathf.Abs(localMax.x - localMin.x),
            Mathf.Abs(localMax.y - localMin.y),
            Mathf.Abs(localMax.z - localMin.z));
    }

    private void RefreshPreviewBrick()
    {
        GameObject template = ResolveBrickTemplate();
        if (template == null || template == previewTemplate)
        {
            return;
        }

        previewTemplate = template;
        if (previewBrick != null)
        {
            Destroy(previewBrick);
        }

        previewBrick = Instantiate(template, transform);
        previewBrick.name = $"{template.name}_Preview";
        previewBrick.SetActive(false);

        foreach (Collider collider in previewBrick.GetComponentsInChildren<Collider>())
        {
            Destroy(collider);
        }

        foreach (BuildingPlacedBrick marker in previewBrick.GetComponentsInChildren<BuildingPlacedBrick>())
        {
            Destroy(marker);
        }

        previewMaterial = new Material(FindPreviewShader())
        {
            name = "Build Preview Material",
            color = previewColor
        };
        ConfigurePreviewMaterial(previewMaterial);

        foreach (Renderer renderer in previewBrick.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterial = previewMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static Shader FindPreviewShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            return shader;
        }

        shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            return shader;
        }

        return Shader.Find("Standard");
    }

    private static void ConfigurePreviewMaterial(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static Vector3 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.position.ReadValue();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector3.zero;
#endif
    }

    private bool WasLeftMousePressed()
    {
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            pressed = mouse.leftButton.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            previousLeftMousePressed = true;
            return true;
        }
#endif

        bool wasPressedThisFrame = pressed && !previousLeftMousePressed;
        previousLeftMousePressed = pressed;
        return wasPressedThisFrame;
    }

    private bool WasRightMousePressed()
    {
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            pressed = mouse.rightButton.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(1))
        {
            previousRightMousePressed = true;
            return true;
        }
#endif

        bool wasPressedThisFrame = pressed && !previousRightMousePressed;
        previousRightMousePressed = pressed;
        return wasPressedThisFrame;
    }
}
