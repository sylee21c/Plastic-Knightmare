using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[ExecuteAlways]
public sealed class BuildingGridOverlay : MonoBehaviour
{
    [SerializeField] private string floorNamePrefix = "SM_Walls_Floor";
    [SerializeField, Min(1)] private int cellsPerFloorSide = 4;
    [SerializeField, Min(0.001f)] private float lineWidth = 0.025f;
    [SerializeField] private float yOffset = 0.018f;
    [SerializeField] private Color lineColor = new Color(0.08f, 0.18f, 0.22f, 0.38f);

    private const string GridMeshName = "Generated Building Grid";

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh gridMesh;
    private Material gridMaterial;
    private readonly HashSet<Vector2Int> floorCells = new HashSet<Vector2Int>();

    public float CellSize { get; private set; }
    public Vector2 GridOrigin { get; private set; }
    public float SurfaceY { get; private set; }

    private void OnEnable()
    {
        EnsureComponents();
        RebuildGrid();
    }

    private void OnValidate()
    {
        cellsPerFloorSide = Mathf.Max(1, cellsPerFloorSide);
        lineWidth = Mathf.Max(0.001f, lineWidth);

        if (isActiveAndEnabled)
        {
            EnsureComponents();
            RebuildGrid();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            RebuildGrid();
        }
    }

    public Vector3 SnapToCellCenter(Vector3 worldPosition)
    {
        if (CellSize <= 0f)
        {
            return worldPosition;
        }

        Vector2Int cell = WorldToCell(worldPosition);
        Vector3 center = CellToWorldCenter(cell);
        center.y = worldPosition.y;
        return center;
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        if (CellSize <= 0f)
        {
            return Vector2Int.zero;
        }

        return new Vector2Int(
            Mathf.FloorToInt((worldPosition.x - GridOrigin.x) / CellSize),
            Mathf.FloorToInt((worldPosition.z - GridOrigin.y) / CellSize));
    }

    public Vector3 CellToWorldCenter(Vector2Int cell)
    {
        if (CellSize <= 0f)
        {
            return Vector3.zero;
        }

        return new Vector3(
            GridOrigin.x + (cell.x + 0.5f) * CellSize,
            SurfaceY,
            GridOrigin.y + (cell.y + 0.5f) * CellSize);
    }

    public bool IsCellOnFloor(Vector2Int cell)
    {
        return floorCells.Contains(cell);
    }

    public void RebuildGrid()
    {
        EnsureComponents();

        List<Bounds> floorBounds = CollectFloorBounds();
        if (floorBounds.Count == 0)
        {
            ClearGrid();
            return;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        HashSet<LineKey> lineKeys = new HashSet<LineKey>();
        floorCells.Clear();

        float minX = float.PositiveInfinity;
        float minZ = float.PositiveInfinity;
        float surfaceY = 0f;
        foreach (Bounds bounds in floorBounds)
        {
            minX = Mathf.Min(minX, bounds.min.x);
            minZ = Mathf.Min(minZ, bounds.min.z);
            surfaceY = Mathf.Max(surfaceY, bounds.max.y);
        }

        GridOrigin = new Vector2(minX, minZ);
        SurfaceY = surfaceY + yOffset;

        float totalCellSize = 0f;
        int validFloorCount = 0;
        foreach (Bounds bounds in floorBounds)
        {
            float tileSize = Mathf.Min(bounds.size.x, bounds.size.z);
            if (tileSize <= Mathf.Epsilon)
            {
                continue;
            }

            float cellSize = tileSize / cellsPerFloorSide;
            totalCellSize += cellSize;
            validFloorCount++;

            float y = bounds.max.y + yOffset;
            Vector2Int minCell = new Vector2Int(
                Mathf.FloorToInt((bounds.min.x - GridOrigin.x) / cellSize),
                Mathf.FloorToInt((bounds.min.z - GridOrigin.y) / cellSize));

            for (int i = 0; i <= cellsPerFloorSide; i++)
            {
                float x = bounds.min.x + cellSize * i;
                float z = bounds.min.z + cellSize * i;

                AddLineIfNew(vertices, triangles, lineKeys, new Vector3(x, y, bounds.min.z), new Vector3(x, y, bounds.max.z));
                AddLineIfNew(vertices, triangles, lineKeys, new Vector3(bounds.min.x, y, z), new Vector3(bounds.max.x, y, z));
            }

            for (int x = 0; x < cellsPerFloorSide; x++)
            {
                for (int z = 0; z < cellsPerFloorSide; z++)
                {
                    floorCells.Add(new Vector2Int(minCell.x + x, minCell.y + z));
                }
            }
        }

        CellSize = validFloorCount > 0 ? totalCellSize / validFloorCount : 0f;

        gridMesh.Clear();
        gridMesh.SetVertices(vertices);
        gridMesh.SetTriangles(triangles, 0);
        gridMesh.RecalculateBounds();
    }

    private List<Bounds> CollectFloorBounds()
    {
        List<Bounds> floorBounds = new List<Bounds>();
        Scene scene = gameObject.scene;
        if (!scene.IsValid())
        {
            scene = SceneManager.GetActiveScene();
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            CollectFloorBounds(root.transform, floorBounds);
        }

        return floorBounds;
    }

    private void CollectFloorBounds(Transform candidate, List<Bounds> floorBounds)
    {
        if (candidate == transform || candidate.IsChildOf(transform))
        {
            return;
        }

        if (candidate.name.StartsWith(floorNamePrefix, System.StringComparison.Ordinal))
        {
            if (TryGetCombinedRendererBounds(candidate, out Bounds bounds))
            {
                floorBounds.Add(bounds);
            }

            return;
        }

        for (int i = 0; i < candidate.childCount; i++)
        {
            CollectFloorBounds(candidate.GetChild(i), floorBounds);
        }
    }

    private static bool TryGetCombinedRendererBounds(Transform root, out Bounds combinedBounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        combinedBounds = default;
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void AddLineIfNew(
        List<Vector3> vertices,
        List<int> triangles,
        HashSet<LineKey> lineKeys,
        Vector3 start,
        Vector3 end)
    {
        LineKey key = new LineKey(start, end);
        if (lineKeys.Add(key))
        {
            AddLine(vertices, triangles, start, end);
        }
    }

    private void AddLine(List<Vector3> vertices, List<int> triangles, Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Vector3 perpendicular = Vector3.Cross(direction.normalized, Vector3.up) * (lineWidth * 0.5f);
        int firstVertex = vertices.Count;

        vertices.Add(start - perpendicular);
        vertices.Add(start + perpendicular);
        vertices.Add(end + perpendicular);
        vertices.Add(end - perpendicular);

        triangles.Add(firstVertex);
        triangles.Add(firstVertex + 1);
        triangles.Add(firstVertex + 2);
        triangles.Add(firstVertex);
        triangles.Add(firstVertex + 2);
        triangles.Add(firstVertex + 3);
    }

    private void EnsureComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        if (gridMesh == null)
        {
            gridMesh = new Mesh { name = GridMeshName };
        }

        meshFilter.sharedMesh = gridMesh;

        if (gridMaterial == null)
        {
            gridMaterial = new Material(FindGridShader())
            {
                name = "Building Grid Overlay Material",
                color = lineColor
            };
            gridMaterial.hideFlags = HideFlags.DontSave;
            ConfigureMaterial(gridMaterial);
        }

        gridMaterial.color = lineColor;
        meshRenderer.sharedMaterial = gridMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private static Shader FindGridShader()
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

    private static void ConfigureMaterial(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void ClearGrid()
    {
        CellSize = 0f;
        GridOrigin = Vector2.zero;
        SurfaceY = 0f;
        floorCells.Clear();
        if (gridMesh != null)
        {
            gridMesh.Clear();
        }
    }

    private readonly struct LineKey
    {
        private const float Precision = 1000f;

        private readonly int ax;
        private readonly int ay;
        private readonly int az;
        private readonly int bx;
        private readonly int by;
        private readonly int bz;

        public LineKey(Vector3 a, Vector3 b)
        {
            int aX = Mathf.RoundToInt(a.x * Precision);
            int aY = Mathf.RoundToInt(a.y * Precision);
            int aZ = Mathf.RoundToInt(a.z * Precision);
            int bX = Mathf.RoundToInt(b.x * Precision);
            int bY = Mathf.RoundToInt(b.y * Precision);
            int bZ = Mathf.RoundToInt(b.z * Precision);

            bool swap = aX > bX || (aX == bX && (aY > bY || (aY == bY && aZ > bZ)));
            ax = swap ? bX : aX;
            ay = swap ? bY : aY;
            az = swap ? bZ : aZ;
            bx = swap ? aX : bX;
            by = swap ? aY : bY;
            bz = swap ? aZ : bZ;
        }
    }
}
