using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Enhanced weapon spawner that scatters weapons throughout the world.
/// Weapons can spawn on the ground, on surfaces, in buildings, and other interesting locations.
/// </summary>
public class WeaponSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("List of weapon prefabs to spawn (must have Weapon + WeaponPickup components)")]
    public List<GameObject> weaponPrefabs;

    [Tooltip("Maximum number of weapons active in the world at once")]
    public int maxActiveWeapons = 20;

    [Tooltip("Time between spawn attempts")]
    public float spawnInterval = 15f;

    [Tooltip("Radius around spawner to search for spawn points")]
    public float spawnRadius = 200f;

    [Tooltip("Layer mask for ground and surface detection")]
    public LayerMask groundMask = 1;

    [Tooltip("Minimum distance from player to spawn weapons")]
    public float minDistanceFromPlayer = 15f;

    [Tooltip("Maximum distance from player to spawn weapons (prevents spawning too far)")]
    public float maxDistanceFromPlayer = 100f;

    [Header("Initial Scatter")]
    [Tooltip("How many weapons to scatter when scene loads")]
    public int initialWorldWeapons = 15;

    [Tooltip("Use more spread out distribution for initial scatter")]
    public bool useWideDistribution = true;

    [Header("Spawn Locations")]
    [Tooltip("Allow spawning on the ground")]
    public bool spawnOnGround = true;

    [Tooltip("Allow spawning on elevated surfaces (rocks, shelves, etc.)")]
    public bool spawnOnSurfaces = true;

    [Tooltip("Maximum height for surface spawns")]
    public float maxSurfaceHeight = 5f;

    [Tooltip("Allow spawning near points of interest (buildings, landmarks, etc.)")]
    public bool spawnNearPointsOfInterest = true;

    [Tooltip("Transforms representing points of interest (buildings, camps, etc.)")]
    public List<Transform> pointsOfInterest;

    [Header("Weapon Placement")]
    [Tooltip("Random rotation for spawned weapons")]
    public bool randomRotation = true;

    [Tooltip("Random height offset for ground spawns")]
    public Vector2 heightOffsetRange = new Vector2(0.1f, 0.5f);

    [Tooltip("Chance to spawn weapon leaning against something")]
    [Range(0f, 1f)]
    public float leanChance = 0.3f;

    [Tooltip("Lean angle range")]
    public Vector2 leanAngleRange = new Vector2(15f, 45f);

    [Header("Visual Effects")]
    [Tooltip("Spawn a glow effect under weapons")]
    public bool spawnGlowEffect = true;

    [Tooltip("Glow effect prefab")]
    public GameObject glowEffectPrefab;

    [Tooltip("Color of the glow effect")]
    public Color glowColor = new Color(1f, 0.8f, 0f, 0.5f);

    // Internal state
    private float timer;
    private List<GameObject> spawned = new List<GameObject>();
    private Transform playerTransform;

    void Start()
    {
        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // Initialize timer
        timer = spawnInterval * 0.5f;

        // Initial scatter of weapons
        for (int i = 0; i < initialWorldWeapons; i++)
        {
            TrySpawn(useWideDistribution);
        }
    }

    void Update()
    {
        // Update player reference if needed
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        // Spawn timer
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = spawnInterval;
            TrySpawn(false);
        }

        // Clean up destroyed weapons
        spawned.RemoveAll(x => x == null);
    }

    /// <summary>
    /// Attempts to spawn a weapon at a valid location.
    /// </summary>
    /// <param name="wideDistribution">If true, uses wider distribution for initial scatter</param>
    void TrySpawn(bool wideDistribution)
    {
        if (spawned.Count >= maxActiveWeapons) return;
        if (weaponPrefabs == null || weaponPrefabs.Count == 0) return;

        Vector3 center = transform.position;
        float effectiveRadius = wideDistribution ? spawnRadius * 1.5f : spawnRadius;

        // Try multiple spawn attempts
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector3 spawnPosition = Vector3.zero;
            bool validPosition = false;

            // Decide spawn type
            float spawnTypeRoll = Random.value;

            if (spawnNearPointsOfInterest && pointsOfInterest != null && pointsOfInterest.Count > 0 && spawnTypeRoll < 0.3f)
            {
                // Spawn near point of interest
                validPosition = TrySpawnNearPointOfInterest(out spawnPosition);
            }
            else if (spawnOnSurfaces && spawnTypeRoll < 0.6f)
            {
                // Spawn on elevated surface
                validPosition = TrySpawnOnSurface(out spawnPosition);
            }
            else if (spawnOnGround)
            {
                // Spawn on ground
                validPosition = TrySpawnOnGround(out spawnPosition, effectiveRadius);
            }

            if (validPosition && IsValidSpawnPosition(spawnPosition))
            {
                SpawnWeapon(spawnPosition);
                return;
            }
        }
    }

    /// <summary>
    /// Attempts to find a valid ground spawn position.
    /// </summary>
    bool TrySpawnOnGround(out Vector3 position, float radius)
    {
        position = Vector3.zero;

        Vector2 randomOffset = Random.insideUnitCircle * radius;
        Vector3 rayOrigin = transform.position + new Vector3(randomOffset.x, 50f, randomOffset.y);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100f, groundMask))
        {
            position = hit.point;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to find a valid elevated surface spawn position.
    /// </summary>
    bool TrySpawnOnSurface(out Vector3 position)
    {
        position = Vector3.zero;

        // First find ground
        if (!TrySpawnOnGround(out Vector3 groundPos, spawnRadius))
            return false;

        // Try to find surface above ground
        Vector3 rayOrigin = groundPos + Vector3.up * maxSurfaceHeight;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxSurfaceHeight, groundMask))
        {
            // Make sure it's not the same as ground (has some elevation)
            if (hit.point.y > groundPos.y + 0.5f)
            {
                position = hit.point;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Attempts to spawn near a point of interest.
    /// </summary>
    bool TrySpawnNearPointOfInterest(out Vector3 position)
    {
        position = Vector3.zero;

        if (pointsOfInterest == null || pointsOfInterest.Count == 0)
            return false;

        // Pick random point of interest
        Transform poi = pointsOfInterest[Random.Range(0, pointsOfInterest.Count)];
        if (poi == null)
            return false;

        // Random position around POI
        Vector2 randomOffset = Random.insideUnitCircle * 15f; // 15 units around POI
        Vector3 rayOrigin = poi.position + new Vector3(randomOffset.x, 20f, randomOffset.y);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 40f, groundMask))
        {
            position = hit.point;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a spawn position is valid (not too close/far from player, etc.).
    /// </summary>
    bool IsValidSpawnPosition(Vector3 position)
    {
        // Check distance from player
        if (playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(position, playerTransform.position);
            if (distanceToPlayer < minDistanceFromPlayer)
                return false;

            if (distanceToPlayer > maxDistanceFromPlayer)
                return false;
        }

        // Check if too close to other spawned weapons
        foreach (GameObject weapon in spawned)
        {
            if (weapon != null && Vector3.Distance(position, weapon.transform.position) < 3f)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Spawns a weapon at the specified position with appropriate rotation and effects.
    /// </summary>
    void SpawnWeapon(Vector3 position)
    {
        // Select random weapon prefab
        GameObject prefab = weaponPrefabs[Random.Range(0, weaponPrefabs.Count)];

        // Calculate spawn position with height offset
        float heightOffset = Random.Range(heightOffsetRange.x, heightOffsetRange.y);
        Vector3 spawnPos = position + Vector3.up * heightOffset;

        // Calculate rotation
        Quaternion spawnRot = Quaternion.identity;
        if (randomRotation)
        {
            spawnRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            // Chance to lean weapon
            if (Random.value < leanChance)
            {
                float leanAngle = Random.Range(leanAngleRange.x, leanAngleRange.y);
                Vector3 leanAxis = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                spawnRot *= Quaternion.AngleAxis(leanAngle, leanAxis);
            }
        }

        // Instantiate weapon
        GameObject weapon = Instantiate(prefab, spawnPos, spawnRot);
        PrepareSpawnedWeapon(weapon);
        spawned.Add(weapon);

        // Add glow effect
        if (spawnGlowEffect && glowEffectPrefab != null)
        {
            GameObject glow = Instantiate(glowEffectPrefab, spawnPos, Quaternion.identity);
            glow.transform.SetParent(weapon.transform);

            // Set glow color if possible
            Renderer glowRenderer = glow.GetComponent<Renderer>();
            if (glowRenderer != null)
            {
                glowRenderer.material.color = glowColor;
            }
        }
    }

    /// <summary>
    /// Manually spawn a specific weapon at a specific position.
    /// </summary>
    public void SpawnSpecificWeapon(GameObject weaponPrefab, Vector3 position, Quaternion rotation)
    {
        if (weaponPrefab == null)
            return;

        GameObject weapon = Instantiate(weaponPrefab, position, rotation);
        PrepareSpawnedWeapon(weapon);
        spawned.Add(weapon);

        if (spawnGlowEffect && glowEffectPrefab != null)
        {
            GameObject glow = Instantiate(glowEffectPrefab, position, Quaternion.identity);
            glow.transform.SetParent(weapon.transform);
        }
    }

    void PrepareSpawnedWeapon(GameObject weaponObject)
    {
        if (weaponObject == null)
            return;

        Weapon weapon = weaponObject.GetComponent<Weapon>();
        if (weapon == null)
            weapon = weaponObject.AddComponent<Weapon>();

        if (string.IsNullOrWhiteSpace(weapon.weaponName) || weapon.weaponName == "Weapon")
            weapon.weaponName = weaponObject.name.Replace("(Clone)", "").Trim();

        if (LooksLikeGun(weaponObject.name) && weaponObject.GetComponent<GunWeapon>() == null)
        {
            GunWeapon gun = weaponObject.AddComponent<GunWeapon>();
            gun.PrepareWeaponData();
        }

        Collider[] colliders = weaponObject.GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
        {
            BoxCollider box = weaponObject.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.45f;
            colliders = new Collider[] { box };
        }

        foreach (Collider col in colliders)
        {
            if (col == null)
                continue;
            col.enabled = true;
            col.isTrigger = false;
        }

        Rigidbody rb = weaponObject.GetComponent<Rigidbody>();
        if (rb == null)
            rb = weaponObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.detectCollisions = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        WeaponPickup pickup = weaponObject.GetComponent<WeaponPickup>();
        if (pickup == null)
            pickup = weaponObject.AddComponent<WeaponPickup>();
        pickup.weaponData = weapon;
        pickup.useTriggerPickup = false;
    }

    bool LooksLikeGun(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        string n = objectName.ToLowerInvariant();
        return n.Contains("gun") || n.Contains("rifle") || n.Contains("pistol") || n.Contains("shot") || n.Contains("musket");
    }

    void OnDrawGizmosSelected()
    {
        // Draw spawn radius
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        // Draw player distance bounds
        Gizmos.color = Color.yellow;
        if (playerTransform != null)
        {
            Gizmos.DrawWireSphere(playerTransform.position, minDistanceFromPlayer);
            Gizmos.DrawWireSphere(playerTransform.position, maxDistanceFromPlayer);
        }

        // Draw points of interest
        if (pointsOfInterest != null)
        {
            Gizmos.color = Color.cyan;
            foreach (Transform poi in pointsOfInterest)
            {
                if (poi != null)
                {
                    Gizmos.DrawWireSphere(poi.position, 15f);
                    Gizmos.DrawLine(transform.position, poi.position);
                }
            }
        }
    }
}
