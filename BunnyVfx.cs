using UnityEngine;

/// <summary>
/// Static helpers for bunny hit/death particles. Called from <see cref="BunnyHealth"/> — do not add this as a component.
/// </summary>
public static class BunnyVfx
{
    static Material CreateParticleMaterial()
    {
        Shader s = Shader.Find("Particles/Standard Unlit");
        if (s == null)
            s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null)
            s = Shader.Find("Particles/Alpha Blended");
        if (s == null)
            s = Shader.Find("Sprites/Default");

        if (s == null)
            return null;

        Material mat = new Material(s);
        mat.name = "BunnyVfxRuntimeMaterial";
        return mat;
    }

    public static void PlayHit(Vector3 worldPos, Vector3 approximateNormal)
    {
        approximateNormal = approximateNormal.sqrMagnitude > 0.0001f ? approximateNormal.normalized : Vector3.up;
        GameObject go = CreateParticleObject("BunnyHitVFX");
        go.transform.position = worldPos + approximateNormal * 0.02f;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.2f;
        main.startLifetime = 0.22f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
        main.startSize = 0.05f;
        main.startColor = new Color(1f, 0.55f, 0.45f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;
        main.gravityModifier = 0.35f;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)18) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.06f;
        shape.rotation = Quaternion.LookRotation(approximateNormal).eulerAngles;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.9f, 0.85f), 0f), new GradientColorKey(new Color(0.9f, 0.2f, 0.15f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ApplyRenderer(ps);
        go.SetActive(true);
        ps.Play();
        Object.Destroy(go, 1.2f);
    }

    public static void PlayDeath(Vector3 worldPos)
    {
        GameObject go = CreateParticleObject("BunnyDeathVFX");
        go.transform.position = worldPos + Vector3.up * 0.15f;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.35f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new Color(0.85f, 0.12f, 0.1f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        main.gravityModifier = 0.6f;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)48) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.18f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.35f, 0.25f), 0f), new GradientColorKey(new Color(0.3f, 0.02f, 0.02f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ApplyRenderer(ps);
        go.SetActive(true);
        ps.Play();
        Object.Destroy(go, 2f);
    }

    static GameObject CreateParticleObject(string name)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false);
        return go;
    }

    static void ApplyRenderer(ParticleSystem ps)
    {
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        Material particleMaterial = CreateParticleMaterial();
        if (particleMaterial != null)
            r.material = particleMaterial;
        r.renderMode = ParticleSystemRenderMode.Billboard;
    }
}
