using UnityEngine;

[RequireComponent(typeof(Weapon))]
public class FlashlightItem : MonoBehaviour
{
    [Header("Flashlight Beam")]
    [Tooltip("How far the flashlight beam reaches.")]
    public float lightRange = 28f;
    [Tooltip("How bright the flashlight beam is.")]
    public float lightIntensity = 4.5f;
    [Tooltip("How wide the flashlight beam is.")]
    public float spotAngle = 58f;
    public Color lightColor = new Color(1f, 0.93f, 0.78f, 1f);

    [Header("Hand Pose")]
    public Vector3 holdLocalPosition = new Vector3(0.36f, -0.3f, 0.9f);
    public Vector3 holdLocalEuler = new Vector3(8f, 0f, 0f);

    [Header("Model")]
    public Vector3 bodyScale = new Vector3(0.085f, 0.42f, 0.085f);
    public Vector3 headScale = new Vector3(0.135f, 0.135f, 0.135f);
    public Color bodyColor = new Color(0.08f, 0.085f, 0.09f, 1f);
    public Color lensColor = new Color(1f, 0.92f, 0.55f, 1f);

    Light _beam;
    Renderer[] _renderers;
    bool _isOn;
    static Sprite _generatedIcon;

    public bool IsOn => _isOn;

    void Awake()
    {
        ConfigureWeapon();
        EnsureInventoryIcon();
        EnsureVisuals();
        ApplyLightSettings();
        SetOn(false);
    }

    void OnValidate()
    {
        ConfigureWeapon();
        ApplyLightSettings();
        ApplyVisualColors();
    }

    void ConfigureWeapon()
    {
        Weapon weapon = GetComponent<Weapon>();
        if (weapon == null)
            return;

        weapon.weaponName = "Flashlight";
        weapon.damage = 0f;
        weapon.range = lightRange;
        weapon.isMelee = false;
        weapon.holdLocalPosition = holdLocalPosition;
        weapon.holdLocalEuler = holdLocalEuler;
    }

    void EnsureInventoryIcon()
    {
        Weapon weapon = GetComponent<Weapon>();
        if (weapon == null || weapon.icon != null)
            return;

        if (_generatedIcon == null)
            _generatedIcon = CreateInventoryIcon();

        weapon.icon = _generatedIcon;
    }

    static Sprite CreateInventoryIcon()
    {
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.name = "GeneratedFlashlightIcon";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        DrawSoftBeam(texture);
        DrawFlashlightBody(texture);

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 64f);
        sprite.name = "Flashlight Icon";
        return sprite;
    }

    static void DrawSoftBeam(Texture2D texture)
    {
        Color warmBeam = new Color(1f, 0.82f, 0.32f, 0.34f);
        Color brightCore = new Color(1f, 0.95f, 0.62f, 0.24f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float nx = (x + 0.5f) / texture.width;
                float ny = (y + 0.5f) / texture.height;
                if (nx < 0.55f)
                    continue;

                float t = Mathf.InverseLerp(0.55f, 1f, nx);
                float centerY = Mathf.Lerp(0.49f, 0.58f, t);
                float halfHeight = Mathf.Lerp(0.08f, 0.32f, t);
                float distance = Mathf.Abs(ny - centerY);
                if (distance > halfHeight)
                    continue;

                float edgeFade = 1f - distance / halfHeight;
                float alpha = Mathf.Pow(edgeFade, 1.8f) * (1f - t * 0.55f);
                BlendPixel(texture, x, y, warmBeam, alpha * warmBeam.a);

                if (distance < halfHeight * 0.28f)
                    BlendPixel(texture, x, y, brightCore, alpha * brightCore.a);
            }
        }
    }

    static void DrawFlashlightBody(Texture2D texture)
    {
        Color shadow = new Color(0.015f, 0.017f, 0.02f, 0.95f);
        Color metalDark = new Color(0.055f, 0.06f, 0.068f, 1f);
        Color metalMid = new Color(0.13f, 0.145f, 0.16f, 1f);
        Color metalHighlight = new Color(0.42f, 0.45f, 0.48f, 1f);
        Color grip = new Color(0.025f, 0.028f, 0.032f, 1f);
        Color lensOuter = new Color(0.9f, 0.68f, 0.22f, 1f);
        Color lensInner = new Color(1f, 0.95f, 0.58f, 1f);
        Color lensSpark = new Color(1f, 1f, 0.9f, 1f);

        float angle = -22f * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        Vector2 center = new Vector2(0.42f, 0.43f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float nx = (x + 0.5f) / texture.width;
                float ny = (y + 0.5f) / texture.height;
                Vector2 p = new Vector2(nx - center.x, ny - center.y);
                float u = p.x * cos + p.y * sin;
                float v = -p.x * sin + p.y * cos;

                if (u > -0.36f && u < 0.14f && Mathf.Abs(v) < 0.075f)
                {
                    float shade = Mathf.InverseLerp(-0.075f, 0.075f, v);
                    Color color = Color.Lerp(metalDark, metalMid, shade);
                    if (v > 0.044f)
                        color = Color.Lerp(color, metalHighlight, 0.55f);
                    if (u < -0.26f || Mathf.Abs(Mathf.Sin((u + 0.18f) * 95f)) > 0.86f)
                        color = Color.Lerp(color, grip, 0.68f);

                    BlendPixel(texture, x, y, color, 1f);
                }

                if (u > -0.39f && u <= -0.34f && Mathf.Abs(v) < 0.09f)
                    BlendPixel(texture, x, y, Color.Lerp(shadow, metalDark, 0.35f), 1f);

                if (u > 0.09f && u < 0.31f && Mathf.Abs(v) < 0.12f)
                {
                    float round = 1f - Mathf.Abs(v) / 0.12f;
                    Color color = Color.Lerp(metalDark, metalMid, round);
                    if (v > 0.055f)
                        color = Color.Lerp(color, metalHighlight, 0.5f);
                    BlendPixel(texture, x, y, color, 1f);
                }

                if (u > 0.26f && u < 0.36f && Mathf.Abs(v) < 0.105f)
                {
                    float lensRound = 1f - Mathf.Abs(v) / 0.105f;
                    Color color = Color.Lerp(lensOuter, lensInner, lensRound);
                    BlendPixel(texture, x, y, color, 1f);
                }

                if (u > 0.22f && u < 0.31f && v > 0.095f && v < 0.13f)
                    BlendPixel(texture, x, y, metalHighlight, 0.85f);

                if (u > -0.05f && u < 0.04f && v > 0.07f && v < 0.12f)
                    BlendPixel(texture, x, y, metalHighlight, 0.9f);

                float sparkDx = u - 0.31f;
                float sparkDy = v - 0.035f;
                if (sparkDx * sparkDx + sparkDy * sparkDy < 0.0014f)
                    BlendPixel(texture, x, y, lensSpark, 1f);
            }
        }
    }

    static void BlendPixel(Texture2D texture, int x, int y, Color color, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        if (alpha <= 0f)
            return;

        Color existing = texture.GetPixel(x, y);
        float outAlpha = alpha + existing.a * (1f - alpha);
        if (outAlpha <= 0f)
        {
            texture.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
            return;
        }

        float existingFactor = existing.a * (1f - alpha);
        Color blended = new Color(
            (color.r * alpha + existing.r * existingFactor) / outAlpha,
            (color.g * alpha + existing.g * existingFactor) / outAlpha,
            (color.b * alpha + existing.b * existingFactor) / outAlpha,
            outAlpha);
        blended.a = outAlpha;
        texture.SetPixel(x, y, blended);
    }

    public void Toggle()
    {
        SetOn(!_isOn);
    }

    public void SetOn(bool value)
    {
        _isOn = value;
        if (_beam != null)
            _beam.enabled = _isOn;
    }

    public void ApplyLightSettings()
    {
        if (_beam == null)
            _beam = GetComponentInChildren<Light>(true);

        if (_beam == null)
            return;

        _beam.type = LightType.Spot;
        _beam.range = Mathf.Max(1f, lightRange);
        _beam.intensity = Mathf.Max(0f, lightIntensity);
        _beam.spotAngle = Mathf.Clamp(spotAngle, 1f, 179f);
        _beam.color = lightColor;
        _beam.shadows = LightShadows.Soft;
        _beam.enabled = _isOn;
    }

    void EnsureVisuals()
    {
        EnsureCylinderPart("Body", new Vector3(0f, 0f, -0.065f), bodyScale);
        EnsureCylinderPart("Head", new Vector3(0f, 0f, 0.205f), headScale);
        EnsureCylinderPart("TailCap", new Vector3(0f, 0f, -0.31f), new Vector3(0.094f, 0.05f, 0.094f));
        EnsureCylinderPart("LensRim", new Vector3(0f, 0f, 0.292f), new Vector3(0.145f, 0.038f, 0.145f));
        EnsureCylinderPart("GripRing1", new Vector3(0f, 0f, -0.24f), new Vector3(0.092f, 0.015f, 0.092f));
        EnsureCylinderPart("GripRing2", new Vector3(0f, 0f, -0.18f), new Vector3(0.092f, 0.015f, 0.092f));
        EnsureCylinderPart("GripRing3", new Vector3(0f, 0f, -0.12f), new Vector3(0.092f, 0.015f, 0.092f));
        EnsureCylinderPart("GripRing4", new Vector3(0f, 0f, -0.06f), new Vector3(0.092f, 0.015f, 0.092f));
        EnsureCubePart("PowerSwitch", new Vector3(0f, 0.092f, 0.055f), new Vector3(0.062f, 0.018f, 0.09f));
        EnsureCubePart("PocketClip", new Vector3(0.089f, 0.008f, -0.1f), new Vector3(0.014f, 0.014f, 0.28f));
        EnsureCubePart("PocketClipEnd", new Vector3(0.089f, 0.02f, -0.255f), new Vector3(0.014f, 0.028f, 0.038f));
        EnsureCylinderPart("Lens", new Vector3(0f, 0f, 0.318f), new Vector3(0.104f, 0.01f, 0.104f));

        Transform beamTransform = transform.Find("Beam");
        if (beamTransform == null)
        {
            GameObject beam = new GameObject("Beam");
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = new Vector3(0f, 0f, 0.33f);
            beam.transform.localRotation = Quaternion.identity;
            _beam = beam.AddComponent<Light>();
        }
        else
        {
            beamTransform.localPosition = new Vector3(0f, 0f, 0.33f);
            beamTransform.localRotation = Quaternion.identity;
            _beam = beamTransform.GetComponent<Light>();
            if (_beam == null)
                _beam = beamTransform.gameObject.AddComponent<Light>();
        }

        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        ApplyVisualColors();
    }

    void EnsureCylinderPart(string partName, Vector3 localPosition, Vector3 localScale)
    {
        Transform existing = transform.Find(partName);
        GameObject part = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        part.name = partName;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        part.transform.localScale = localScale;
    }

    void EnsureCubePart(string partName, Vector3 localPosition, Vector3 localScale)
    {
        Transform existing = transform.Find(partName);
        GameObject part = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;
    }

    void ApplyVisualColors()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in _renderers)
        {
            if (renderer == null)
                continue;

            Material mat = renderer.sharedMaterial;
            if (mat == null || mat.name == "Default-Material")
                mat = new Material(Shader.Find("Standard"));

            renderer.sharedMaterial = mat;
            ApplyPartMaterial(renderer.gameObject.name, mat);
        }
    }

    void ApplyPartMaterial(string partName, Material mat)
    {
        Color darkGrip = new Color(0.025f, 0.027f, 0.03f, 1f);
        Color rimColor = new Color(0.18f, 0.19f, 0.2f, 1f);
        Color switchColor = new Color(0.015f, 0.015f, 0.016f, 1f);
        Color clipColor = new Color(0.32f, 0.34f, 0.36f, 1f);

        if (partName == "Lens")
        {
            mat.color = lensColor;
            mat.SetColor("_EmissionColor", lensColor * 0.45f);
            mat.EnableKeyword("_EMISSION");
            return;
        }

        mat.DisableKeyword("_EMISSION");

        if (partName.StartsWith("GripRing", System.StringComparison.Ordinal) || partName == "TailCap")
            mat.color = darkGrip;
        else if (partName == "LensRim" || partName == "Head")
            mat.color = rimColor;
        else if (partName == "PowerSwitch")
            mat.color = switchColor;
        else if (partName.StartsWith("PocketClip", System.StringComparison.Ordinal))
            mat.color = clipColor;
        else
            mat.color = bodyColor;

        mat.SetFloat("_Glossiness", partName.StartsWith("PocketClip", System.StringComparison.Ordinal) ? 0.55f : 0.35f);
    }

    public static GameObject CreateRuntimeObject()
    {
        GameObject go = new GameObject("Flashlight");
        go.AddComponent<Weapon>();
        go.AddComponent<FlashlightItem>();
        return go;
    }
}
