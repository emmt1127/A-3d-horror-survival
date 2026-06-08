using UnityEngine;

/// <summary>
/// World chunk load radius from quality preset (PlayerPrefs). Used by terrain + forest streamers.
/// 0 = Low (3x3), 1 = Medium (3x3), 2 = High (5x5), 3 = Very high (5x5) - never loads “infinite” at once.
/// </summary>
public static class WorldStreamSettings
{
    public const string PrefKey = "world_stream_quality";
    public const int DefaultQuality = 0;

    /// <summary>0 = low … 3 = very high.</summary>
    public static int LoadQualityIndex() => Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, DefaultQuality), 0, 3);

    public static int ChunkRadiusForQuality(int q)
    {
        switch (Mathf.Clamp(q, 0, 3))
        {
            case 0: return 1;
            case 1: return 1;
            case 2: return 2;
            default: return 2;
        }
    }

    public static void SetQualityIndex(int index)
    {
        PlayerPrefs.SetInt(PrefKey, Mathf.Clamp(index, 0, 3));
        PlayerPrefs.Save();
    }

    public static string QualityLabel(int q)
    {
        switch (Mathf.Clamp(q, 0, 3))
        {
            case 0: return "Low";
            case 1: return "Medium";
            case 2: return "High";
            default: return "Very high";
        }
    }
}
