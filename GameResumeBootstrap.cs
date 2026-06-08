using UnityEngine;

/// <summary>
/// Place in the gameplay scene once. Assign the same weapon prefabs you use for pickups / starter so resume can rebuild inventory.
/// Also ensures the player object is enabled on scene start.
/// </summary>
public class GameResumeBootstrap : MonoBehaviour
{
    [Tooltip("Prefabs whose Weapon.weaponName matches what was saved (order does not matter).")]
    public GameObject[] weaponPrefabsForResume;

    void Awake()
    {
        RunStatePersistence.SetWeaponResumeCatalog(weaponPrefabsForResume);
        
        // Ensure player is enabled on scene start
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            // Fallback: find by controller component
            controller ctrl = FindObjectOfType<controller>();
            if (ctrl != null)
                player = ctrl.gameObject;
        }
        
        if (player != null)
        {
            if (!player.activeSelf)
            {
                player.SetActive(true);
                Debug.Log("GameResumeBootstrap: Enabled disabled player object");
            }
            else
            {
                Debug.Log("GameResumeBootstrap: Player object is already active");
            }
        }
        else
        {
            Debug.LogError("GameResumeBootstrap: No GameObject with tag 'Player' or controller component found!");
        }
    }
}
