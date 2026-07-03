using UnityEngine;

[ExecuteAlways]
public sealed class SuburbanExteriorBackdrop : MonoBehaviour
{
    private const string GeneratedRootName = "Generated Suburban Exterior";

    [SerializeField] private bool buildInEditMode = true;
    [SerializeField] private bool preserveExistingChildren = true;
    [SerializeField] private Vector3 roomCenter = new Vector3(3f, 0f, 1.5f);
    [SerializeField] private Vector2 yardSize = new Vector2(34f, 28f);
    [SerializeField] private float yardY = -0.08f;

    private Material skyMaterial;
    private Material grassMaterial;
    private Material roadMaterial;
    private Material pathMaterial;
    private Material houseBlueMaterial;
    private Material houseCreamMaterial;
    private Material roofMaterial;
    private Material treeLeafMaterial;
    private Material treeTrunkMaterial;
    private Material fenceMaterial;

    private void OnEnable()
    {
        if (Application.isPlaying || buildInEditMode)
        {
            EnsureBackdrop();
        }
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled && buildInEditMode)
        {
            EnsureBackdrop();
        }
    }

    [ContextMenu("Rebuild Exterior Backdrop")]
    private void RebuildBackdrop()
    {
        ClearGeneratedRoot();
        BuildBackdrop();
    }

    private void EnsureBackdrop()
    {
        Transform generated = transform.Find(GeneratedRootName);
        if (generated != null && preserveExistingChildren)
        {
            return;
        }

        if (generated == null)
        {
            BuildBackdrop();
        }
    }

    private void BuildBackdrop()
    {
        EnsureMaterials();

        GameObject root = new GameObject(GeneratedRootName);
        root.transform.SetParent(transform, false);

        CreateCube(root.transform, "Soft Blue Day Sky Backdrop", roomCenter + new Vector3(0f, 4.2f, 13.5f), new Vector3(38f, 8f, 0.35f), skyMaterial);
        CreateCube(root.transform, "Left Day Sky Backdrop", roomCenter + new Vector3(-17.5f, 4.2f, 1.5f), new Vector3(0.35f, 8f, 27f), skyMaterial);
        CreateCube(root.transform, "Right Day Sky Backdrop", roomCenter + new Vector3(17.5f, 4.2f, 1.5f), new Vector3(0.35f, 8f, 27f), skyMaterial);

        CreateCube(root.transform, "Suburban Grass Yard", roomCenter + new Vector3(0f, yardY, 5f), new Vector3(yardSize.x, 0.12f, yardSize.y), grassMaterial);
        CreateCube(root.transform, "Quiet Neighborhood Road", roomCenter + new Vector3(0f, yardY + 0.02f, 16.5f), new Vector3(34f, 0.04f, 2.8f), roadMaterial);
        CreateCube(root.transform, "Pale Sidewalk Strip", roomCenter + new Vector3(0f, yardY + 0.04f, 14.4f), new Vector3(34f, 0.04f, 0.42f), pathMaterial);

        CreateHouse(root.transform, "Blue Suburban House", roomCenter + new Vector3(-8.5f, 0f, 12.4f), houseBlueMaterial);
        CreateHouse(root.transform, "Cream Suburban House", roomCenter + new Vector3(6.8f, 0f, 13.2f), houseCreamMaterial);

        CreateTree(root.transform, "Maple Tree Left", roomCenter + new Vector3(-13f, 0f, 9.4f), 1.25f);
        CreateTree(root.transform, "Maple Tree Back", roomCenter + new Vector3(-2.5f, 0f, 14.2f), 1.05f);
        CreateTree(root.transform, "Maple Tree Right", roomCenter + new Vector3(12f, 0f, 10.5f), 1.2f);

        for (int i = 0; i < 12; i++)
        {
            float x = -14.5f + i * 2.6f;
            CreateCube(root.transform, $"White Fence Picket {i + 1}", roomCenter + new Vector3(x, 0.62f, 8.6f), new Vector3(0.18f, 1.15f, 0.16f), fenceMaterial);
        }

        CreateCube(root.transform, "White Fence Rail Top", roomCenter + new Vector3(-0.2f, 0.86f, 8.6f), new Vector3(30f, 0.14f, 0.12f), fenceMaterial);
        CreateCube(root.transform, "White Fence Rail Bottom", roomCenter + new Vector3(-0.2f, 0.42f, 8.6f), new Vector3(30f, 0.12f, 0.12f), fenceMaterial);
    }

    private void CreateHouse(Transform parent, string name, Vector3 position, Material wallMaterial)
    {
        Transform house = new GameObject(name).transform;
        house.SetParent(parent, false);
        house.position = position;

        CreateCube(house, "House Body", new Vector3(0f, 0.85f, 0f), new Vector3(4.6f, 1.7f, 2.8f), wallMaterial);
        CreateRoof(house, "House Roof", new Vector3(0f, 1.9f, 0f), new Vector3(5.2f, 1.05f, 3.25f), roofMaterial);
        CreateCube(house, "Front Door", new Vector3(-1.35f, 0.55f, -1.43f), new Vector3(0.55f, 1.1f, 0.08f), roadMaterial);
        CreateCube(house, "Window Left", new Vector3(0.35f, 0.9f, -1.44f), new Vector3(0.58f, 0.54f, 0.08f), skyMaterial);
        CreateCube(house, "Window Right", new Vector3(1.25f, 0.9f, -1.44f), new Vector3(0.58f, 0.54f, 0.08f), skyMaterial);
    }

    private void CreateTree(Transform parent, string name, Vector3 position, float scale)
    {
        Transform tree = new GameObject(name).transform;
        tree.SetParent(parent, false);
        tree.position = position;

        CreateCube(tree, "Tree Trunk", new Vector3(0f, 0.62f * scale, 0f), new Vector3(0.26f * scale, 1.25f * scale, 0.26f * scale), treeTrunkMaterial);
        CreateSphere(tree, "Tree Crown Main", new Vector3(0f, 1.55f * scale, 0f), Vector3.one * (1.25f * scale), treeLeafMaterial);
        CreateSphere(tree, "Tree Crown Left", new Vector3(-0.55f * scale, 1.35f * scale, -0.05f), Vector3.one * (0.9f * scale), treeLeafMaterial);
        CreateSphere(tree, "Tree Crown Right", new Vector3(0.55f * scale, 1.35f * scale, 0.08f), Vector3.one * (0.85f * scale), treeLeafMaterial);
    }

    private void CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        SetMaterialAndRemoveCollider(cube, material);
    }

    private void CreateSphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(parent, false);
        sphere.transform.localPosition = position;
        sphere.transform.localScale = scale;
        SetMaterialAndRemoveCollider(sphere, material);
    }

    private void CreateRoof(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject roof = new GameObject(name);
        roof.transform.SetParent(parent, false);
        roof.transform.localPosition = position;
        roof.transform.localScale = scale;

        MeshFilter meshFilter = roof.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = roof.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;

        Mesh mesh = new Mesh { name = "Suburban Roof Prism" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0f, 0.5f, 0.5f)
        };
        mesh.triangles = new[]
        {
            0, 2, 1,
            3, 4, 5,
            0, 3, 5, 0, 5, 2,
            1, 2, 5, 1, 5, 4,
            0, 1, 4, 0, 4, 3
        };
        mesh.RecalculateNormals();
        meshFilter.sharedMesh = mesh;
    }

    private void SetMaterialAndRemoveCollider(GameObject target, Material material)
    {
        MeshRenderer renderer = target.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }
    }

    private void EnsureMaterials()
    {
        skyMaterial = CreateMaterial("Exterior Sky Soft Blue", new Color(0.67f, 0.84f, 0.92f, 1f));
        grassMaterial = CreateMaterial("Exterior Grass", new Color(0.42f, 0.67f, 0.35f, 1f));
        roadMaterial = CreateMaterial("Exterior Warm Asphalt", new Color(0.32f, 0.33f, 0.35f, 1f));
        pathMaterial = CreateMaterial("Exterior Pale Sidewalk", new Color(0.76f, 0.73f, 0.67f, 1f));
        houseBlueMaterial = CreateMaterial("Exterior House Powder Blue", new Color(0.56f, 0.72f, 0.78f, 1f));
        houseCreamMaterial = CreateMaterial("Exterior House Cream", new Color(0.84f, 0.77f, 0.62f, 1f));
        roofMaterial = CreateMaterial("Exterior Roof Brick Red", new Color(0.48f, 0.18f, 0.14f, 1f));
        treeLeafMaterial = CreateMaterial("Exterior Tree Leaves", new Color(0.27f, 0.55f, 0.25f, 1f));
        treeTrunkMaterial = CreateMaterial("Exterior Tree Trunk", new Color(0.45f, 0.28f, 0.17f, 1f));
        fenceMaterial = CreateMaterial("Exterior White Fence", new Color(0.92f, 0.9f, 0.84f, 1f));
    }

    private static Material CreateMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
            color = color
        };
        return material;
    }

    private void ClearGeneratedRoot()
    {
        Transform generated = transform.Find(GeneratedRootName);
        if (generated == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(generated.gameObject);
        }
        else
        {
            DestroyImmediate(generated.gameObject);
        }
    }
}
