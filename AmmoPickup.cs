using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AmmoPickup : MonoBehaviour
{
    public int ammoAmount = 8;
    public bool pickupOnTrigger = false;
    public bool generateSimpleVisual = true;

    const string VisualRootName = "GeneratedAmmoVisual";

    void Reset()
    {
        ConfigurePhysics();
        EnsureVisual();
    }

    void Awake()
    {
        ConfigurePhysics();
        EnsureVisual();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!pickupOnTrigger)
            return;

        PlayerInventory inventory = other.GetComponent<PlayerInventory>() ?? other.GetComponentInParent<PlayerInventory>();
        if (inventory != null)
            TryPickupInto(inventory);
    }

    public bool TryPickupInto(PlayerInventory inventory)
    {
        GunWeapon gun = FindGun(inventory);
        if (gun == null)
            return false;

        gun.AddAmmo(ammoAmount);
        Destroy(gameObject);
        return true;
    }

    GunWeapon FindGun(PlayerInventory inventory)
    {
        if (inventory == null || inventory.weapons == null)
            return null;

        Weapon active = inventory.GetActiveWeapon();
        if (active != null)
        {
            GunWeapon activeGun = active.GetComponent<GunWeapon>();
            if (activeGun != null)
                return activeGun;
        }

        foreach (Weapon weapon in inventory.weapons)
        {
            if (weapon == null)
                continue;

            GunWeapon gun = weapon.GetComponent<GunWeapon>();
            if (gun != null)
                return gun;
        }

        return null;
    }

    void ConfigurePhysics()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = pickupOnTrigger;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.mass = 0.2f;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void EnsureVisual()
    {
        if (!generateSimpleVisual || transform.Find(VisualRootName) != null)
            return;

        GameObject root = new GameObject(VisualRootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        if (shader == null)
            return;

        Material brass = new Material(shader);
        brass.color = new Color(0.92f, 0.62f, 0.18f, 1f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "AmmoBody";
        body.transform.SetParent(root.transform, false);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(0.12f, 0.38f, 0.12f);
        Renderer bodyRenderer = body.GetComponent<Renderer>();
        if (bodyRenderer != null)
            bodyRenderer.sharedMaterial = brass;
        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
            Destroy(bodyCollider);

        GameObject tip = new GameObject("AmmoTip");
        tip.name = "AmmoTip";
        tip.transform.SetParent(root.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0f, 0.42f);
        tip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        tip.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f);
        MeshFilter tipFilter = tip.AddComponent<MeshFilter>();
        tipFilter.sharedMesh = CreateConeMesh(16);
        MeshRenderer tipRenderer = tip.AddComponent<MeshRenderer>();
        if (tipRenderer != null)
            tipRenderer.sharedMaterial = brass;
    }

    Mesh CreateConeMesh(int segments)
    {
        segments = Mathf.Max(6, segments);
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 6];

        vertices[0] = Vector3.up;
        vertices[1] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            vertices[i + 2] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[t++] = 0;
            triangles[t++] = i + 2;
            triangles[t++] = next + 2;

            triangles[t++] = 1;
            triangles[t++] = next + 2;
            triangles[t++] = i + 2;
        }

        Mesh mesh = new Mesh();
        mesh.name = "RuntimeAmmoCone";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }
}
