using UnityEngine;

public class BunnyVisuals : MonoBehaviour
{
    const string VisualRootName = "GeneratedBunnyVisual";

    public Color furColor = new Color(0.78f, 0.72f, 0.62f, 1f);
    public Color bellyColor = new Color(0.9f, 0.84f, 0.72f, 1f);
    public Color earInnerColor = new Color(0.92f, 0.58f, 0.55f, 1f);
    public Color eyeColor = new Color(0.025f, 0.018f, 0.014f, 1f);
    public Color noseColor = new Color(0.9f, 0.42f, 0.45f, 1f);

    void Awake()
    {
        EnsureVisuals();
    }

    void OnValidate()
    {
        EnsureVisuals();
    }

    public void EnsureVisuals()
    {
        Transform root = transform.Find(VisualRootName);
        if (root == null)
        {
            GameObject rootGo = new GameObject(VisualRootName);
            rootGo.transform.SetParent(transform, false);
            root = rootGo.transform;
        }

        HideOriginalRenderers(root);

        SetPart(root, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.27f, -0.04f), new Vector3(0.34f, 0.4f, 0.48f), Quaternion.Euler(90f, 0f, 0f), furColor);
        SetPart(root, "ChestBridge", PrimitiveType.Sphere, new Vector3(0f, 0.39f, 0.17f), new Vector3(0.25f, 0.2f, 0.22f), Quaternion.identity, furColor);
        SetPart(root, "Belly", PrimitiveType.Sphere, new Vector3(0f, 0.255f, 0.04f), new Vector3(0.25f, 0.19f, 0.19f), Quaternion.identity, bellyColor);
        SetPart(root, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.49f, 0.31f), new Vector3(0.27f, 0.25f, 0.25f), Quaternion.identity, furColor);
        SetPart(root, "Muzzle", PrimitiveType.Sphere, new Vector3(0f, 0.445f, 0.49f), new Vector3(0.15f, 0.095f, 0.08f), Quaternion.identity, bellyColor);
        SetPart(root, "LeftCheek", PrimitiveType.Sphere, new Vector3(-0.075f, 0.43f, 0.455f), new Vector3(0.09f, 0.075f, 0.06f), Quaternion.identity, bellyColor);
        SetPart(root, "RightCheek", PrimitiveType.Sphere, new Vector3(0.075f, 0.43f, 0.455f), new Vector3(0.09f, 0.075f, 0.06f), Quaternion.identity, bellyColor);
        SetPart(root, "Nose", PrimitiveType.Sphere, new Vector3(0f, 0.465f, 0.565f), new Vector3(0.035f, 0.025f, 0.02f), Quaternion.identity, noseColor);
        SetPart(root, "LeftEar", PrimitiveType.Capsule, new Vector3(-0.075f, 0.665f, 0.285f), new Vector3(0.065f, 0.19f, 0.055f), Quaternion.Euler(-10f, 0f, -10f), furColor);
        SetPart(root, "RightEar", PrimitiveType.Capsule, new Vector3(0.075f, 0.665f, 0.285f), new Vector3(0.065f, 0.19f, 0.055f), Quaternion.Euler(-10f, 0f, 10f), furColor);
        SetPart(root, "LeftEarBase", PrimitiveType.Sphere, new Vector3(-0.072f, 0.575f, 0.29f), new Vector3(0.075f, 0.06f, 0.065f), Quaternion.identity, furColor);
        SetPart(root, "RightEarBase", PrimitiveType.Sphere, new Vector3(0.072f, 0.575f, 0.29f), new Vector3(0.075f, 0.06f, 0.065f), Quaternion.identity, furColor);
        SetPart(root, "LeftEarInner", PrimitiveType.Capsule, new Vector3(-0.076f, 0.672f, 0.315f), new Vector3(0.032f, 0.125f, 0.02f), Quaternion.Euler(-10f, 0f, -10f), earInnerColor);
        SetPart(root, "RightEarInner", PrimitiveType.Capsule, new Vector3(0.076f, 0.672f, 0.315f), new Vector3(0.032f, 0.125f, 0.02f), Quaternion.Euler(-10f, 0f, 10f), earInnerColor);
        SetPart(root, "Tail", PrimitiveType.Sphere, new Vector3(0f, 0.31f, -0.285f), new Vector3(0.14f, 0.14f, 0.14f), Quaternion.identity, bellyColor);
        SetPart(root, "LeftEye", PrimitiveType.Sphere, new Vector3(-0.075f, 0.525f, 0.505f), new Vector3(0.033f, 0.033f, 0.019f), Quaternion.identity, eyeColor);
        SetPart(root, "RightEye", PrimitiveType.Sphere, new Vector3(0.075f, 0.525f, 0.505f), new Vector3(0.033f, 0.033f, 0.019f), Quaternion.identity, eyeColor);
        SetPart(root, "LeftEyeShine", PrimitiveType.Sphere, new Vector3(-0.084f, 0.535f, 0.518f), new Vector3(0.011f, 0.011f, 0.008f), Quaternion.identity, Color.white);
        SetPart(root, "RightEyeShine", PrimitiveType.Sphere, new Vector3(0.066f, 0.535f, 0.518f), new Vector3(0.011f, 0.011f, 0.008f), Quaternion.identity, Color.white);
        SetPart(root, "LeftBackFoot", PrimitiveType.Capsule, new Vector3(-0.15f, 0.13f, -0.1f), new Vector3(0.075f, 0.13f, 0.065f), Quaternion.Euler(78f, 0f, -14f), furColor);
        SetPart(root, "RightBackFoot", PrimitiveType.Capsule, new Vector3(0.15f, 0.13f, -0.1f), new Vector3(0.075f, 0.13f, 0.065f), Quaternion.Euler(78f, 0f, 14f), furColor);
        SetPart(root, "LeftFrontPaw", PrimitiveType.Sphere, new Vector3(-0.105f, 0.18f, 0.23f), new Vector3(0.06f, 0.045f, 0.055f), Quaternion.identity, furColor);
        SetPart(root, "RightFrontPaw", PrimitiveType.Sphere, new Vector3(0.105f, 0.18f, 0.23f), new Vector3(0.06f, 0.045f, 0.055f), Quaternion.identity, furColor);
    }

    public void RemoveGeneratedVisualsAndRestorePrefabRenderers()
    {
        Transform root = transform.Find(VisualRootName);
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && (root == null || !renderer.transform.IsChildOf(root)))
                renderer.enabled = true;
        }

        if (root != null)
            Destroy(root.gameObject);
    }

    void HideOriginalRenderers(Transform visualRoot)
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && !renderer.transform.IsChildOf(visualRoot))
                renderer.enabled = false;
        }
    }

    void SetPart(Transform root, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Quaternion rotation, Color color)
    {
        Transform part = root.Find(name);
        if (part == null)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.transform.SetParent(root, false);
            part = go.transform;
        }

        part.localPosition = position;
        part.localRotation = rotation;
        part.localScale = scale;

        Collider col = part.GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer == null)
            return;

        renderer.enabled = true;
        Material mat = renderer.sharedMaterial;
        if (mat == null || mat.name == "Default-Material")
            mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        renderer.sharedMaterial = mat;
    }
}
