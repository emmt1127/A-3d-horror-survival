using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Saves player state when opening Settings from gameplay (ESC) and restores it when returning.
/// Assign weapon prefab catalog on <see cref="GameResumeBootstrap"/> for inventory restore.
/// </summary>
public static class RunStatePersistence
{
    public const string KeyReturnScene = "SettingsReturnScene";
    public const string KeyResumePending = "RunStateResumePending";

    const string Px = "rs_px", Py = "rs_py", Pz = "rs_pz", Ry = "rs_ry";
    const string Hunger = "rs_hunger";
    const string Health = "rs_health";
    const string Wood = "rs_wood";
    const string WoodSackLevel = "rs_wood_sack_level";
    const string Stam = "rs_stamina";
    const string WpnCount = "rs_wpn_count";
    const string WpnActive = "rs_wpn_active";

    static GameObject[] _weaponCatalog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        _weaponCatalog = null;
    }

    public static void SetWeaponResumeCatalog(GameObject[] prefabs) => _weaponCatalog = prefabs;

    public static void SaveFromPlayer(GameObject playerRoot)
    {
        if (playerRoot == null)
            return;

        Transform t = playerRoot.transform;
        PlayerPrefs.SetFloat(Px, t.position.x);
        PlayerPrefs.SetFloat(Py, t.position.y);
        PlayerPrefs.SetFloat(Pz, t.position.z);
        PlayerPrefs.SetFloat(Ry, t.eulerAngles.y);

        PlayerHunger hunger = playerRoot.GetComponentInChildren<PlayerHunger>();
        if (hunger != null)
            PlayerPrefs.SetFloat(Hunger, hunger.GetHungerNormalized());

        Health health = playerRoot.GetComponentInChildren<Health>();
        if (health != null)
            PlayerPrefs.SetFloat(Health, health.maxHealth > 0f ? health.CurrentHealth / health.maxHealth : 1f);

        PlayerWoodCarry wood = playerRoot.GetComponentInChildren<PlayerWoodCarry>();
        if (wood != null)
        {
            PlayerPrefs.SetInt(Wood, wood.Current);
            PlayerPrefs.SetInt(WoodSackLevel, wood.SackLevel);
        }

        controller ctrl = playerRoot.GetComponentInChildren<controller>();
        if (ctrl != null)
            PlayerPrefs.SetFloat(Stam, ctrl.GetStaminaNormalized());

        PlayerInventory inv = playerRoot.GetComponentInChildren<PlayerInventory>();
        if (inv != null)
            SaveWeapons(inv);

        PlayerPrefs.SetString(KeyReturnScene, SceneManager.GetActiveScene().name);
        PlayerPrefs.SetInt(KeyResumePending, 1);
        PlayerPrefs.Save();
    }

    static void SaveWeapons(PlayerInventory inv)
    {
        int n = Mathf.Min(inv.weapons.Count, 10);
        PlayerPrefs.SetInt(WpnCount, n);
        PlayerPrefs.SetInt(WpnActive, inv.activeIndex);
        for (int i = 0; i < n; i++)
        {
            Weapon w = inv.weapons[i];
            PlayerPrefs.SetString($"rs_wpn_{i}", w != null ? w.weaponName : "");
        }
    }

    /// <summary>Call early in player Start. Returns true if a saved run was restored (skip default spawn logic).</summary>
    public static bool TryApplyToPlayer(GameObject playerRoot)
    {
        if (PlayerPrefs.GetInt(KeyResumePending, 0) == 0)
            return false;

        if (playerRoot == null)
            return false;

        CharacterController cc = playerRoot.GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;

        Transform t = playerRoot.transform;
        Vector3 pos = new Vector3(PlayerPrefs.GetFloat(Px), PlayerPrefs.GetFloat(Py), PlayerPrefs.GetFloat(Pz));
        t.position = pos;
        Vector3 e = t.eulerAngles;
        e.y = PlayerPrefs.GetFloat(Ry);
        t.eulerAngles = e;

        if (cc != null)
            cc.enabled = true;

        PlayerHunger hunger = playerRoot.GetComponentInChildren<PlayerHunger>();
        if (hunger != null)
            hunger.SetHungerNormalized(PlayerPrefs.GetFloat(Hunger, 1f));

        Health health = playerRoot.GetComponentInChildren<Health>();
        if (health != null)
            health.SetHealthFraction(PlayerPrefs.GetFloat(Health, 1f));

        PlayerWoodCarry wood = playerRoot.GetComponentInChildren<PlayerWoodCarry>();
        if (wood != null)
        {
            wood.SetSackLevel(PlayerPrefs.GetInt(WoodSackLevel, 1));
            wood.SetCarriedWood(PlayerPrefs.GetInt(Wood, 0));
        }

        controller ctrl = playerRoot.GetComponentInChildren<controller>();
        if (ctrl != null)
            ctrl.SetStaminaNormalized(PlayerPrefs.GetFloat(Stam, 1f));

        PlayerInventory inv = playerRoot.GetComponentInChildren<PlayerInventory>();
        if (inv != null)
            LoadWeapons(inv);

        PlayerPrefs.SetInt(KeyResumePending, 0);
        PlayerPrefs.Save();
        return true;
    }

    static void LoadWeapons(PlayerInventory inv)
    {
        int savedCount = Mathf.Clamp(PlayerPrefs.GetInt(WpnCount, 0), 0, 10);
        if (savedCount == 0)
        {
            inv.ClearWeaponsForResume();
            return;
        }

        if (_weaponCatalog == null || _weaponCatalog.Length == 0)
        {
            inv.ClearWeaponsForResume();
            return;
        }

        inv.ClearWeaponsForResume();
        Transform parent = inv.transform;

        for (int i = 0; i < savedCount; i++)
        {
            string wantName = PlayerPrefs.GetString($"rs_wpn_{i}", "");
            if (string.IsNullOrEmpty(wantName))
                continue;

            GameObject prefab = FindWeaponPrefab(wantName);
            if (prefab == null)
                continue;

            GameObject go = Object.Instantiate(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            Weapon w = go.GetComponent<Weapon>();
            if (w == null)
            {
                Object.Destroy(go);
                continue;
            }

            if (!inv.AddWeapon(w))
                Object.Destroy(go);
        }

        int active = PlayerPrefs.GetInt(WpnActive, 0);
        if (inv.weapons.Count > 0)
            inv.activeIndex = Mathf.Clamp(active, 0, inv.weapons.Count - 1);
        else
            inv.activeIndex = -1;

        inv.MarkWeaponVisualsDirty();
    }

    static GameObject FindWeaponPrefab(string weaponName)
    {
        if (_weaponCatalog == null)
            return null;
        foreach (GameObject p in _weaponCatalog)
        {
            if (p == null)
                continue;
            Weapon w = p.GetComponent<Weapon>();
            if (w != null && w.weaponName == weaponName)
                return p;
        }

        return null;
    }

    public static void ClearReturnSceneOnly()
    {
        PlayerPrefs.DeleteKey(KeyReturnScene);
        PlayerPrefs.Save();
    }
}
