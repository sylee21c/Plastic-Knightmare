using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class BuildingModeController : MonoBehaviour
{
    private enum BrickOrientation
    {
        Horizontal,
        Vertical
    }

    private enum HalfCellSide
    {
        None,
        Negative,
        Positive
    }

    [System.Serializable]
    private sealed class BrickDefinition
    {
        [SerializeField] private string displayName = "2x2";
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(1)] private int cellsLong = 1;
        [SerializeField] private bool halfCell;

        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public int CellsLong => Mathf.Max(1, cellsLong);
        public bool HalfCell => halfCell;
        public bool CanRotate => halfCell || CellsLong > 1;
    }

    private readonly struct PlacementKey
    {
        public readonly Vector2Int Cell;
        public readonly HalfCellSide HalfSide;

        public PlacementKey(Vector2Int cell, HalfCellSide halfSide)
        {
            Cell = cell;
            HalfSide = halfSide;
        }

        public override bool Equals(object obj)
        {
            return obj is PlacementKey other && Cell == other.Cell && HalfSide == other.HalfSide;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Cell.x;
                hash = hash * 31 + Cell.y;
                hash = hash * 31 + (int)HalfSide;
                return hash;
            }
        }
    }

    private readonly struct Placement
    {
        public readonly Vector2Int AnchorCell;
        public readonly Vector3 Center;
        public readonly PlacementKey[] Keys;
        public readonly HalfCellSide HalfSide;

        public Placement(Vector2Int anchorCell, Vector3 center, PlacementKey[] keys, HalfCellSide halfSide)
        {
            AnchorCell = anchorCell;
            Center = center;
            Keys = keys;
            HalfSide = halfSide;
        }
    }

    [SerializeField] private string playerName = "Player";
    [SerializeField] private BuildingGridOverlay gridOverlay;
    [SerializeField] private GameObject brickPrefab;
    [SerializeField] private string sceneBrickTemplateName = "Lego_Part_3";
    [SerializeField] private BrickDefinition[] brickDefinitions =
    {
        new BrickDefinition(),
        new BrickDefinition()
    };
    [SerializeField] private float brickYOffset = 0f;
    [SerializeField] private float stackOverlapY = 0.0473111f;
    [SerializeField] private bool requireDetectedFloorCell = false;
    [Tooltip("씬의 BuildableCellMarker 오브젝트가 위치한 칸에만 배치 허용")]
    [SerializeField] private bool restrictToBuildableMarkers = false;

    [Header("Brick Health")]
    [SerializeField] private float brickBaseHealth = 100f;
    [SerializeField] private float brickStackBonus = 30f;
    [SerializeField] private bool addHealthBarToBricks = true;
    [SerializeField] private Vector3 brickHealthBarOffset = new Vector3(0f, 0.55f, 0f);
    [SerializeField] private Vector2 brickHealthBarPixelSize = new Vector2(90f, 10f);
    [SerializeField] private float brickHealthBarWorldScale = 0.008f;
    [SerializeField] private Color previewColor = new Color(1f, 1f, 1f, 0.36f);
    [SerializeField] private Color blockedPreviewColor = new Color(1f, 0.25f, 0.2f, 0.3f);

    private readonly Dictionary<PlacementKey, List<GameObject>> placedBricks = new Dictionary<PlacementKey, List<GameObject>>();

    private Transform player;
    private Camera sceneCamera;
    private GameObject previewBrick;
    private Material previewMaterial;
    private GameObject previewTemplate;
    private Vector2Int highlightedCell;
    private Vector3 highlightedFloorPoint;
    private bool hasHighlightedCell;
    private bool canBuildOnHighlightedCell;
    private bool previousLeftMousePressed;
    private bool previousRightMousePressed;
    private bool queuedLeftClickBuild;
    private bool queuedRightClickDestroy;
    private bool isDragSelecting;
    private Vector2Int dragAnchorCell;
    private readonly List<GameObject> dragGhostBricks = new List<GameObject>();
    private readonly List<Vector3> dragGhostOriginalScales = new List<Vector3>();
    private Material dragGhostMaterial;
    private int dragGhostBrickIndex = -1;
    private int selectedBrickIndex = 0;
    private BrickOrientation selectedOrientation = BrickOrientation.Horizontal;
    private const int HotbarSlotCount = 5;

    private void Awake()
    {
        FindReferences();
        BuildingHotbarUI.EnsureExists();
        BuildingHotbarUI.SetSelectedSlot(selectedBrickIndex);
        RefreshPreviewBrick(true);
    }

    private void OnDisable()
    {
        if (previewBrick != null)
        {
            previewBrick.SetActive(false);
        }
    }

    private void Update()
    {
        FindReferences();
        HandleSelectionInput();
        RefreshPreviewBrick(false);
        UpdateHighlight();
        HandleBuildInput();
        UpdateDragSelect();
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

    private void HandleSelectionInput()
    {
        int selectableSlots = Mathf.Max(HotbarSlotCount, brickDefinitions?.Length ?? 0);
        for (int i = 0; i < selectableSlots; i++)
        {
            if (WasNumberKeyPressed(i + 1))
            {
                SelectBrick(i);
                return;
            }
        }

        BrickDefinition selectedBrick = GetSelectedBrick();
        if (selectedBrick != null && selectedBrick.CanRotate && WasRotateKeyPressed())
        {
            selectedOrientation = selectedOrientation == BrickOrientation.Horizontal
                ? BrickOrientation.Vertical
                : BrickOrientation.Horizontal;
        }
    }

    private void SelectBrick(int index)
    {
        int selectableSlots = Mathf.Max(HotbarSlotCount, brickDefinitions?.Length ?? 1);
        int clampedIndex = Mathf.Clamp(index, 0, selectableSlots - 1);
        if (selectedBrickIndex == clampedIndex)
        {
            return;
        }

        selectedBrickIndex = clampedIndex;
        if (GetSelectedBrick()?.CanRotate != true)
        {
            selectedOrientation = BrickOrientation.Horizontal;
        }

        BuildingHotbarUI.SetSelectedSlot(selectedBrickIndex);
        RefreshPreviewBrick(true);
    }

    private void UpdateHighlight()
    {
        hasHighlightedCell = TryGetTargetCell(out highlightedCell, out highlightedFloorPoint);
        canBuildOnHighlightedCell = false;

        BrickDefinition selectedBrick = GetSelectedBrick();
        Placement placement = default;
        bool hasPlacement = selectedBrick != null && hasHighlightedCell;
        if (hasPlacement)
        {
            hasPlacement = TryGetPlacement(selectedBrick, out placement);
        }

        if (hasPlacement)
        {
            bool placementAllowed = IsPlacementAllowed(placement);
            bool inStock = HasInventoryFor(selectedBrick);
            canBuildOnHighlightedCell = placementAllowed && inStock;
        }

        if (previewBrick == null)
        {
            return;
        }

        previewBrick.SetActive(hasPlacement && !isDragSelecting);
        if (!hasPlacement)
        {
            return;
        }

        int stackIndex = GetStackCount(placement.Keys);
        PlaceBrick(previewBrick, selectedBrick, placement, stackIndex);
        if (previewMaterial != null)
        {
            previewMaterial.color = canBuildOnHighlightedCell ? previewColor : blockedPreviewColor;
        }
    }

    private bool TryGetTargetCell(out Vector2Int targetCell, out Vector3 floorPoint)
    {
        targetCell = default;
        floorPoint = default;
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

        floorPoint = ray.GetPoint(enter);
        targetCell = gridOverlay.WorldToCell(floorPoint);
        return true;
    }

    private bool TryGetPlacement(BrickDefinition brick, out Placement placement)
    {
        placement = default;
        if (gridOverlay == null || gridOverlay.CellSize <= 0f)
        {
            return false;
        }

        if (brick.HalfCell)
        {
            HalfCellSide side = GetHalfCellSide(highlightedCell, highlightedFloorPoint);
            Vector3 center = gridOverlay.CellToWorldCenter(highlightedCell);
            float offset = gridOverlay.CellSize * 0.25f;
            if (selectedOrientation == BrickOrientation.Vertical)
            {
                center.x += side == HalfCellSide.Negative ? -offset : offset;
            }
            else
            {
                center.z += side == HalfCellSide.Negative ? -offset : offset;
            }

            placement = new Placement(
                highlightedCell,
                center,
                new[] { new PlacementKey(highlightedCell, side) },
                side);
            return true;
        }

        PlacementKey[] keys = new PlacementKey[brick.CellsLong];
        Vector3 centerSum = Vector3.zero;
        for (int i = 0; i < brick.CellsLong; i++)
        {
            Vector2Int cell = highlightedCell + GetCellOffset(i);
            keys[i] = new PlacementKey(cell, HalfCellSide.None);
            centerSum += gridOverlay.CellToWorldCenter(cell);
        }

        placement = new Placement(highlightedCell, centerSum / brick.CellsLong, keys, HalfCellSide.None);
        return true;
    }

    private Vector2Int GetCellOffset(int distance)
    {
        return selectedOrientation == BrickOrientation.Horizontal
            ? new Vector2Int(distance, 0)
            : new Vector2Int(0, distance);
    }

    private HalfCellSide GetHalfCellSide(Vector2Int cell, Vector3 floorPoint)
    {
        Vector3 center = gridOverlay.CellToWorldCenter(cell);
        if (selectedOrientation == BrickOrientation.Vertical)
        {
            return floorPoint.x < center.x ? HalfCellSide.Negative : HalfCellSide.Positive;
        }

        return floorPoint.z < center.z ? HalfCellSide.Negative : HalfCellSide.Positive;
    }

    private bool IsPlacementAllowed(Placement placement)
    {
        // 지정 셀 모드: 마커 셀에 포함된 것만 통과
        if (restrictToBuildableMarkers)
        {
            HashSet<Vector2Int> allowed = GetBuildableMarkerCells();
            if (allowed != null && allowed.Count > 0)
            {
                foreach (PlacementKey key in placement.Keys)
                {
                    if (!allowed.Contains(key.Cell)) return false;
                }
                return true;
            }
        }

        if (!requireDetectedFloorCell)
        {
            return true;
        }

        foreach (PlacementKey key in placement.Keys)
        {
            if (!gridOverlay.IsCellOnFloor(key.Cell))
            {
                return false;
            }
        }

        return true;
    }

    private void HandleBuildInput()
    {
        if (!WasLeftMousePressed() && !queuedLeftClickBuild)
            return;

        queuedLeftClickBuild = false;

        // Shift + 클릭 → 직사각형 드래그 선택 시작 (배치는 마우스 놓을 때)
        if (IsShiftHeld() && hasHighlightedCell)
        {
            isDragSelecting = true;
            dragAnchorCell = highlightedCell;
            dragGhostBrickIndex = -1;
            return;
        }

        // 일반 클릭 → 단일 배치
        BrickDefinition selectedBrick = GetSelectedBrick();
        GameObject template = ResolveBrickTemplate(selectedBrick);
        if (!canBuildOnHighlightedCell || selectedBrick == null || template == null
            || !TryGetPlacement(selectedBrick, out Placement placement))
            return;

        // 인벤토리 소모 (없으면 배치 못 함)
        if (!TryConsumeInventoryForBrick(selectedBrick)) return;

        SpawnBrick(selectedBrick, template, placement);
    }

    private bool TryConsumeInventoryForBrick(BrickDefinition brick)
    {
        if (brick == null) return false;
        BrickInventory.EnsureExists();
        if (!BrickInventory.Instance.TryConsume(brick.DisplayName, 1))
        {
            Debug.Log($"[BuildingMode] {brick.DisplayName} 브릭 재고 없음");
            return false;
        }
        return true;
    }

    // 재고 유무 확인 (Editor 모드에서는 항상 있는 것으로 간주 → 프리뷰 정상 표시)
    private bool HasInventoryFor(BrickDefinition brick)
    {
        if (brick == null) return false;
        if (!Application.isPlaying) return true;
        BrickInventory.EnsureExists();
        return BrickInventory.Instance.GetCount(brick.DisplayName) > 0;
    }

    // 마커 셀 캐시 (매 프레임 재계산 — 마커가 씬에서 이동해도 반영)
    private HashSet<Vector2Int> cachedBuildableCells;
    private int cachedBuildableFrame = -1;

    private HashSet<Vector2Int> GetBuildableMarkerCells()
    {
        if (gridOverlay == null || gridOverlay.CellSize <= 0f) return null;

        // 프레임당 한 번만 재계산
        if (cachedBuildableCells != null && cachedBuildableFrame == Time.frameCount)
            return cachedBuildableCells;

        if (cachedBuildableCells == null)
            cachedBuildableCells = new HashSet<Vector2Int>();
        else
            cachedBuildableCells.Clear();

#if UNITY_2023_1_OR_NEWER
        BuildableCellMarker[] markers = Object.FindObjectsByType<BuildableCellMarker>(FindObjectsSortMode.None);
#else
        BuildableCellMarker[] markers = Object.FindObjectsOfType<BuildableCellMarker>();
#endif
        foreach (BuildableCellMarker m in markers)
        {
            if (m == null) continue;
            cachedBuildableCells.Add(m.GetCell(gridOverlay));
        }

        cachedBuildableFrame = Time.frameCount;
        return cachedBuildableCells;
    }

    private void UpdateDragSelect()
    {
        if (!isDragSelecting)
            return;

        // Shift를 놓으면 취소
        if (!IsShiftHeld())
        {
            ClearDragGhosts();
            isDragSelecting = false;
            return;
        }

        // 마우스를 놓으면 배치 확정
        if (!IsLeftMouseHeld())
        {
            CommitDragSelect();
            isDragSelecting = false;
            return;
        }

        if (hasHighlightedCell)
            RefreshDragGhosts();
    }

    private void CommitDragSelect()
    {
        BrickDefinition selectedBrick = GetSelectedBrick();
        if (selectedBrick == null) return;

        GameObject template = ResolveBrickTemplate(selectedBrick);
        if (template == null) return;

        Vector2Int currentCell = hasHighlightedCell ? highlightedCell : dragAnchorCell;
        List<Vector2Int> cells = GetRectangleCells(dragAnchorCell, currentCell);

        foreach (Vector2Int cell in cells)
        {
            if (!TryGetPlacementForCell(selectedBrick, cell, out Placement placement))
                continue;
            if (!IsPlacementAllowed(placement))
                continue;
            // 인벤토리 없으면 이 위치는 건너뜀
            if (!TryConsumeInventoryForBrick(selectedBrick))
                break; // 재고 다 소진되면 나머지 셀도 건너뜀
            SpawnBrick(selectedBrick, template, placement);
        }

        ClearDragGhosts();
    }

    private void RefreshDragGhosts()
    {
        BrickDefinition selectedBrick = GetSelectedBrick();
        if (selectedBrick == null) { ClearDragGhosts(); return; }

        // 브릭 타입이 바뀌면 풀 초기화
        if (dragGhostBrickIndex != selectedBrickIndex)
        {
            ClearDragGhosts();
            dragGhostBrickIndex = selectedBrickIndex;
        }

        List<Vector2Int> cells = GetRectangleCells(dragAnchorCell, highlightedCell);
        GameObject template = ResolveBrickTemplate(selectedBrick);
        if (template == null) { ClearDragGhosts(); return; }

        EnsureDragGhostMaterial();

        // 풀 확장
        while (dragGhostBricks.Count < cells.Count)
        {
            GameObject ghost = Instantiate(template, transform);
            ghost.SetActive(false);
            foreach (Collider col in ghost.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (BuildingPlacedBrick m in ghost.GetComponentsInChildren<BuildingPlacedBrick>()) Destroy(m);
            foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = dragGhostMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            dragGhostBricks.Add(ghost);
            dragGhostOriginalScales.Add(ghost.transform.localScale);
        }

        // 활성 고스트 배치
        for (int i = 0; i < cells.Count; i++)
        {
            if (!TryGetPlacementForCell(selectedBrick, cells[i], out Placement placement))
            {
                dragGhostBricks[i].SetActive(false);
                continue;
            }
            dragGhostBricks[i].SetActive(true);
            dragGhostBricks[i].transform.localScale = dragGhostOriginalScales[i];
            PlaceBrick(dragGhostBricks[i], selectedBrick, placement, GetStackCount(placement.Keys));
        }

        // 초과 고스트 비활성
        for (int i = cells.Count; i < dragGhostBricks.Count; i++)
            dragGhostBricks[i].SetActive(false);
    }

    private void ClearDragGhosts()
    {
        foreach (GameObject ghost in dragGhostBricks)
            if (ghost != null) Destroy(ghost);
        dragGhostBricks.Clear();
        dragGhostOriginalScales.Clear();
    }

    private void EnsureDragGhostMaterial()
    {
        if (dragGhostMaterial != null) return;
        dragGhostMaterial = new Material(FindPreviewShader())
        {
            name = "Drag Ghost Material",
            color = previewColor
        };
        ConfigurePreviewMaterial(dragGhostMaterial);
    }

    private bool TryGetPlacementForCell(BrickDefinition brick, Vector2Int cell, out Placement placement)
    {
        placement = default;
        if (gridOverlay == null || gridOverlay.CellSize <= 0f) return false;

        if (brick.HalfCell)
        {
            HalfCellSide side = HalfCellSide.Negative;
            Vector3 center = gridOverlay.CellToWorldCenter(cell);
            float offset = gridOverlay.CellSize * 0.25f;
            if (selectedOrientation == BrickOrientation.Vertical)
                center.x -= offset;
            else
                center.z -= offset;

            placement = new Placement(cell, center, new[] { new PlacementKey(cell, side) }, side);
            return true;
        }

        PlacementKey[] keys = new PlacementKey[brick.CellsLong];
        Vector3 centerSum = Vector3.zero;
        for (int i = 0; i < brick.CellsLong; i++)
        {
            Vector2Int c = cell + GetCellOffset(i);
            keys[i] = new PlacementKey(c, HalfCellSide.None);
            centerSum += gridOverlay.CellToWorldCenter(c);
        }
        placement = new Placement(cell, centerSum / brick.CellsLong, keys, HalfCellSide.None);
        return true;
    }

    private static List<Vector2Int> GetRectangleCells(Vector2Int anchor, Vector2Int current)
    {
        int minX = Mathf.Min(anchor.x, current.x);
        int maxX = Mathf.Max(anchor.x, current.x);
        int minY = Mathf.Min(anchor.y, current.y);
        int maxY = Mathf.Max(anchor.y, current.y);

        List<Vector2Int> cells = new List<Vector2Int>((maxX - minX + 1) * (maxY - minY + 1));
        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                cells.Add(new Vector2Int(x, y));
        return cells;
    }

    private void SpawnBrick(BrickDefinition selectedBrick, GameObject template, Placement placement)
    {
        GameObject brick = Instantiate(template, placement.Center, Quaternion.identity);
        brick.name = $"{template.name}_{selectedBrick.DisplayName}_Built";
        brick.SetActive(true);
        int stackIndex = GetStackCount(placement.Keys);
        PlaceBrick(brick, selectedBrick, placement, stackIndex);

        BuildingPlacedBrick marker = brick.GetComponent<BuildingPlacedBrick>();
        if (marker == null)
        {
            marker = brick.AddComponent<BuildingPlacedBrick>();
        }

        marker.Cell = placement.AnchorCell;
        marker.StackIndex = stackIndex;
        EnsureClickableCollider(brick);
        AddBrickToStacks(brick, placement.Keys);

        // HP 부여: 스택 인덱스가 클수록 (위로 쌓을수록) 더 튼튼
        float health = brickBaseHealth + brickStackBonus * stackIndex;
        Damageable dmg = brick.GetComponent<Damageable>();
        if (dmg == null) dmg = brick.AddComponent<Damageable>();
        dmg.SetMaxHealth(health);
        dmg.OnDeath += () => HandleBrickDestroyed(brick);

        if (addHealthBarToBricks && brick.GetComponent<HealthBar>() == null)
        {
            HealthBar hb = brick.AddComponent<HealthBar>();
            // 크기와 위치를 브릭에 맞게 커스터마이즈
            hb.Configure(brickHealthBarOffset, brickHealthBarPixelSize, brickHealthBarWorldScale);
        }
    }

    private void HandleBrickDestroyed(GameObject brick)
    {
        if (brick == null) return;
        RemoveBrickFromStacks(brick);
        Destroy(brick);
    }

    private void PlaceBrick(GameObject brick, BrickDefinition definition, Placement placement, int stackIndex)
    {
        brick.transform.rotation = Quaternion.Euler(0f, GetRotationY(definition), 0f);
        brick.transform.position = placement.Center;
        FitBrickToFootprint(brick, GetTargetFootprint(definition), GetRotationY(definition));
        CenterBrickOnFootprint(brick, placement.Center);
        MoveBottomToSurface(brick, GetPlacementSurface(placement.Keys));
    }

    private static void CenterBrickOnFootprint(GameObject brick, Vector3 targetCenter)
    {
        if (!TryGetRendererBounds(brick, out Bounds bounds))
        {
            return;
        }

        Vector3 offset = bounds.center - targetCenter;
        offset.y = 0f;
        brick.transform.position -= offset;
    }

    private Vector2 GetTargetFootprint(BrickDefinition definition)
    {
        float cellSize = gridOverlay.CellSize;
        if (definition.HalfCell)
        {
            return selectedOrientation == BrickOrientation.Horizontal
                ? new Vector2(cellSize, cellSize * 0.5f)
                : new Vector2(cellSize * 0.5f, cellSize);
        }

        return selectedOrientation == BrickOrientation.Horizontal
            ? new Vector2(cellSize * definition.CellsLong, cellSize)
            : new Vector2(cellSize, cellSize * definition.CellsLong);
    }

    private float GetRotationY(BrickDefinition definition)
    {
        if (!definition.CanRotate)
        {
            return 0f;
        }

        return selectedOrientation == BrickOrientation.Horizontal ? 0f : 90f;
    }

    private BrickDefinition GetSelectedBrick()
    {
        if (brickDefinitions == null || brickDefinitions.Length == 0)
        {
            return null;
        }

        if (selectedBrickIndex < 0 || selectedBrickIndex >= brickDefinitions.Length)
        {
            return null;
        }

        return brickDefinitions[selectedBrickIndex];
    }

    private GameObject ResolveBrickTemplate(BrickDefinition definition)
    {
        if (definition != null && definition.Prefab != null)
        {
            return definition.Prefab;
        }

        if (selectedBrickIndex == 0 && brickPrefab != null)
        {
            return brickPrefab;
        }

        if (definition == null)
        {
            return null;
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

    private static void FitBrickToFootprint(GameObject brick, Vector2 footprint, float rotationY)
    {
        if (footprint.x <= 0f || footprint.y <= 0f)
        {
            return;
        }

        if (!TryGetRendererBounds(brick, out Bounds bounds))
        {
            return;
        }

        if (bounds.size.x <= Mathf.Epsilon || bounds.size.z <= Mathf.Epsilon)
        {
            return;
        }

        // footprint.x = target world X, footprint.y = target world Z
        float scaleForWorldX = footprint.x / bounds.size.x;
        float scaleForWorldZ = footprint.y / bounds.size.z;

        if (!IsValidScaleFactor(scaleForWorldX) || !IsValidScaleFactor(scaleForWorldZ))
        {
            return;
        }

        Vector3 scale = brick.transform.localScale;

        // After 90째 Y rotation: local X ??world Z, local Z ??world X (magnitudes)
        bool rotated90 = Mathf.Abs(Mathf.DeltaAngle(rotationY, 90f)) < 1f;
        if (rotated90)
        {
            scale.x *= scaleForWorldZ;
            scale.z *= scaleForWorldX;
        }
        else
        {
            scale.x *= scaleForWorldX;
            scale.z *= scaleForWorldZ;
        }

        // Y scales with the narrow dimension so height stays proportional to brick thickness
        scale.y *= Mathf.Min(scaleForWorldX, scaleForWorldZ);

        brick.transform.localScale = scale;
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        bounds = default;
        if (renderers.Length == 0)
        {
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    private static bool IsValidScaleFactor(float scaleFactor)
    {
        return scaleFactor > Mathf.Epsilon && !float.IsInfinity(scaleFactor) && !float.IsNaN(scaleFactor);
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

        BrickDefinition selectedBrick = GetSelectedBrick();
        if (selectedBrick == null || !TryGetPlacement(selectedBrick, out Placement placement))
        {
            return;
        }

        GameObject brick = FindTopBrick(placement.Keys);
        if (brick != null)
        {
            RemoveBrickFromStacks(brick);
            Destroy(brick);
        }
    }

    private GameObject FindTopBrick(PlacementKey[] keys)
    {
        GameObject topBrick = null;
        int topIndex = -1;
        foreach (PlacementKey key in keys)
        {
            if (!placedBricks.TryGetValue(key, out List<GameObject> stack) || stack.Count == 0)
            {
                continue;
            }

            int index = stack.Count - 1;
            if (index > topIndex)
            {
                topIndex = index;
                topBrick = stack[index];
            }
        }

        return topBrick;
    }

    private int GetStackCount(PlacementKey[] keys)
    {
        int stackCount = 0;
        foreach (PlacementKey key in keys)
        {
            stackCount = Mathf.Max(stackCount, CountInStack(key));

            if (key.HalfSide == HalfCellSide.None)
            {
                stackCount = Mathf.Max(stackCount, CountInStack(new PlacementKey(key.Cell, HalfCellSide.Negative)));
                stackCount = Mathf.Max(stackCount, CountInStack(new PlacementKey(key.Cell, HalfCellSide.Positive)));
            }
            else
            {
                stackCount = Mathf.Max(stackCount, CountInStack(new PlacementKey(key.Cell, HalfCellSide.None)));
            }
        }

        return stackCount;
    }

    private int CountInStack(PlacementKey key)
    {
        return placedBricks.TryGetValue(key, out List<GameObject> stack) ? stack.Count : 0;
    }

    private float GetPlacementSurface(PlacementKey[] keys)
    {
        float surface = gridOverlay.SurfaceY + brickYOffset;
        foreach (PlacementKey key in keys)
        {
            surface = Mathf.Max(surface, GetTopSurfaceForKey(key));
            if (key.HalfSide == HalfCellSide.None)
            {
                surface = Mathf.Max(surface, GetTopSurfaceForKey(new PlacementKey(key.Cell, HalfCellSide.Negative)));
                surface = Mathf.Max(surface, GetTopSurfaceForKey(new PlacementKey(key.Cell, HalfCellSide.Positive)));
            }
            else
            {
                surface = Mathf.Max(surface, GetTopSurfaceForKey(new PlacementKey(key.Cell, HalfCellSide.None)));
            }
        }
        return surface;
    }

    private float GetTopSurfaceForKey(PlacementKey key)
    {
        if (!placedBricks.TryGetValue(key, out List<GameObject> stack) || stack.Count == 0)
        {
            return gridOverlay.SurfaceY + brickYOffset;
        }

        GameObject topBrick = stack[stack.Count - 1];
        if (TryGetRendererBounds(topBrick, out Bounds bounds))
        {
            return bounds.max.y - stackOverlapY;
        }

        return gridOverlay.SurfaceY + brickYOffset;
    }

    private void AddBrickToStacks(GameObject brick, PlacementKey[] keys)
    {
        foreach (PlacementKey key in keys)
        {
            if (!placedBricks.TryGetValue(key, out List<GameObject> stack))
            {
                stack = new List<GameObject>();
                placedBricks.Add(key, stack);
            }

            stack.Add(brick);
        }
    }

    private void RemoveBrickFromStacks(GameObject brick)
    {
        List<PlacementKey> emptyKeys = new List<PlacementKey>();
        foreach (KeyValuePair<PlacementKey, List<GameObject>> pair in placedBricks)
        {
            pair.Value.RemoveAll(candidate => candidate == brick);
            if (pair.Value.Count == 0)
            {
                emptyKeys.Add(pair.Key);
            }
        }

        foreach (PlacementKey key in emptyKeys)
        {
            placedBricks.Remove(key);
        }
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

    private void RefreshPreviewBrick(bool force)
    {
        BrickDefinition selectedBrick = GetSelectedBrick();
        GameObject template = ResolveBrickTemplate(selectedBrick);
        if (!force && (template == null || template == previewTemplate))
        {
            return;
        }

        previewTemplate = template;
        if (previewBrick != null)
        {
            Destroy(previewBrick);
        }

        if (template == null)
        {
            previewBrick = null;
            return;
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

    private bool IsShiftHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
            return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
        return false;
#endif
    }

    private bool IsLeftMouseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
            return mouse.leftButton.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(0);
#else
        return false;
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

    private static bool WasNumberKeyPressed(int number)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            switch (number)
            {
                case 1: if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) return true; break;
                case 2: if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) return true; break;
                case 3: if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) return true; break;
                case 4: if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) return true; break;
                case 5: if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) return true; break;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        switch (number)
        {
            case 1: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
            case 2: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
            case 3: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
            case 4: return Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4);
            case 5: return Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5);
        }
#endif

        return false;
    }

    private static bool WasRotateKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.R);
#else
        return false;
#endif
    }
}
