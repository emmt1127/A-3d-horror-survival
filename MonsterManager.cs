using UnityEngine;

/// <summary>
/// Manages the spawning and despawning of the monster based on day/night cycle.
/// Attach this to a GameObject in the scene (e.g., empty "MonsterManager").
/// Assign the monster prefab in the inspector.
/// </summary>
public class MonsterManager : MonoBehaviour
{
    [Header("Prefab & Settings")]
    [Tooltip("The monster prefab to spawn at night")]
    public GameObject monsterPrefab;

    [Tooltip("Minimum random nighttime spawn distance from the player. Leave 0 to use the map-spawn default.")]
    public float minSpawnDistance = 0f;

    [Tooltip("Maximum random nighttime spawn distance from the player. Leave 0 to use the map-spawn default.")]
    public float maxSpawnDistance = 0f;

    [Tooltip("Default minimum distance for random map nighttime spawns.")]
    public float defaultNightSpawnMinDistance = 70f;

    [Tooltip("Default maximum distance for random map nighttime spawns.")]
    public float defaultNightSpawnMaxDistance = 160f;

    [Tooltip("Scale applied to spawned monsters. The mutant asset is authored small, so 3 keeps it visible in the forest.")]
    public float spawnScale = 3f;

    [Header("Protected Zones")]
    [Tooltip("Monster cannot spawn inside or enter this radius around the campfire.")]
    public float campfireProtectedRadius = 28f;

    [Tooltip("Monster cannot spawn inside or enter this radius around the camping tent.")]
    public float tentProtectedRadius = 18f;

    [Tooltip("Optional tent transform. If empty, the manager looks for an object with 'tent' in its name.")]
    public Transform tentTransformOverride;

    private GameObject currentMonster;
    private DayNightCycle dayNightCycle;
    Transform _resolvedTentTransform;

    [Header("Auto Assign Overrides")]
    [Tooltip("Optional: assign a specific player Transform to use for spawned monsters. If null, will find GameObject with tag 'Player'.")]
    public Transform playerTransformOverride;
    [Tooltip("Optional: assign a specific player Camera to use for spawned monsters.")]
    public Camera playerCameraOverride;
    [Tooltip("Optional: assign a specific Health component to use for spawned monsters.")]
    public Health playerHealthOverride;
    [Tooltip("Optional: assign an empty GameObject whose GameObject will be used to create or find the JumpscareManager component.")]
    public GameObject jumpscareManagerObjectOverride;
    [Tooltip("Optional: assign an empty GameObject whose GameObject will be used to create or find the Campfire component.")]
    public GameObject campfireObjectOverride;

    void Start()
    {
        // Find DayNightCycle in the scene
        dayNightCycle = FindObjectOfType<DayNightCycle>();
        if (dayNightCycle == null)
        {
            Debug.LogError("MonsterManager: DayNightCycle not found in scene!");
            enabled = false;
            return;
        }

        // If it's already night, spawn the monster immediately
        if (!dayNightCycle.IsDay && monsterPrefab != null)
        {
            SpawnMonster();
        }
    }

    void Update()
    {
        if (dayNightCycle == null || monsterPrefab == null)
            return;

        // If day starts and monster is active, despawn it
        if (dayNightCycle.IsDay && currentMonster != null)
        {
            DespawnMonster();
        }

        // If night starts and no monster, spawn one
        if (!dayNightCycle.IsDay && currentMonster == null)
        {
            SpawnMonster();
        }
    }

    void SpawnMonster()
    {
        if (monsterPrefab == null || dayNightCycle == null || dayNightCycle.IsDay)
            return;

        // Instantiate the monster prefab
        currentMonster = Instantiate(monsterPrefab);
        // Ensure the spawned prefab is active and properly scaled in case the asset was saved inactive
        if (currentMonster != null)
        {
            currentMonster.SetActive(true);
            if (spawnScale > 0f)
                currentMonster.transform.localScale = Vector3.one * spawnScale;
        }
        else
        {
            return;
        }

        // Populate commonly-required scene references on the instantiated monster
        MonsterAI ai = currentMonster.GetComponent<MonsterAI>();
        if (ai != null)
        {
            Transform playerTransform = playerTransformOverride;
            Camera playerCamera = playerCameraOverride;
            Health playerHealth = playerHealthOverride;
            JumpscareManager jumpscareManager = null;
            Campfire campfire = null;

            if (playerTransform == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    playerTransform = playerObj.transform;
            }

            if (playerCamera == null && playerTransform != null)
                playerCamera = playerTransform.GetComponentInChildren<Camera>();

            if (playerHealth == null && playerTransform != null)
                playerHealth = playerTransform.GetComponent<Health>();

            if (jumpscareManagerObjectOverride != null)
            {
                jumpscareManager = jumpscareManagerObjectOverride.GetComponent<JumpscareManager>();
                if (jumpscareManager == null)
                    jumpscareManager = jumpscareManagerObjectOverride.AddComponent<JumpscareManager>();
            }
            else
            {
                jumpscareManager = FindObjectOfType<JumpscareManager>();
                if (jumpscareManager == null)
                {
                    GameObject jmObj = new GameObject("JumpscareManager");
                    jumpscareManager = jmObj.AddComponent<JumpscareManager>();
                }
            }

            if (campfireObjectOverride != null)
            {
                campfire = campfireObjectOverride.GetComponent<Campfire>();
                if (campfire == null)
                    campfire = campfireObjectOverride.AddComponent<Campfire>();
            }
            else
            {
                campfire = FindObjectOfType<Campfire>();
                if (campfire == null)
                {
                    GameObject campObj = new GameObject("Campfire");
                    campfire = campObj.AddComponent<Campfire>();
                }
            }

            ai.InitializeMonster(playerTransform, playerCamera, playerHealth, jumpscareManager, campfire);
            ai.ConfigureProtectedZones(campfire != null ? campfire.transform : null, campfireProtectedRadius, ResolveTentTransform(), tentProtectedRadius);

            float minD = minSpawnDistance > 0f ? minSpawnDistance : defaultNightSpawnMinDistance;
            float maxD = maxSpawnDistance > 0f ? maxSpawnDistance : defaultNightSpawnMaxDistance;
            if (maxD < minD)
                maxD = minD;

            ai.SpawnRandomAroundMap(minD, maxD);
            ai.BeginChasingPlayer();

            ai.EnsureVisible();
        }
        else
        {
            Debug.LogWarning("MonsterManager: Spawned monster has no MonsterAI component.");
        }
    }

    Transform ResolveTentTransform()
    {
        if (tentTransformOverride != null)
            return tentTransformOverride;

        if (_resolvedTentTransform != null)
            return _resolvedTentTransform;

        Transform[] allTransforms = FindObjectsOfType<Transform>();
        foreach (Transform t in allTransforms)
        {
            if (t != null && t.name.ToLowerInvariant().Contains("tent"))
            {
                _resolvedTentTransform = t;
                return _resolvedTentTransform;
            }
        }

        return null;
    }

    void DespawnMonster()
    {
        if (currentMonster != null)
        {
            // Notify AI to clean up (disable NavMeshAgent)
            MonsterAI ai = currentMonster.GetComponent<MonsterAI>();
            if (ai != null)
            {
                ai.OnDespawn();
            }
            Destroy(currentMonster);
            currentMonster = null;
        }
    }
}
