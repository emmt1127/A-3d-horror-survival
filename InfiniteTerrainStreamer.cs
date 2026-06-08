using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Streams Unity Terrain tiles around the player using procedural height (continuous across chunk borders).
/// Match <see cref="chunkWorldSize"/> with <see cref="InfiniteForestWorld.chunkSize"/> for best results.
/// </summary>
public class InfiniteTerrainStreamer : MonoBehaviour
{
    [Header("Player")]
    public Transform player;
    public bool autoFindPlayer = true;

    [Header("Chunks")]
    [Tooltip("World width/depth of each terrain tile (meters).")]
    public float chunkWorldSize = 64f;
    [Tooltip("Builds one fixed map at startup instead of streaming chunks while the player moves.")]
    public bool buildFixedMapAtStart = true;
    public int fixedMapChunksX = 8;
    public int fixedMapChunksZ = 4;
    [Tooltip("When Use Global Quality is on, this is the max radius cap. When off, this is the exact radius.")]
    public int loadRadius = 1;
    public bool useGlobalStreamingQuality = true;
    [Tooltip("Limits expensive terrain generation per frame to reduce movement hitching.")]
    public int maxChunkSpawnsPerFrame = 12;
    [Tooltip("Higher values make terrain render cheaper and chunkier, closer to Minecraft-style performance.")]
    public float terrainPixelError = 24f;
    public float terrainBasemapDistance = 70f;
    [Tooltip("Heightmap resolution per axis (power of two + 1, e.g. 33, 65, 129). Lower = faster.")]
    public int heightmapResolution = 33;
    [Tooltip("Maximum terrain height in meters (SetHeights are normalized by this).")]
    public float terrainMaxHeight = 60f;
    public int terrainSeed = 42;

    [Header("Shape")]
    public float noiseScale = 0.028f;
    [Range(1, 6)]
    public int noiseOctaves = 4;
    public float octavePersistence = 0.48f;
    public float baseHeightFraction = 0.12f;

    [Header("Look (optional)")]
    public bool use2DTerrain = false;
    [Tooltip("When true, uses 2D terrain with splat maps like Unity's terrain painter. When false, uses 3D terrain with heightmap.")]
    public Color groundTint = new Color(0.22f, 0.42f, 0.2f);
    [Tooltip("Material for the terrain floor. If null, uses groundTint color.")]
    public Material terrainMaterial;
    [Tooltip("Layer for generated terrains (create e.g. Ground in Tags & Layers).")]
    public int terrainLayer = 0;

    [Header("Boundary Walls")]
    public bool createBoundaryWalls = true;
    public float boundaryWallHeight = 80f;
    public float boundaryWallThickness = 3f;
    public Color boundaryWallColor = new Color(0.28f, 0.28f, 0.28f, 1f);
    
    [Header("2D Terrain (only when use2DTerrain is true)")]
    [Tooltip("Terrain layers for 2D terrain painting (like Unity's terrain painter).")]
    public TerrainLayer[] terrainLayers;
    [Tooltip("Resolution of the splat map for texture blending (higher = more detailed).")]
    public int splatMapResolution = 512;

    readonly Dictionary<Vector2Int, TerrainChunk> _active = new Dictionary<Vector2Int, TerrainChunk>();
    readonly Queue<Vector2Int> _pendingSpawns = new Queue<Vector2Int>();
    readonly HashSet<Vector2Int> _queuedSpawns = new HashSet<Vector2Int>();
    readonly HashSet<Vector2Int> _wantedChunks = new HashSet<Vector2Int>();
    Vector2Int _lastChunk;
    int _cachedEffectiveRadius = int.MinValue;
    GameObject _boundaryRoot;

    public bool IsLoading => _pendingSpawns.Count > 0 || _queuedSpawns.Count > 0;
    public float LoadingProgress01
    {
        get
        {
            int wanted = Mathf.Max(1, _wantedChunks.Count);
            int loaded = 0;
            foreach (Vector2Int id in _wantedChunks)
            {
                if (_active.ContainsKey(id))
                    loaded++;
            }
            return Mathf.Clamp01(loaded / (float)wanted);
        }
    }

    struct TerrainChunk
    {
        public GameObject Root;
        public TerrainData Data;
        public List<Object> OwnedObjects;
    }

    void Awake()
    {
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
        if (!Application.isPlaying)
            return;

        if (player != null)
            _lastChunk = WorldToChunk(player.position);
        if (buildFixedMapAtStart)
            BuildFixedTerrainMap();
        else
            RefreshTerrain();
        ProcessSpawnQueue();
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || player == null)
            return;

        if (buildFixedMapAtStart)
        {
            ProcessSpawnQueue();
            return;
        }

        int eff = EffectiveLoadRadius;
        if (eff != _cachedEffectiveRadius)
        {
            _cachedEffectiveRadius = eff;
            RefreshTerrain();
        }

        Vector2Int current = WorldToChunk(player.position);
        if (current != _lastChunk)
        {
            _lastChunk = current;
            RefreshTerrain();
        }

        ProcessSpawnQueue();
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

    /// <summary>Approximate surface height at world XZ (for other systems).</summary>
    public float SampleHeightWorld(float worldX, float worldZ)
    {
        float n = SampleNormalizedHeight(worldX, worldZ);
        return n * terrainMaxHeight;
    }

    public float ChunkWorldSize => chunkWorldSize;

    Vector2Int WorldToChunk(Vector3 world)
    {
        int cx = Mathf.FloorToInt(world.x / chunkWorldSize);
        int cz = Mathf.FloorToInt(world.z / chunkWorldSize);
        return new Vector2Int(cx, cz);
    }

    void RefreshTerrain()
    {
        if (player == null)
            return;

        heightmapResolution = Mathf.Clamp(heightmapResolution, 33, 4097);
        if ((heightmapResolution - 1) % 2 != 0)
            heightmapResolution = Mathf.ClosestPowerOfTwo(heightmapResolution - 1) + 1;

        int r = EffectiveLoadRadius;
        _cachedEffectiveRadius = r;

        HashSet<Vector2Int> keep = new HashSet<Vector2Int>();
        for (int x = -r; x <= r; x++)
        {
            for (int z = -r; z <= r; z++)
            {
                Vector2Int id = _lastChunk + new Vector2Int(x, z);
                keep.Add(id);
                if (!_active.ContainsKey(id))
                    QueueSpawn(id);
            }
        }

        _wantedChunks.Clear();
        foreach (Vector2Int id in keep)
            _wantedChunks.Add(id);

        List<Vector2Int> remove = new List<Vector2Int>();
        foreach (var kv in _active)
        {
            if (!keep.Contains(kv.Key))
                remove.Add(kv.Key);
        }

        foreach (Vector2Int id in remove)
            DespawnChunk(id);

        RefreshAllNeighbors();
    }

    void BuildFixedTerrainMap()
    {
        _wantedChunks.Clear();
        _pendingSpawns.Clear();
        _queuedSpawns.Clear();

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
                if (!_active.ContainsKey(id))
                    QueueSpawn(id);
            }
        }

        if (createBoundaryWalls)
            CreateBoundaryWallsForWantedChunks();
    }

    void QueueSpawn(Vector2Int id)
    {
        if (_queuedSpawns.Contains(id))
            return;

        _queuedSpawns.Add(id);
        _pendingSpawns.Enqueue(id);
    }

    void ProcessSpawnQueue()
    {
        int budget = Mathf.Max(1, maxChunkSpawnsPerFrame);
        bool changed = false;

        for (int i = 0; i < budget && _pendingSpawns.Count > 0; i++)
        {
            Vector2Int id = _pendingSpawns.Dequeue();
            _queuedSpawns.Remove(id);

            if (_active.ContainsKey(id) || !IsChunkInCurrentRadius(id))
                continue;

            SpawnChunk(id);
            changed = true;
        }

        if (changed)
            RefreshAllNeighbors();
    }

    bool IsChunkInCurrentRadius(Vector2Int id)
    {
        if (buildFixedMapAtStart)
            return _wantedChunks.Contains(id);

        int r = EffectiveLoadRadius;
        return Mathf.Abs(id.x - _lastChunk.x) <= r && Mathf.Abs(id.y - _lastChunk.y) <= r;
    }

    void CreateBoundaryWallsForWantedChunks()
    {
        if (_wantedChunks.Count == 0)
            return;

        if (_boundaryRoot != null)
            Destroy(_boundaryRoot);

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minZ = int.MaxValue;
        int maxZ = int.MinValue;
        foreach (Vector2Int id in _wantedChunks)
        {
            minX = Mathf.Min(minX, id.x);
            maxX = Mathf.Max(maxX, id.x);
            minZ = Mathf.Min(minZ, id.y);
            maxZ = Mathf.Max(maxZ, id.y);
        }

        float worldMinX = minX * chunkWorldSize;
        float worldMaxX = (maxX + 1) * chunkWorldSize;
        float worldMinZ = minZ * chunkWorldSize;
        float worldMaxZ = (maxZ + 1) * chunkWorldSize;
        float width = worldMaxX - worldMinX;
        float depth = worldMaxZ - worldMinZ;
        float wallHeight = Mathf.Max(2f, boundaryWallHeight);
        float wallThickness = Mathf.Max(0.5f, boundaryWallThickness);

        _boundaryRoot = new GameObject("FixedMapBoundaryWalls");
        _boundaryRoot.transform.SetParent(transform, false);

        Shader wallShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material wallMaterial = wallShader != null ? new Material(wallShader) : null;
        if (wallMaterial != null)
            wallMaterial.color = boundaryWallColor;

        CreateBoundaryWall("NorthWall", new Vector3((worldMinX + worldMaxX) * 0.5f, wallHeight * 0.5f, worldMaxZ + wallThickness * 0.5f), new Vector3(width + wallThickness * 2f, wallHeight, wallThickness), wallMaterial);
        CreateBoundaryWall("SouthWall", new Vector3((worldMinX + worldMaxX) * 0.5f, wallHeight * 0.5f, worldMinZ - wallThickness * 0.5f), new Vector3(width + wallThickness * 2f, wallHeight, wallThickness), wallMaterial);
        CreateBoundaryWall("EastWall", new Vector3(worldMaxX + wallThickness * 0.5f, wallHeight * 0.5f, (worldMinZ + worldMaxZ) * 0.5f), new Vector3(wallThickness, wallHeight, depth), wallMaterial);
        CreateBoundaryWall("WestWall", new Vector3(worldMinX - wallThickness * 0.5f, wallHeight * 0.5f, (worldMinZ + worldMaxZ) * 0.5f), new Vector3(wallThickness, wallHeight, depth), wallMaterial);
    }

    void CreateBoundaryWall(string wallName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.SetParent(_boundaryRoot.transform, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        Renderer rendererComponent = wall.GetComponent<Renderer>();
        if (rendererComponent != null)
            rendererComponent.sharedMaterial = material;
    }

    void SpawnChunk(Vector2Int id)
    {
        List<Object> ownedObjects = new List<Object>();
        TerrainData data = new TerrainData
        {
            heightmapResolution = heightmapResolution,
            size = new Vector3(chunkWorldSize, use2DTerrain ? 1f : terrainMaxHeight, chunkWorldSize),
            alphamapResolution = use2DTerrain ? splatMapResolution : 48
        };

        if (use2DTerrain)
        {
            // Create flat terrain for 2D mode
            int res = data.heightmapResolution;
            float[,] heights = new float[res, res];
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                    heights[z, x] = 0f;
            }
            data.SetHeights(0, 0, heights);
            
            // Apply splat map for texture blending
            ApplySplatMap(data, id);
        }
        else
        {
            // 3D mode with heightmap
            BuildHeightmap(id, data);
            ApplySingleLayerTint(data, groundTint, ownedObjects);
        }

        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = $"TerrainChunk_{id.x}_{id.y}";
        go.layer = terrainLayer;
        go.transform.SetParent(transform);

        float ox = id.x * chunkWorldSize;
        float oz = id.y * chunkWorldSize;
        go.transform.position = new Vector3(ox, 0f, oz);

        Terrain terrain = go.GetComponent<Terrain>();
        if (terrain != null)
        {
            terrain.heightmapPixelError = Mathf.Max(5f, terrainPixelError);
            terrain.basemapDistance = Mathf.Max(10f, terrainBasemapDistance);
            terrain.detailObjectDistance = 0f;
            terrain.treeDistance = 0f;
            terrain.drawInstanced = true;
        }

        _active[id] = new TerrainChunk { Root = go, Data = data, OwnedObjects = ownedObjects };
    }

    void DespawnChunk(Vector2Int id)
    {
        if (!_active.TryGetValue(id, out TerrainChunk chunk))
            return;

        if (chunk.Root != null)
            Destroy(chunk.Root);
        if (chunk.Data != null)
            Destroy(chunk.Data);
        if (chunk.OwnedObjects != null)
        {
            foreach (Object owned in chunk.OwnedObjects)
            {
                if (owned != null)
                    Destroy(owned);
            }
        }

        _active.Remove(id);
    }

    void BuildHeightmap(Vector2Int chunkId, TerrainData data)
    {
        int res = data.heightmapResolution;
        float[,] heights = new float[res, res];
        float ox = chunkId.x * chunkWorldSize;
        float oz = chunkId.y * chunkWorldSize;

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float wx = ox + (x / (float)(res - 1)) * chunkWorldSize;
                float wz = oz + (z / (float)(res - 1)) * chunkWorldSize;
                heights[z, x] = SampleNormalizedHeight(wx, wz);
            }
        }

        data.SetHeights(0, 0, heights);
    }

    float SampleNormalizedHeight(float worldX, float worldZ)
    {
        float amp = 1f;
        float freq = noiseScale;
        float sum = 0f;
        float totalAmp = 0f;
        float ox = terrainSeed * 73.19f;
        float oz = terrainSeed * 109.37f;

        for (int o = 0; o < noiseOctaves; o++)
        {
            float nx = (worldX + ox) * freq;
            float nz = (worldZ + oz) * freq;
            float n = Mathf.PerlinNoise(nx, nz);
            sum += (n - 0.5f) * amp;
            totalAmp += amp;
            amp *= octavePersistence;
            freq *= 2.02f;
        }

        float normalizedNoise = totalAmp > 0f ? sum / totalAmp : 0f;
        return Mathf.Clamp01(baseHeightFraction + normalizedNoise * 0.35f);
    }

    static Texture2D CreateSolidTexture(Color color, int size = 32)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = $"RuntimeTerrainTint_{ColorUtility.ToHtmlStringRGB(color)}";
        Color[] cols = new Color[size * size];
        for (int i = 0; i < cols.Length; i++)
            cols[i] = color;
        tex.SetPixels(cols);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        return tex;
    }

    void ApplySingleLayerTint(TerrainData data, Color tint, List<Object> ownedObjects)
    {
        Texture2D tintTexture = CreateSolidTexture(tint);
        TerrainLayer layer = new TerrainLayer
        {
            diffuseTexture = tintTexture,
            tileSize = new Vector2(16f, 16f)
        };
        layer.name = "RuntimeTerrainLayer";
        ownedObjects.Add(tintTexture);
        ownedObjects.Add(layer);

        // Use custom material if provided
        if (terrainMaterial != null)
        {
            if (terrainMaterial.mainTexture is Texture2D diffuseTexture)
                layer.diffuseTexture = diffuseTexture;

            if (terrainMaterial.HasProperty("_BumpMap"))
            {
                Texture bumpMap = terrainMaterial.GetTexture("_BumpMap");
                if (bumpMap is Texture2D bumpTexture)
                    layer.normalMapTexture = bumpTexture;
            }

            if (terrainMaterial.HasProperty("_MaskMap"))
            {
                Texture maskMap = terrainMaterial.GetTexture("_MaskMap");
                if (maskMap is Texture2D maskTexture)
                    layer.maskMapTexture = maskTexture;
            }

            if (terrainMaterial.HasProperty("_Smoothness"))
            {
                float smoothness = terrainMaterial.GetFloat("_Smoothness");
                layer.specular = new Color(smoothness, smoothness, smoothness);
            }

            if (terrainMaterial.HasProperty("_Metallic"))
                layer.metallic = terrainMaterial.GetFloat("_Metallic");
        }

        data.terrainLayers = new TerrainLayer[] { layer };

        int aw = data.alphamapWidth;
        int ah = data.alphamapHeight;
        float[,,] map = new float[ah, aw, 1];
        for (int y = 0; y < ah; y++)
        {
            for (int x = 0; x < aw; x++)
                map[y, x, 0] = 1f;
        }

        data.SetAlphamaps(0, 0, map);
    }

    void ApplySplatMap(TerrainData data, Vector2Int chunkId)
    {
        // Use provided terrain layers if available, otherwise create default layer
        if (terrainLayers != null && terrainLayers.Length > 0)
        {
            data.terrainLayers = terrainLayers;
        }
        else
        {
            // Create default layer if none provided
            TerrainLayer layer = new TerrainLayer
            {
                diffuseTexture = CreateSolidTexture(groundTint),
                tileSize = new Vector2(16f, 16f)
            };
            data.terrainLayers = new TerrainLayer[] { layer };
        }

        // Create splat map for texture blending
        int numLayers = data.terrainLayers.Length;
        int aw = data.alphamapWidth;
        int ah = data.alphamapHeight;
        float[,,] splatMap = new float[ah, aw, numLayers];

        // Generate procedural splat map based on noise
        float ox = chunkId.x * chunkWorldSize;
        float oz = chunkId.y * chunkWorldSize;

        for (int y = 0; y < ah; y++)
        {
            for (int x = 0; x < aw; x++)
            {
                float wx = ox + (x / (float)(aw - 1)) * chunkWorldSize;
                float wz = oz + (y / (float)(ah - 1)) * chunkWorldSize;
                
                // Use noise to blend between layers
                float noise = SampleNormalizedHeight(wx, wz);
                
                // Distribute weights across layers based on noise
                for (int layer = 0; layer < numLayers; layer++)
                {
                    float layerThreshold = (float)layer / (numLayers - 1);
                    float blend = 1f - Mathf.Abs(noise - layerThreshold) * numLayers;
                    splatMap[y, x, layer] = Mathf.Clamp01(blend);
                }
            }
        }

        // Normalize splat map so each pixel sums to 1
        for (int y = 0; y < ah; y++)
        {
            for (int x = 0; x < aw; x++)
            {
                float total = 0f;
                for (int layer = 0; layer < numLayers; layer++)
                    total += splatMap[y, x, layer];
                
                if (total > 0f)
                {
                    for (int layer = 0; layer < numLayers; layer++)
                        splatMap[y, x, layer] /= total;
                }
                else
                {
                    splatMap[y, x, 0] = 1f;
                }
            }
        }

        data.SetAlphamaps(0, 0, splatMap);
    }

    void OnDestroy()
    {
        List<Vector2Int> ids = new List<Vector2Int>(_active.Keys);
        foreach (Vector2Int id in ids)
            DespawnChunk(id);
    }

    Terrain TerrainAt(Vector2Int id)
    {
        return _active.TryGetValue(id, out TerrainChunk ch) && ch.Root != null
            ? ch.Root.GetComponent<Terrain>()
            : null;
    }

    void RefreshAllNeighbors()
    {
        foreach (var kv in _active)
        {
            Vector2Int id = kv.Key;
            Terrain center = TerrainAt(id);
            if (center == null)
                continue;

            Terrain left = TerrainAt(id + new Vector2Int(-1, 0));
            Terrain right = TerrainAt(id + new Vector2Int(1, 0));
            Terrain top = TerrainAt(id + new Vector2Int(0, 1));
            Terrain bottom = TerrainAt(id + new Vector2Int(0, -1));
            center.SetNeighbors(left, top, right, bottom);
        }
    }
}
