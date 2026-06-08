using System.Collections;
using UnityEngine;

public class CraftingTableAutoSetup : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateRuntimeSetup()
    {
        GameObject go = new GameObject("CraftingTableAutoSetup");
        go.AddComponent<CraftingTableAutoSetup>();
    }

    IEnumerator Start()
    {
        for (int i = 0; i < 12; i++)
        {
            BindTables();
            yield return new WaitForSeconds(0.25f);
        }
    }

    void BindTables()
    {
        GameObject[] objects = FindObjectsOfType<GameObject>(true);
        foreach (GameObject obj in objects)
        {
            if (obj == null || !LooksLikeCraftingTable(obj.name))
                continue;

            if (obj.GetComponentInParent<CraftingTable>() == null)
                obj.AddComponent<CraftingTable>();

            EnsureCollider(obj);
        }
    }

    bool LooksLikeCraftingTable(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        string lower = objectName.ToLowerInvariant();
        return lower.Contains("crafting table")
            || lower.Contains("craftingtable")
            || lower.Contains("craft table")
            || lower.Contains("workbench")
            || lower == "table"
            || lower.Contains(" table");
    }

    void EnsureCollider(GameObject obj)
    {
        if (obj.GetComponentInChildren<Collider>() != null)
            return;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(2f, 1f, 1.2f);
        collider.center = new Vector3(0f, 0.5f, 0f);
    }
}
