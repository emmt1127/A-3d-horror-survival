using UnityEngine;

public class TerrainColorPainter : MonoBehaviour
{
    public Terrain targetTerrain;

    // Create a solid color texture
    Texture2D CreateSolidTexture(Color color, int size = 32)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] cols = new Color[size * size];
        for (int i = 0; i < cols.Length; i++) cols[i] = color;
        tex.SetPixels(cols);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        return tex;
    }

    // Create a TerrainLayer asset in memory (not saved to disk)
    TerrainLayer CreateColorLayer(Color color)
    {
        TerrainLayer layer = new TerrainLayer();
        layer.diffuseTexture = CreateSolidTexture(color);
        layer.tileSize = new Vector2(10, 10);
        return layer;
    }

    // Paint a rectangular region (in terrain local alphamap coords) to the given layer index
    // x,y,width,height are in alphamap resolution space (0..alphamapWidth-1)
    public void ApplyColorTile(int x, int y, int width, int height, Color color)
    {
        if (targetTerrain == null) { Debug.LogWarning("No terrain assigned"); return; }

        TerrainData td = targetTerrain.terrainData;
        int alphaW = td.alphamapWidth;
        int alphaH = td.alphamapHeight;

        // Create or append the color layer to terrain layers
        TerrainLayer[] layers = td.terrainLayers;
        TerrainLayer colorLayer = CreateColorLayer(color);

        // Append layer
        TerrainLayer[] newLayers = new TerrainLayer[layers.Length + 1];
        for (int i = 0; i < layers.Length; i++) newLayers[i] = layers[i];
        newLayers[layers.Length] = colorLayer;
        td.terrainLayers = newLayers;
        int colorLayerIndex = newLayers.Length - 1;

        // Get current alphamaps
        float[,,] alphas = td.GetAlphamaps(0, 0, alphaW, alphaH);

        // Clamp rect to alphamap bounds
        int x0 = Mathf.Clamp(x, 0, alphaW - 1);
        int y0 = Mathf.Clamp(y, 0, alphaH - 1);
        int x1 = Mathf.Clamp(x + width, 0, alphaW);
        int y1 = Mathf.Clamp(y + height, 0, alphaH);

        for (int yy = y0; yy < y1; yy++)
        {
            for (int xx = x0; xx < x1; xx++)
            {
                // zero out other layers and set this layer to 1
                for (int li = 0; li < newLayers.Length; li++)
                    alphas[yy, xx, li] = (li == colorLayerIndex) ? 1f : 0f;
            }
        }

        // Apply back
        td.SetAlphamaps(0, 0, alphas);
    }

    // Example test call
    void Start()
    {
        // Example: paint a center rectangle (50x50 in alphamap space) with green
        if (targetTerrain != null)
        {
            int w = targetTerrain.terrainData.alphamapWidth;
            int h = targetTerrain.terrainData.alphamapHeight;
            ApplyColorTile(w/2 - 25, h/2 - 25, 50, 50, Color.green);
        }
    }
}

