using UnityEngine;

/// <summary>
/// Place on your camp / campfire root so the minimap and distance readout know where "camp" is.
/// Only one active instance is used (first wins unless you replace it).
/// </summary>
public class CampLocator : MonoBehaviour
{
    public static CampLocator Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Instance = null;
    }

    public static Vector3 CampPosition => Instance != null ? Instance.transform.position : Vector3.zero;

    public static bool HasCamp => Instance != null;

    void OnEnable()
    {
        if (Instance == null)
            Instance = this;
    }

    void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
