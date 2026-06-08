using UnityEngine;

public enum CraftableItemKind
{
    WoodenBarrier,
    FarmPlot,
    RecipeBook,
    Torch,
    CraftingTableLevel2
}

public class CraftedItemPrefabCatalog : MonoBehaviour
{
    public GameObject woodenBarrierPrefab;
    public GameObject farmPlotPrefab;
    public GameObject recipeBookPrefab;
    public GameObject torchPrefab;
    public GameObject craftingTableLevel2Prefab;

    static CraftedItemPrefabCatalog _instance;

    public static CraftedItemPrefabCatalog Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CraftedItemPrefabCatalog>();
                if (_instance == null)
                    _instance = new GameObject("CraftedItemPrefabCatalog").AddComponent<CraftedItemPrefabCatalog>();
            }

            return _instance;
        }
    }

    public GameObject Spawn(CraftableItemKind kind, Vector3 position, Quaternion rotation)
    {
        GameObject prefab = GetAssignedPrefab(kind);
        if (prefab != null)
            return Instantiate(prefab, position, rotation);

        GameObject fallback = CreateFallback(kind);
        fallback.transform.position = position;
        fallback.transform.rotation = rotation;
        return fallback;
    }

    GameObject GetAssignedPrefab(CraftableItemKind kind)
    {
        switch (kind)
        {
            case CraftableItemKind.WoodenBarrier:
                return woodenBarrierPrefab;
            case CraftableItemKind.FarmPlot:
                return farmPlotPrefab;
            case CraftableItemKind.RecipeBook:
                return recipeBookPrefab;
            case CraftableItemKind.Torch:
                return torchPrefab;
            case CraftableItemKind.CraftingTableLevel2:
                return craftingTableLevel2Prefab;
            default:
                return null;
        }
    }

    GameObject CreateFallback(CraftableItemKind kind)
    {
        GameObject root = new GameObject(kind.ToString());

        switch (kind)
        {
            case CraftableItemKind.WoodenBarrier:
                AddCube(root.transform, "LeftPost", new Vector3(-0.55f, 0.55f, 0f), new Vector3(0.14f, 1.1f, 0.16f), WoodDark());
                AddCube(root.transform, "RightPost", new Vector3(0.55f, 0.55f, 0f), new Vector3(0.14f, 1.1f, 0.16f), WoodDark());
                AddCube(root.transform, "TopRail", new Vector3(0f, 0.78f, 0f), new Vector3(1.35f, 0.16f, 0.18f), WoodMid());
                AddCube(root.transform, "BottomRail", new Vector3(0f, 0.35f, 0f), new Vector3(1.35f, 0.16f, 0.18f), WoodMid());
                break;
            case CraftableItemKind.FarmPlot:
                AddCube(root.transform, "Soil", new Vector3(0f, 0.04f, 0f), new Vector3(1.7f, 0.08f, 1.25f), new Color(0.18f, 0.09f, 0.035f, 1f));
                AddCube(root.transform, "WoodFrameA", new Vector3(0f, 0.12f, 0.68f), new Vector3(1.8f, 0.14f, 0.12f), WoodMid());
                AddCube(root.transform, "WoodFrameB", new Vector3(0f, 0.12f, -0.68f), new Vector3(1.8f, 0.14f, 0.12f), WoodMid());
                AddCube(root.transform, "WoodFrameC", new Vector3(0.92f, 0.12f, 0f), new Vector3(0.12f, 0.14f, 1.25f), WoodMid());
                AddCube(root.transform, "WoodFrameD", new Vector3(-0.92f, 0.12f, 0f), new Vector3(0.12f, 0.14f, 1.25f), WoodMid());
                break;
            case CraftableItemKind.RecipeBook:
                AddCube(root.transform, "Book", new Vector3(0f, 0.18f, 0f), new Vector3(0.55f, 0.12f, 0.75f), new Color(0.35f, 0.06f, 0.045f, 1f));
                AddCube(root.transform, "Pages", new Vector3(0.08f, 0.25f, 0f), new Vector3(0.42f, 0.04f, 0.64f), new Color(0.82f, 0.72f, 0.52f, 1f));
                break;
            case CraftableItemKind.Torch:
                AddCylinder(root.transform, "Handle", new Vector3(0f, 0.45f, 0f), new Vector3(0.12f, 0.75f, 0.12f), WoodDark());
                AddCube(root.transform, "Wrap", new Vector3(0f, 0.92f, 0f), new Vector3(0.24f, 0.16f, 0.24f), new Color(0.2f, 0.11f, 0.055f, 1f));
                AddSphere(root.transform, "Flame", new Vector3(0f, 1.15f, 0f), new Vector3(0.28f, 0.42f, 0.28f), new Color(1f, 0.42f, 0.06f, 1f));
                break;
            case CraftableItemKind.CraftingTableLevel2:
                AddCube(root.transform, "Top", new Vector3(0f, 0.72f, 0f), new Vector3(1.2f, 0.18f, 1f), WoodMid());
                AddCube(root.transform, "Body", new Vector3(0f, 0.43f, 0f), new Vector3(0.9f, 0.4f, 0.75f), WoodDark());
                AddCube(root.transform, "MetalTrim", new Vector3(0f, 0.84f, 0f), new Vector3(1.26f, 0.05f, 1.05f), new Color(0.55f, 0.5f, 0.42f, 1f));
                break;
        }

        foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            collider.enabled = true;

        return root;
    }

    void AddCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ConfigurePrimitive(go, parent, name, localPosition, localScale, color);
    }

    void AddCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ConfigurePrimitive(go, parent, name, localPosition, localScale, color);
    }

    void AddSphere(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ConfigurePrimitive(go, parent, name, localPosition, localScale, color);
    }

    void ConfigurePrimitive(GameObject go, Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = CreateMaterial(color);
            if (material != null)
                renderer.sharedMaterial = material;
        }
    }

    Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Standard")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Diffuse")
            ?? Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    Color WoodMid() => new Color(0.48f, 0.28f, 0.12f, 1f);
    Color WoodDark() => new Color(0.24f, 0.13f, 0.065f, 1f);
}
