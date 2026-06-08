using UnityEngine;

public class Weapon : MonoBehaviour
{
    [Header("Weapon Data")]
    public string weaponName = "Weapon";
    public float damage = 25f;
    [Tooltip("Melee: reach in front of camera. Ranged: ray length when isMelee is off.")]
    public float range = 30f;
    public Sprite icon;
    public bool isMelee;

    [Header("Melee")]
    [Tooltip("Sphere radius for melee hits (more forgiving than a thin ray).")]
    public float meleeRadius = 0.22f;

    [Header("Hit detection")]
    public LayerMask hitMask = ~0;

    [Header("Hold pose (first-person, relative to camera hold point)")]
    public Vector3 holdLocalPosition = new Vector3(0.38f, -0.28f, 0.55f);
    public Vector3 holdLocalEuler = new Vector3(0f, 90f, 0f);

    [Header("Melee swing (local rotation around axis)")]
    [Tooltip("Degrees to swing out (forward chop), then return.")]
    public float swingPeakDegrees = 52f;
    public float swingOutTime = 0.09f;
    public float swingInTime = 0.14f;
    [Tooltip("Local axis to rotate around (default X = pitch forward).")]
    public Vector3 swingLocalEulerAxis = new Vector3(1f, 0f, 0f);

    static readonly System.Collections.Generic.Dictionary<string, Sprite> GeneratedIcons = new System.Collections.Generic.Dictionary<string, Sprite>();

    public void ApplyHoldPose()
    {
        transform.localPosition = holdLocalPosition;
        transform.localRotation = Quaternion.Euler(holdLocalEuler);
    }

    public void EnsureInventoryIcon()
    {
        if (icon != null)
            return;

        icon = GetGeneratedIcon(weaponName);
    }

    static Sprite GetGeneratedIcon(string itemName)
    {
        string key = string.IsNullOrWhiteSpace(itemName) ? "weapon" : itemName.Trim().ToLowerInvariant();
        if (GeneratedIcons.TryGetValue(key, out Sprite existing) && existing != null)
            return existing;

        Sprite created = CreateSwordIcon(key);
        GeneratedIcons[key] = created;
        return created;
    }

    static Sprite CreateSwordIcon(string key)
    {
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.name = $"GeneratedIcon_{key.Replace(' ', '_')}";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        SwordPalette palette = PaletteForKey(key);
        DrawSwordIcon(texture, palette);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 64f);
        sprite.name = $"{key} Inventory Icon";
        return sprite;
    }

    struct SwordPalette
    {
        public Color bladeDark;
        public Color bladeMid;
        public Color bladeLight;
        public Color handle;
        public Color guard;
        public Color accent;
    }

    static SwordPalette PaletteForKey(string key)
    {
        SwordPalette p = new SwordPalette
        {
            bladeDark = new Color(0.42f, 0.46f, 0.5f, 1f),
            bladeMid = new Color(0.72f, 0.78f, 0.84f, 1f),
            bladeLight = new Color(0.96f, 0.98f, 1f, 1f),
            handle = new Color(0.16f, 0.09f, 0.045f, 1f),
            guard = new Color(0.52f, 0.42f, 0.22f, 1f),
            accent = new Color(0.4f, 0.72f, 1f, 1f)
        };

        if (key.Contains("green"))
        {
            p.bladeDark = new Color(0.08f, 0.32f, 0.18f, 1f);
            p.bladeMid = new Color(0.18f, 0.68f, 0.34f, 1f);
            p.bladeLight = new Color(0.68f, 1f, 0.72f, 1f);
            p.accent = new Color(0.3f, 1f, 0.44f, 1f);
        }
        else if (key.Contains("white") || key.Contains("blue"))
        {
            p.bladeDark = new Color(0.18f, 0.35f, 0.58f, 1f);
            p.bladeMid = new Color(0.45f, 0.74f, 1f, 1f);
            p.bladeLight = key.Contains("white") ? new Color(0.95f, 0.98f, 1f, 1f) : new Color(0.72f, 0.9f, 1f, 1f);
            p.accent = new Color(0.25f, 0.67f, 1f, 1f);
        }
        else if (key.Contains("black"))
        {
            p.bladeDark = new Color(0.015f, 0.015f, 0.018f, 1f);
            p.bladeMid = new Color(0.09f, 0.095f, 0.11f, 1f);
            p.bladeLight = new Color(0.34f, 0.36f, 0.42f, 1f);
            p.handle = new Color(0.035f, 0.025f, 0.022f, 1f);
            p.guard = new Color(0.2f, 0.18f, 0.16f, 1f);
            p.accent = new Color(0.75f, 0.1f, 0.08f, 1f);
        }

        return p;
    }

    static void DrawSwordIcon(Texture2D texture, SwordPalette p)
    {
        float angle = -42f * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        Vector2 center = new Vector2(0.5f, 0.5f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float nx = (x + 0.5f) / texture.width;
                float ny = (y + 0.5f) / texture.height;
                Vector2 q = new Vector2(nx - center.x, ny - center.y);
                float u = q.x * cos + q.y * sin;
                float v = -q.x * sin + q.y * cos;

                DrawShadow(texture, x, y, u, v);
                DrawBlade(texture, x, y, u, v, p);
                DrawGuard(texture, x, y, u, v, p);
                DrawHandle(texture, x, y, u, v, p);
                DrawPommel(texture, x, y, u, v, p);
            }
        }
    }

    static void DrawShadow(Texture2D texture, int x, int y, float u, float v)
    {
        if (u > -0.42f && u < 0.43f && Mathf.Abs(v + 0.035f) < 0.035f)
            BlendPixel(texture, x, y, new Color(0f, 0f, 0f, 0.22f), 0.22f);
    }

    static void DrawBlade(Texture2D texture, int x, int y, float u, float v, SwordPalette p)
    {
        if (u <= -0.05f || u >= 0.42f)
            return;

        float bladeWidth = Mathf.Lerp(0.058f, 0.008f, Mathf.InverseLerp(-0.05f, 0.42f, u));
        if (Mathf.Abs(v) > bladeWidth)
            return;

        float edge = 1f - Mathf.Abs(v) / Mathf.Max(0.001f, bladeWidth);
        Color color = Color.Lerp(p.bladeDark, p.bladeMid, edge);
        if (v > 0f)
            color = Color.Lerp(color, p.bladeLight, 0.52f);
        if (Mathf.Abs(v) < bladeWidth * 0.18f)
            color = Color.Lerp(color, p.bladeLight, 0.42f);

        BlendPixel(texture, x, y, color, 1f);
    }

    static void DrawGuard(Texture2D texture, int x, int y, float u, float v, SwordPalette p)
    {
        if (u > -0.11f && u < -0.045f && Mathf.Abs(v) < 0.2f)
        {
            float fade = 1f - Mathf.Abs(v) / 0.2f;
            BlendPixel(texture, x, y, Color.Lerp(p.guard * 0.75f, p.guard, fade), 1f);
        }

        float gem = Ellipse(u, v, -0.075f, 0f, 0.03f, 0.038f);
        if (gem > 0f)
            BlendPixel(texture, x, y, Color.Lerp(p.accent * 0.55f, p.accent, gem), gem);
    }

    static void DrawHandle(Texture2D texture, int x, int y, float u, float v, SwordPalette p)
    {
        if (u > -0.39f && u < -0.11f && Mathf.Abs(v) < 0.045f)
        {
            float band = Mathf.Abs(Mathf.Sin((u + 0.38f) * 90f));
            Color color = Color.Lerp(p.handle, p.guard * 0.75f, band > 0.78f ? 0.55f : 0.08f);
            if (v > 0.018f)
                color = Color.Lerp(color, Color.white, 0.18f);
            BlendPixel(texture, x, y, color, 1f);
        }
    }

    static void DrawPommel(Texture2D texture, int x, int y, float u, float v, SwordPalette p)
    {
        float pommel = Ellipse(u, v, -0.43f, 0f, 0.055f, 0.06f);
        if (pommel > 0f)
            BlendPixel(texture, x, y, Color.Lerp(p.guard * 0.7f, p.guard, pommel), pommel);
    }

    static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = x - cx;
        float dy = y - cy;
        float d = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
        return Mathf.Clamp01(1f - d);
    }

    static void BlendPixel(Texture2D texture, int x, int y, Color color, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        if (alpha <= 0f)
            return;

        Color existing = texture.GetPixel(x, y);
        float outAlpha = alpha + existing.a * (1f - alpha);
        if (outAlpha <= 0f)
            return;

        float existingFactor = existing.a * (1f - alpha);
        Color blended = new Color(
            (color.r * alpha + existing.r * existingFactor) / outAlpha,
            (color.g * alpha + existing.g * existingFactor) / outAlpha,
            (color.b * alpha + existing.b * existingFactor) / outAlpha,
            outAlpha);
        texture.SetPixel(x, y, blended);
    }
}
