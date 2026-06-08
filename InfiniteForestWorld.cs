using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// Simple infinite chunk spawner: trees, optional grass, bunnies, wood pickups around the player.
/// Assign prefabs and a ground mask (Terrain / ground layer). Place one empty in the scene and press Play.
/// </summary>
public class InfiniteForestWorld : MonoBehaviour
{
    [Header("Player")]
    public Transform player;
    [Tooltip("Auto-find object tagged Player if null.")]
    public bool autoFindPlayer = true;

    [Header("Chunk grid")]
    public float chunkSize = 48f;
    [Tooltip("Builds one fixed forest map at startup instead of streaming chunks while the player moves.")]
    public bool buildFixedMapAtStart = true;
    public bool matchTerrainStreamerFixedMap = true;
    public int fixedMapChunksX = 8;
    public int fixedMapChunksZ = 4;
    [Tooltip("When Use Global Quality is on, max radius cap. When off, exact radius.")]
    public int loadRadius = 1;
    public bool useGlobalStreamingQuality = true;
    public LayerMask groundMask = ~0;
    public float raycastStartHeight = 80f;
    public float raycastMaxDistance = 200f;
    [Tooltip("Repairs empty/too-low spawn settings at runtime so the forest does not become just ground.")]
    public bool autoRepairEmptySpawnSettings = true;
    [Tooltip("Limits heavy chunk object spawning per frame to smooth out lag spikes.")]
    public int maxChunkSpawnsPerFrame = 12;
    [Header("Minecraft-style performance")]
    [Tooltip("Keeps the fixed map generated, but disables far forest object chunks so Unity renders less.")]
    public bool cullDistantForestChunks = true;
    [Tooltip("Forest chunks outside this chunk radius from the player are hidden until approached.")]
    public int forestChunkRenderRadius = 2;
    [Tooltip("How often to update chunk visibility. Higher is cheaper but slower to react.")]
    public float chunkCullInterval = 0.35f;
    [Tooltip("Grass is decoration, so disabling its shadows saves a surprising amount of GPU time.")]
    public bool disableGrassShadows = true;
    [Tooltip("Removes colliders from spawned grass prefabs so decoration never participates in physics.")]
    public bool removeGrassPrefabColliders = true;

    [Header("Trees")]
    public GameObject[] treePrefabs;
    public int treesPerChunkMin = 18;
    public int treesPerChunkMax = 30;
    [Tooltip("Uses generated blocky, modded-Minecraft-style trees instead of the assigned prefab trees.")]
    public bool useStylizedRuntimeTrees = false;
    [Tooltip("When no tree prefabs are assigned, generate simple rounded runtime trees instead of spawning nothing.")]
    public bool useRuntimeTreeFallback = true;
    public float treeMinimumSpacing = 3.35f;
    public int treeSpawnAttemptsPerTree = 24;
    public bool avoidWaterForTrees = true;
    public float treeWaterHeight = 1.8f;
    [Tooltip("Optional center used to keep trees out of camp. If blank, CampLocator or Campfire is used.")]
    public Transform treeCampfireAvoidCenter;
    [Tooltip("Trees will not spawn within this horizontal radius of the campfire, creating a readable camp clearing.")]
    public float treeCampfireAvoidRadius = 30f;
    public Vector2 treeScaleRange = new Vector2(0.95f, 1.25f);
    [Tooltip("Adds colliders/NavMesh obstacles to spawned trees so the monster routes around them.")]
    public bool addMonsterBlockingToTrees = true;
    [Tooltip("NavMesh carving is expensive. Leave off for smoother movement; colliders still block objects.")]
    public bool addNavMeshCarvingToSpawnedObjects = false;
    public float treeObstacleRadius = 1.25f;
    public float treeObstacleHeight = 5.5f;

    [Header("Grass / detail (optional)")]
    public GameObject grassPrefab;
    public int grassPerChunkMin = 3;
    public int grassPerChunkMax = 8;
    [Tooltip("When no grass prefab is assigned, generate tiny runtime grass patches instead of spawning nothing.")]
    public bool useRuntimeGrassFallback = true;

    [Header("Bunnies")]
    public GameObject bunnyPrefab;
    public GameObject bunnyFoodPrefab;
    public int bunniesPerChunkMin = 1;
    public int bunniesPerChunkMax = 1;
    [Range(0f, 1f)]
    public float bunnySpawnChancePerChunk = 0.75f;
    [Tooltip("Guarantees a few bunnies near the camp edge so the player can find them early.")]
    public bool spawnBunniesNearCamp = true;
    public int campBunnyCount = 6;
    public float campBunnyInnerRadius = 18f;
    public float campBunnyOuterRadius = 34f;
    public int campBunnySpawnAttempts = 90;

    [Header("Wood")]
    public GameObject woodPrefab;
    public int woodPerChunkMin = 1;
    public int woodPerChunkMax = 4;
    [Tooltip("Stops loose wood from spawning on the low terrain that visually reads as water.")]
    public bool avoidWaterForWood = true;
    [Tooltip("Ground at or below this world height is treated as water for wood spawning.")]
    public float woodWaterHeight = 1.6f;
    [Tooltip("Extra height above the water line before wood can spawn.")]
    public float woodWaterEdgePadding = 0.2f;
    [Tooltip("How many random positions each wood pickup tries before giving up.")]
    public int woodSpawnAttemptsPerItem = 40;
    [Tooltip("Minimum horizontal space between spawned wood pickups.")]
    public float woodMinimumSpacing = 5f;
    [Tooltip("Optional center used to keep wood away from camp. If blank, CampLocator or Campfire is used.")]
    public Transform woodCampfireAvoidCenter;
    [Tooltip("Wood will not spawn within this horizontal radius of the campfire.")]
    public float woodCampfireAvoidRadius = 32f;
    [Tooltip("Adds small NavMesh obstacles to spawned wood piles so AI avoids clipping through them.")]
    public bool addMonsterBlockingToWood = true;

    readonly Dictionary<Vector2Int, Transform> _chunks = new Dictionary<Vector2Int, Transform>();
    readonly Queue<Vector2Int> _pendingChunkSpawns = new Queue<Vector2Int>();
    readonly HashSet<Vector2Int> _queuedChunkSpawns = new HashSet<Vector2Int>();
    readonly HashSet<Vector2Int> _wantedChunks = new HashSet<Vector2Int>();
    readonly List<Vector3> _spawnedWoodPositions = new List<Vector3>();
    Vector2Int _lastChunk;
    int _cachedEffectiveRadius = int.MinValue;
    float _nextChunkCullTime;
    DayNightCycle _dayNightCycle;
    Transform _resolvedCampfireAvoidCenter;
    Transform _resolvedTreeCampfireAvoidCenter;
    Material _stylizedTrunkMaterial;
    Material _stylizedLeafMaterial;
    Material _stylizedLeafDarkMaterial;
    Material _runtimeGrassMaterial;
    bool _campBunniesSpawned;

    public bool IsLoading => _pendingChunkSpawns.Count > 0 || _queuedChunkSpawns.Count > 0 || (spawnBunniesNearCamp && !_campBunniesSpawned);
    public float LoadingProgress01
    {
        get
        {
            int wanted = Mathf.Max(1, _wantedChunks.Count);
            int loaded = 0;
            foreach (Vector2Int id in _wantedChunks)
            {
                if (_chunks.ContainsKey(id))
                    loaded++;
            }

            float chunkProgress = Mathf.Clamp01(loaded / (float)wanted);
            if (spawnBunniesNearCamp && !_campBunniesSpawned)
                chunkProgress *= 0.9f;

            return chunkProgress;
        }
    }

    int EffectiveLoadRadius
    {
        get
        {
            if (!useGlobalStreamingQuality)
                return Mathf.Max(0, loadRadius);
            int q = WorldStreamSettings.ChunkRadiusForQuality(WorldStreamSettings.LoadQualityIndex());
            return Mathf.Max(0, Mathf.Min(loadRadius, q));
        }
    }

    void Awake()
    {
        RepairSpawnSettingsIfNeeded();

        if (player == null && autoFindPlayer)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null)
            {
                // Fallback: find by controller component
                controller ctrl = FindObjectOfType<controller>();
                if (ctrl != null)
                    p = ctrl.gameObject;
            }
            if (p != null)
                player = p.transform;
        }
    }

    void Start()
    {
        _dayNightCycle = FindObjectOfType<DayNightCycle>();
        if (_dayNightCycle != null)
            _dayNightCycle.OnDayStarted += OnDayStarted;

        if (player != null)
            _lastChunk = WorldToChunk(player.position);
        if (buildFixedMapAtStart)
            BuildFixedForestMap();
        else
            RefreshChunks();
        ProcessChunkSpawnQueue();

        if (spawnBunniesNearCamp)
            StartCoroutine(SpawnCampBunniesWhenGroundReady());
    }

    void LateUpdate()
    {
        if (player == null)
            return;

        if (buildFixedMapAtStart)
        {
            ProcessChunkSpawnQueue();
            UpdateForestChunkVisibility(false);
            return;
        }

        int eff = EffectiveLoadRadius;
        if (eff != _cachedEffectiveRadius)
        {
            _cachedEffectiveRadius = eff;
            RefreshChunks();
        }

        Vector2Int current = WorldToChunk(player.position);
        if (current != _lastChunk)
        {
            _lastChunk = current;
            RefreshChunks();
        }

        ProcessChunkSpawnQueue();
        UpdateForestChunkVisibility(false);
    }

    Vector2Int WorldToChunk(Vector3 world)
    {
        int cx = Mathf.FloorToInt(world.x / chunkSize);
        int cz = Mathf.FloorToInt(world.z / chunkSize);
        return new Vector2Int(cx, cz);
    }

    void RefreshChunks()
    {
        if (player == null)
            return;

        int r = EffectiveLoadRadius;
        _cachedEffectiveRadius = r;

        HashSet<Vector2Int> keep = new HashSet<Vector2Int>();
        for (int x = -r; x <= r; x++)
        {
            for (int z = -r; z <= r; z++)
            {
                Vector2Int id = _lastChunk + new Vector2Int(x, z);
                keep.Add(id);
                if (!_chunks.ContainsKey(id))
                    QueueChunkSpawn(id);
            }
        }

        _wantedChunks.Clear();
        foreach (Vector2Int id in keep)
            _wantedChunks.Add(id);

        List<Vector2Int> remove = new List<Vector2Int>();
        foreach (var kv in _chunks)
        {
            if (!keep.Contains(kv.Key))
                remove.Add(kv.Key);
        }

        foreach (Vector2Int id in remove)
        {
            if (_chunks.TryGetValue(id, out Transform root) && root != null)
                Destroy(root.gameObject);
            _chunks.Remove(id);
        }
    }

    void BuildFixedForestMap()
    {
        _wantedChunks.Clear();
        _pendingChunkSpawns.Clear();
        _queuedChunkSpawns.Clear();

        if (matchTerrainStreamerFixedMap)
        {
            InfiniteTerrainStreamer terrainStreamer = FindObjectOfType<InfiniteTerrainStreamer>();
            if (terrainStreamer != null && terrainStreamer.buildFixedMapAtStart)
            {
                fixedMapChunksX = terrainStreamer.fixedMapChunksX;
                fixedMapChunksZ = terrainStreamer.fixedMapChunksZ;
                chunkSize = terrainStreamer.ChunkWorldSize;
            }
        }

        int chunksX = Mathf.Max(1, fixedMapChunksX);
        int chunksZ = Mathf.Max(1, fixedMapChunksZ);
        Vector2Int center = player != null ? WorldToChunk(player.position) : Vector2Int.zero;
        int startX = center.x - chunksX / 2;
        int startZ = center.y - chunksZ / 2;

        for (int x = 0; x < chunksX; x++)
        {
            for (int z = 0; z < chunksZ; z++)
            {
                Vector2Int id = new Vector2Int(startX + x, startZ + z);
                _wantedChunks.Add(id);
                if (!_chunks.ContainsKey(id))
                    QueueChunkSpawn(id);
            }
        }
    }

    void QueueChunkSpawn(Vector2Int id)
    {
        if (_queuedChunkSpawns.Contains(id))
            return;

        _queuedChunkSpawns.Add(id);
        _pendingChunkSpawns.Enqueue(id);
    }

    void ProcessChunkSpawnQueue()
    {
        int budget = Mathf.Max(1, maxChunkSpawnsPerFrame);
        int spawnedThisFrame = 0;

        while (_pendingChunkSpawns.Count > 0 && spawnedThisFrame < budget)
        {
            Vector2Int id = _pendingChunkSpawns.Dequeue();
            _queuedChunkSpawns.Remove(id);

            if (_chunks.ContainsKey(id) || !IsChunkInCurrentRadius(id))
                continue;

            SpawnChunk(id);
            spawnedThisFrame++;
        }

        if (spawnedThisFrame > 0)
            UpdateForestChunkVisibility(true);
    }

    bool IsChunkInCurrentRadius(Vector2Int id)
    {
        if (buildFixedMapAtStart)
            return _wantedChunks.Contains(id);

        int r = EffectiveLoadRadius;
        return Mathf.Abs(id.x - _lastChunk.x) <= r && Mathf.Abs(id.y - _lastChunk.y) <= r;
    }

    void SpawnChunk(Vector2Int id)
    {
        GameObject root = new GameObject($"ForestChunk_{id.x}_{id.y}");
        root.transform.SetParent(transform);
        _chunks[id] = root.transform;

        Random.InitState(id.x * 73856093 ^ id.y * 19349663);

        float baseX = id.x * chunkSize;
        float baseZ = id.y * chunkSize;

        SpawnTrees(root.transform, baseX, baseZ);
        SpawnGrass(root.transform, baseX, baseZ);
        SpawnWood(root.transform, baseX, baseZ);

        try
        {
            SpawnBunnies(root.transform, baseX, baseZ);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"InfiniteForestWorld: Failed to spawn bunny in chunk {id}. Trees and wood will continue spawning. {ex}");
        }

        OptimizeChunkDecorations(root);
    }

    void UpdateForestChunkVisibility(bool force)
    {
        if (!cullDistantForestChunks || player == null)
            return;

        if (!force && Time.time < _nextChunkCullTime)
            return;

        _nextChunkCullTime = Time.time + Mathf.Max(0.05f, chunkCullInterval);
        Vector2Int playerChunk = WorldToChunk(player.position);
        int radius = Mathf.Max(0, forestChunkRenderRadius);

        foreach (KeyValuePair<Vector2Int, Transform> kv in _chunks)
        {
            Transform root = kv.Value;
            if (root == null)
                continue;

            bool closeEnough = Mathf.Abs(kv.Key.x - playerChunk.x) <= radius
                && Mathf.Abs(kv.Key.y - playerChunk.y) <= radius;
            if (root.gameObject.activeSelf != closeEnough)
                root.gameObject.SetActive(closeEnough);
        }
    }

    void OnDayStarted(int completedDays)
    {
        if (completedDays < 1)
            return;

        foreach (KeyValuePair<Vector2Int, Transform> kv in _chunks)
        {
            Transform chunkRoot = kv.Value;
            if (chunkRoot == null)
                continue;

            // Add a few extra wood piles and rabbits each new day.
            Vector2Int id = kv.Key;
            float baseX = id.x * chunkSize;
            float baseZ = id.y * chunkSize;
            SpawnWood(chunkRoot, baseX, baseZ);
            try
            {
                SpawnBunnies(chunkRoot, baseX, baseZ);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"InfiniteForestWorld: Failed to respawn bunny in chunk {id} on new day. {ex}");
            }
        }
    }

    void SpawnTrees(Transform parent, float baseX, float baseZ)
    {
        bool hasTreePrefabs = treePrefabs != null && treePrefabs.Length > 0;
        bool canUseRuntimeFallback = useRuntimeTreeFallback && !hasTreePrefabs;
        if (!useStylizedRuntimeTrees && !hasTreePrefabs && !canUseRuntimeFallback)
            return;

        int tmin = Mathf.Min(treesPerChunkMin, treesPerChunkMax);
        int tmax = Mathf.Max(treesPerChunkMin, treesPerChunkMax);
        int count = Random.Range(tmin, tmax + 1);
        List<Vector3> placedTrees = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            if (!TryTreeSpawnPoint(baseX, baseZ, placedTrees, out Vector3 pos))
                continue;

            float snappedYaw = Random.Range(0, 4) * 90f;
            Quaternion rot = Quaternion.Euler(0f, snappedYaw, 0f);
            GameObject tree = useStylizedRuntimeTrees
                ? CreateStylizedTree(pos, rot, parent)
                : hasTreePrefabs
                    ? SpawnPrefabTree(pos, rot, parent)
                    : CreateRuntimeForestTree(pos, rot, parent);
            if (tree == null)
                continue;

            placedTrees.Add(pos);
            if (addMonsterBlockingToTrees)
                EnsureNavigationBlocker(tree, treeObstacleRadius, treeObstacleHeight);
        }
    }

    bool TryTreeSpawnPoint(float baseX, float baseZ, List<Vector3> placedTrees, out Vector3 pos)
    {
        int attempts = Mathf.Max(1, treeSpawnAttemptsPerTree);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float inset = Mathf.Min(3f, chunkSize * 0.2f);
            float wx = baseX + Random.Range(inset, chunkSize - inset);
            float wz = baseZ + Random.Range(inset, chunkSize - inset);
            if (!TryGroundPoint(wx, wz, out pos) || !IsValidTreeSpawnPoint(pos, placedTrees))
                continue;

            return true;
        }

        pos = default;
        return false;
    }

    bool IsValidTreeSpawnPoint(Vector3 pos, List<Vector3> placedTrees)
    {
        if (avoidWaterForTrees && pos.y <= treeWaterHeight)
            return false;

        if (!IsFarEnoughFromCampfireForTree(pos))
            return false;

        float minDistanceSqr = treeMinimumSpacing * treeMinimumSpacing;
        for (int i = 0; i < placedTrees.Count; i++)
        {
            if (HorizontalDistanceSqr(pos, placedTrees[i]) < minDistanceSqr)
                return false;
        }

        return true;
    }

    bool IsFarEnoughFromCampfireForTree(Vector3 pos)
    {
        Transform camp = ResolveTreeCampfireAvoidCenter();
        if (camp == null || treeCampfireAvoidRadius <= 0f)
            return true;

        float minDistanceSqr = treeCampfireAvoidRadius * treeCampfireAvoidRadius;
        return HorizontalDistanceSqr(pos, camp.position) >= minDistanceSqr;
    }

    Transform ResolveTreeCampfireAvoidCenter()
    {
        if (treeCampfireAvoidCenter != null)
            return treeCampfireAvoidCenter;

        if (_resolvedTreeCampfireAvoidCenter != null)
            return _resolvedTreeCampfireAvoidCenter;

        if (CampLocator.Instance != null)
        {
            _resolvedTreeCampfireAvoidCenter = CampLocator.Instance.transform;
            return _resolvedTreeCampfireAvoidCenter;
        }

        Campfire campfire = FindObjectOfType<Campfire>();
        if (campfire != null)
            _resolvedTreeCampfireAvoidCenter = campfire.transform;

        return _resolvedTreeCampfireAvoidCenter;
    }

    GameObject SpawnPrefabTree(Vector3 pos, Quaternion rot, Transform parent)
    {
        GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
        if (prefab == null)
            return null;

        GameObject tree = Instantiate(prefab, pos, rot, parent);
        float scale = Random.Range(treeScaleRange.x, treeScaleRange.y);
        tree.transform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        TryTagTree(tree);
        return tree;
    }

    GameObject CreateStylizedTree(Vector3 pos, Quaternion rot, Transform parent)
    {
        EnsureStylizedTreeMaterials();

        GameObject root = new GameObject("Stylized Block Tree");
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(pos, rot);
        TryTagTree(root);

        float scale = Mathf.Max(0.1f, Random.Range(treeScaleRange.x, treeScaleRange.y));
        int trunkBlocks = Random.Range(4, 7);
        float block = 0.82f * scale;

        for (int i = 0; i < trunkBlocks; i++)
            CreateTreeBlock(root.transform, "Trunk", new Vector3(0f, block * (0.5f + i), 0f), new Vector3(block * 0.72f, block, block * 0.72f), _stylizedTrunkMaterial);

        float canopyY = block * (trunkBlocks + 0.15f);
        CreateTreeBlock(root.transform, "LeavesCore", new Vector3(0f, canopyY, 0f), new Vector3(block * 2.5f, block * 1.4f, block * 2.5f), _stylizedLeafMaterial);
        CreateTreeBlock(root.transform, "LeavesTop", new Vector3(0f, canopyY + block * 0.95f, 0f), new Vector3(block * 1.7f, block * 1.05f, block * 1.7f), _stylizedLeafMaterial);
        CreateTreeBlock(root.transform, "LeavesNorth", new Vector3(0f, canopyY - block * 0.05f, block * 1.25f), new Vector3(block * 1.65f, block * 1.05f, block * 0.9f), _stylizedLeafDarkMaterial);
        CreateTreeBlock(root.transform, "LeavesSouth", new Vector3(0f, canopyY - block * 0.05f, -block * 1.25f), new Vector3(block * 1.65f, block * 1.05f, block * 0.9f), _stylizedLeafDarkMaterial);
        CreateTreeBlock(root.transform, "LeavesEast", new Vector3(block * 1.25f, canopyY - block * 0.05f, 0f), new Vector3(block * 0.9f, block * 1.05f, block * 1.65f), _stylizedLeafDarkMaterial);
        CreateTreeBlock(root.transform, "LeavesWest", new Vector3(-block * 1.25f, canopyY - block * 0.05f, 0f), new Vector3(block * 0.9f, block * 1.05f, block * 1.65f), _stylizedLeafDarkMaterial);

        return root;
    }

    GameObject CreateRuntimeForestTree(Vector3 pos, Quaternion rot, Transform parent)
    {
        EnsureStylizedTreeMaterials();

        GameObject root = new GameObject("Runtime Forest Tree");
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(pos, rot);
        TryTagTree(root);

        float scale = Mathf.Max(0.1f, Random.Range(treeScaleRange.x, treeScaleRange.y));
        float trunkHeight = Random.Range(2.8f, 4.2f) * scale;
        float trunkRadius = Random.Range(0.18f, 0.28f) * scale;

        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(root.transform, false);
        trunk.transform.localPosition = new Vector3(0f, trunkHeight * 0.5f, 0f);
        trunk.transform.localScale = new Vector3(trunkRadius, trunkHeight * 0.5f, trunkRadius);
        Renderer trunkRenderer = trunk.GetComponent<Renderer>();
        if (trunkRenderer != null)
            trunkRenderer.sharedMaterial = _stylizedTrunkMaterial;
        Collider trunkCollider = trunk.GetComponent<Collider>();
        if (trunkCollider != null)
            Destroy(trunkCollider);

        int canopyCount = Random.Range(2, 4);
        for (int i = 0; i < canopyCount; i++)
        {
            GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Canopy";
            canopy.transform.SetParent(root.transform, false);
            float offsetX = Random.Range(-0.28f, 0.28f) * scale;
            float offsetZ = Random.Range(-0.28f, 0.28f) * scale;
            canopy.transform.localPosition = new Vector3(offsetX, trunkHeight + i * 0.35f * scale, offsetZ);
            float canopyScale = Random.Range(1.15f, 1.65f) * scale;
            canopy.transform.localScale = new Vector3(canopyScale, canopyScale * 0.9f, canopyScale);
            Renderer canopyRenderer = canopy.GetComponent<Renderer>();
            if (canopyRenderer != null)
                canopyRenderer.sharedMaterial = i == 0 ? _stylizedLeafDarkMaterial : _stylizedLeafMaterial;
            Collider canopyCollider = canopy.GetComponent<Collider>();
            if (canopyCollider != null)
                Destroy(canopyCollider);
        }

        return root;
    }

    void CreateTreeBlock(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localRotation = Quaternion.identity;
        block.transform.localScale = localScale;

        Collider col = block.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        Renderer rendererComponent = block.GetComponent<Renderer>();
        if (rendererComponent != null)
            rendererComponent.sharedMaterial = material;
    }

    void EnsureStylizedTreeMaterials()
    {
        if (_stylizedTrunkMaterial == null)
        {
            _stylizedTrunkMaterial = new Material(ResolveTreeShader());
            _stylizedTrunkMaterial.name = "Runtime_BlockTree_Trunk";
            _stylizedTrunkMaterial.color = new Color(0.36f, 0.2f, 0.09f, 1f);
        }
        if (_stylizedLeafMaterial == null)
        {
            _stylizedLeafMaterial = new Material(ResolveTreeShader());
            _stylizedLeafMaterial.name = "Runtime_BlockTree_Leaves";
            _stylizedLeafMaterial.color = new Color(0.12f, 0.42f, 0.16f, 1f);
        }
        if (_stylizedLeafDarkMaterial == null)
        {
            _stylizedLeafDarkMaterial = new Material(ResolveTreeShader());
            _stylizedLeafDarkMaterial.name = "Runtime_BlockTree_Leaves_Dark";
            _stylizedLeafDarkMaterial.color = new Color(0.07f, 0.28f, 0.12f, 1f);
        }
    }

    Shader ResolveTreeShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Diffuse");
        return shader;
    }

    void TryTagTree(GameObject tree)
    {
        if (tree == null)
            return;

        try
        {
            tree.tag = "tree";
        }
        catch (UnityException)
        {
            // The monster can still use NavMesh obstacles if the optional tree tag is not present.
        }
    }

    void SpawnGrass(Transform parent, float baseX, float baseZ)
    {
        bool canUseFallback = grassPrefab == null && useRuntimeGrassFallback;
        if ((!canUseFallback && grassPrefab == null) || grassPerChunkMax <= 0)
            return;

        int gmin = Mathf.Min(grassPerChunkMin, grassPerChunkMax);
        int gmax = Mathf.Max(grassPerChunkMin, grassPerChunkMax);
        int count = Random.Range(gmin, gmax + 1);

        // Fewer grass instances when low-world-quality is selected.
        if (useGlobalStreamingQuality && WorldStreamSettings.LoadQualityIndex() == 0)
            count = Mathf.Min(count, 2);

        for (int i = 0; i < count; i++)
        {
            float wx = baseX + Random.Range(0f, chunkSize);
            float wz = baseZ + Random.Range(0f, chunkSize);
            if (!TryGroundPoint(wx, wz, out Vector3 pos))
                continue;

            if (grassPrefab != null)
            {
                GameObject grass = Instantiate(grassPrefab, pos + Vector3.up * 0.02f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
                OptimizeGrassObject(grass);
            }
            else
                CreateRuntimeGrassPatch(pos + Vector3.up * 0.02f, parent);
        }
    }

    void SpawnBunnies(Transform parent, float baseX, float baseZ)
    {
        int bmin = Mathf.Min(bunniesPerChunkMin, bunniesPerChunkMax);
        int bmax = Mathf.Max(bunniesPerChunkMin, bunniesPerChunkMax);
        int count = Random.Range(Mathf.Max(0, bmin), Mathf.Max(0, bmax) + 1);
        if (count == 0 && Random.value <= bunnySpawnChancePerChunk)
            count = 1;

        for (int i = 0; i < count; i++)
        {
            if (Random.value > bunnySpawnChancePerChunk && i >= bunniesPerChunkMin)
                continue;

            float wx = baseX + Random.Range(2f, chunkSize - 2f);
            float wz = baseZ + Random.Range(2f, chunkSize - 2f);
            if (!TryGroundPoint(wx, wz, out Vector3 pos))
                continue;

            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject bunny = CreateBunnyInstance(pos, rotation, parent, out bool usedAssignedPrefab);
            EnsureBunnySetup(bunny, usedAssignedPrefab);
        }
    }

    IEnumerator SpawnCampBunniesWhenGroundReady()
    {
        for (int frame = 0; frame < 600; frame++)
        {
            if (TrySpawnCampBunnies())
                yield break;

            yield return null;
        }
    }

    bool TrySpawnCampBunnies()
    {
        if (_campBunniesSpawned || campBunnyCount <= 0)
            return true;

        Transform center = ResolveCampBunnyCenter();
        if (center == null)
            return false;

        GameObject root = new GameObject("CampEdgeBunnies");
        root.transform.SetParent(transform, false);

        int spawned = 0;
        int attempts = Mathf.Max(campBunnySpawnAttempts, campBunnyCount);
        float inner = Mathf.Max(0f, campBunnyInnerRadius);
        float outer = Mathf.Max(inner + 1f, campBunnyOuterRadius);

        for (int i = 0; i < attempts && spawned < campBunnyCount; i++)
        {
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.001f)
                dir = Vector2.right;
            dir.Normalize();

            float radius = Random.Range(inner, outer);
            float wx = center.position.x + dir.x * radius;
            float wz = center.position.z + dir.y * radius;
            if (!TryGroundPoint(wx, wz, out Vector3 pos))
                continue;

            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject bunny = CreateBunnyInstance(pos, rotation, root.transform, out bool usedAssignedPrefab);
            EnsureBunnySetup(bunny, usedAssignedPrefab);
            spawned++;
        }

        if (spawned <= 0)
        {
            Destroy(root);
            return false;
        }

        _campBunniesSpawned = true;
        return true;
    }

    Transform ResolveCampBunnyCenter()
    {
        Transform camp = ResolveTreeCampfireAvoidCenter();
        if (camp != null)
            return camp;

        Transform woodCamp = ResolveCampfireAvoidCenter();
        if (woodCamp != null)
            return woodCamp;

        return player;
    }

    void CreateRuntimeGrassPatch(Vector3 position, Transform parent)
    {
        EnsureRuntimeGrassMaterial();

        GameObject root = new GameObject("Runtime Grass Patch");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        int blades = Random.Range(2, 4);
        for (int i = 0; i < blades; i++)
        {
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Quad);
            blade.name = "GrassBlade";
            blade.transform.SetParent(root.transform, false);
            blade.transform.localPosition = new Vector3(Random.Range(-0.12f, 0.12f), 0.12f, Random.Range(-0.12f, 0.12f));
            blade.transform.localRotation = Quaternion.Euler(0f, (180f / Mathf.Max(1, blades)) * i, 0f);
            float height = Random.Range(0.28f, 0.46f);
            blade.transform.localScale = new Vector3(Random.Range(0.12f, 0.2f), height, 1f);
            Renderer rendererComponent = blade.GetComponent<Renderer>();
            if (rendererComponent != null)
                rendererComponent.sharedMaterial = _runtimeGrassMaterial;
            Collider colliderComponent = blade.GetComponent<Collider>();
            if (colliderComponent != null)
                Destroy(colliderComponent);
        }
    }

    GameObject CreateBunnyInstance(Vector3 position, Quaternion rotation, Transform parent, out bool usedAssignedPrefab)
    {
        usedAssignedPrefab = false;

        if (bunnyPrefab != null)
        {
            try
            {
                Object spawned = Instantiate((Object)bunnyPrefab, position, rotation, parent);
                if (spawned is GameObject spawnedObject)
                {
                    usedAssignedPrefab = true;
                    return spawnedObject;
                }
                if (spawned is Component spawnedComponent)
                {
                    usedAssignedPrefab = true;
                    return spawnedComponent.gameObject;
                }

                if (spawned != null)
                    Destroy(spawned);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"InfiniteForestWorld: Assigned bunny prefab could not be spawned, using runtime bunny instead. {ex.Message}");
            }
        }

        return CreateRuntimeBunny(position, rotation, parent);
    }

    GameObject CreateRuntimeBunny(Vector3 position, Quaternion rotation, Transform parent)
    {
        GameObject bunny = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bunny.name = "Runtime Bunny";
        bunny.transform.SetPositionAndRotation(position, rotation);
        bunny.transform.SetParent(parent, true);
        bunny.transform.localScale = new Vector3(0.35f, 0.35f, 0.55f);

        Renderer rendererComponent = bunny.GetComponent<Renderer>();
        if (rendererComponent != null)
            rendererComponent.material.color = new Color(0.78f, 0.72f, 0.62f);

        return bunny;
    }

    void SpawnWood(Transform parent, float baseX, float baseZ)
    {
        if (woodPrefab == null)
            return;

        int wmin = Mathf.Min(woodPerChunkMin, woodPerChunkMax);
        int wmax = Mathf.Max(woodPerChunkMin, woodPerChunkMax);
        int count = Random.Range(wmin, wmax + 1);
        for (int i = 0; i < count; i++)
        {
            if (TryWoodSpawnPoint(baseX, baseZ, out Vector3 pos))
            {
                GameObject wood = Instantiate(woodPrefab, pos + Vector3.up * 0.05f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
                wood.name = $"Spawned Wood {parent.childCount}";
                _spawnedWoodPositions.Add(pos);
                if (addMonsterBlockingToWood)
                    EnsureNavigationBlocker(wood, 0.55f, 0.65f);
            }
        }
    }

    void OptimizeChunkDecorations(GameObject chunkRoot)
    {
        if (chunkRoot == null)
            return;

        Renderer[] renderers = chunkRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            bool isGrass = r.transform.name.ToLowerInvariant().Contains("grass");
            if (disableGrassShadows && isGrass)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
    }

    void OptimizeGrassObject(GameObject grass)
    {
        if (grass == null)
            return;

        if (removeGrassPrefabColliders)
        {
            Collider[] colliders = grass.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    Destroy(colliders[i]);
            }
        }

        if (!disableGrassShadows)
            return;

        Renderer[] renderers = grass.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
        }
    }

    void EnsureNavigationBlocker(GameObject obj, float fallbackRadius, float fallbackHeight)
    {
        if (obj == null)
            return;

        Collider existingCollider = obj.GetComponentInChildren<Collider>();
        if (existingCollider == null)
        {
            CapsuleCollider col = obj.AddComponent<CapsuleCollider>();
            col.radius = Mathf.Max(0.05f, fallbackRadius);
            col.height = Mathf.Max(col.radius * 2f, fallbackHeight);
            col.center = new Vector3(0f, col.height * 0.5f, 0f);
            col.direction = 1;
            existingCollider = col;
        }
        else
        {
            existingCollider.isTrigger = false;
        }

        if (!addNavMeshCarvingToSpawnedObjects)
            return;

        NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
        if (obstacle == null)
            obstacle = obj.AddComponent<NavMeshObstacle>();

        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.radius = Mathf.Max(0.05f, fallbackRadius);
        obstacle.height = Mathf.Max(obstacle.radius * 2f, fallbackHeight);
        obstacle.center = new Vector3(0f, obstacle.height * 0.5f, 0f);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        obstacle.carvingMoveThreshold = 0.1f;
        obstacle.carvingTimeToStationary = 0.1f;
    }

    bool TryWoodSpawnPoint(float baseX, float baseZ, out Vector3 pos)
    {
        int attempts = Mathf.Max(1, woodSpawnAttemptsPerItem);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float wx = baseX + Random.Range(1f, chunkSize - 1f);
            float wz = baseZ + Random.Range(1f, chunkSize - 1f);
            if (TryGroundPoint(wx, wz, out pos) && IsValidWoodSpawnPoint(pos))
                return true;
        }

        pos = default;
        return false;
    }

    bool IsValidWoodSpawnPoint(Vector3 pos)
    {
        return IsDryEnoughForWood(pos) && IsFarEnoughFromCampfire(pos) && IsFarEnoughFromOtherWood(pos);
    }

    bool IsDryEnoughForWood(Vector3 pos)
    {
        if (!avoidWaterForWood)
            return true;

        return pos.y > woodWaterHeight + woodWaterEdgePadding;
    }

    bool IsFarEnoughFromCampfire(Vector3 pos)
    {
        Transform camp = ResolveCampfireAvoidCenter();
        if (camp == null || woodCampfireAvoidRadius <= 0f)
            return true;

        float minDistanceSqr = woodCampfireAvoidRadius * woodCampfireAvoidRadius;
        return HorizontalDistanceSqr(pos, camp.position) >= minDistanceSqr;
    }

    Transform ResolveCampfireAvoidCenter()
    {
        if (woodCampfireAvoidCenter != null)
            return woodCampfireAvoidCenter;

        if (_resolvedCampfireAvoidCenter != null)
            return _resolvedCampfireAvoidCenter;

        if (CampLocator.Instance != null)
        {
            _resolvedCampfireAvoidCenter = CampLocator.Instance.transform;
            return _resolvedCampfireAvoidCenter;
        }

        Campfire campfire = FindObjectOfType<Campfire>();
        if (campfire != null)
            _resolvedCampfireAvoidCenter = campfire.transform;

        return _resolvedCampfireAvoidCenter;
    }

    bool IsFarEnoughFromOtherWood(Vector3 pos)
    {
        if (woodMinimumSpacing <= 0f)
            return true;

        float minDistanceSqr = woodMinimumSpacing * woodMinimumSpacing;
        for (int i = 0; i < _spawnedWoodPositions.Count; i++)
        {
            if (HorizontalDistanceSqr(pos, _spawnedWoodPositions[i]) < minDistanceSqr)
                return false;
        }

        return true;
    }

    float HorizontalDistanceSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    bool TryGroundPoint(float wx, float wz, out Vector3 groundPos)
    {
        Vector3 origin = new Vector3(wx, raycastStartHeight, wz);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, raycastMaxDistance, groundMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider != null && IsGeneratedChunkTransform(hit.collider.transform))
                continue;

            groundPos = hit.point;
            return true;
        }

        groundPos = default;
        return false;
    }

    bool IsGeneratedChunkTransform(Transform candidate)
    {
        Transform t = candidate;
        while (t != null && t != transform)
        {
            if (t.name.StartsWith("ForestChunk_", System.StringComparison.Ordinal))
                return true;
            t = t.parent;
        }
        return false;
    }

    void EnsureRuntimeGrassMaterial()
    {
        if (_runtimeGrassMaterial != null)
            return;

        _runtimeGrassMaterial = new Material(ResolveTreeShader());
        _runtimeGrassMaterial.name = "Runtime_Grass";
        _runtimeGrassMaterial.color = new Color(0.18f, 0.46f, 0.16f, 1f);
    }

    void RepairSpawnSettingsIfNeeded()
    {
        if (!autoRepairEmptySpawnSettings)
            return;

        if ((treePrefabs == null || treePrefabs.Length == 0) && !useStylizedRuntimeTrees)
            useRuntimeTreeFallback = true;

        if (treesPerChunkMax <= 0)
        {
            treesPerChunkMin = 18;
            treesPerChunkMax = 30;
        }

        if (grassPrefab == null)
        {
            useRuntimeGrassFallback = true;
            if (grassPerChunkMax <= 2)
            {
                grassPerChunkMin = 3;
                grassPerChunkMax = 8;
            }
        }

        if (bunniesPerChunkMax <= 0)
        {
            bunniesPerChunkMin = 1;
            bunniesPerChunkMax = 1;
        }

        bunniesPerChunkMin = Mathf.Max(1, bunniesPerChunkMin);
        bunniesPerChunkMax = Mathf.Max(bunniesPerChunkMin, bunniesPerChunkMax);

        if (bunnySpawnChancePerChunk < 0.55f)
            bunnySpawnChancePerChunk = 0.75f;

        campBunnyCount = Mathf.Max(4, campBunnyCount);
        campBunnyInnerRadius = Mathf.Max(10f, campBunnyInnerRadius);
        campBunnyOuterRadius = Mathf.Max(campBunnyInnerRadius + 4f, campBunnyOuterRadius);
    }

    void EnsureBunnySetup(GameObject bunny, bool usedAssignedPrefab)
    {
        if (bunny == null)
            return;

        if (!bunny.activeSelf)
            bunny.SetActive(true);

        Rigidbody rb = bunny.GetComponent<Rigidbody>();
        if (rb == null)
            rb = bunny.AddComponent<Rigidbody>();

        rb.mass = 1f;
        rb.drag = 0f;
        rb.angularDrag = 0.05f;
        rb.isKinematic = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        CapsuleCollider capsule = bunny.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = bunny.AddComponent<CapsuleCollider>();

        capsule.isTrigger = false;
        capsule.radius = 0.22f;
        capsule.height = 0.5f;
        capsule.center = new Vector3(0f, 0.25f, 0f);
        capsule.direction = 1;

        BunnyAI ai = bunny.GetComponent<BunnyAI>();
        if (ai == null)
            ai = bunny.AddComponent<BunnyAI>();
        ai.useGeneratedVisuals = !usedAssignedPrefab;
        ai.enabled = true;

        if (!usedAssignedPrefab)
        {
            BunnyVisuals visuals = bunny.GetComponent<BunnyVisuals>();
            if (visuals == null)
                visuals = bunny.AddComponent<BunnyVisuals>();
            visuals.EnsureVisuals();
        }
        else
        {
            BunnyVisuals visuals = bunny.GetComponent<BunnyVisuals>();
            if (visuals != null)
            {
                visuals.RemoveGeneratedVisualsAndRestorePrefabRenderers();
                visuals.enabled = false;
            }
        }

        BunnyHealth health = bunny.GetComponent<BunnyHealth>();
        if (health == null)
            health = bunny.AddComponent<BunnyHealth>();

        if (health.foodPrefab == null)
            health.foodPrefab = bunnyFoodPrefab;
    }

    void OnDestroy()
    {
        if (_dayNightCycle != null)
            _dayNightCycle.OnDayStarted -= OnDayStarted;

        if (_stylizedTrunkMaterial != null)
            Destroy(_stylizedTrunkMaterial);
        if (_stylizedLeafMaterial != null)
            Destroy(_stylizedLeafMaterial);
        if (_stylizedLeafDarkMaterial != null)
            Destroy(_stylizedLeafDarkMaterial);
        if (_runtimeGrassMaterial != null)
            Destroy(_runtimeGrassMaterial);
    }
}
