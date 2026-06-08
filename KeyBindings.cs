using System.Collections.Generic;
using UnityEngine;

public static class KeyBindings
{
    static Dictionary<string, KeyCode> map = new Dictionary<string, KeyCode>();

    public static void SetKey(string action, KeyCode key)
    {
        if (map.ContainsKey(action)) map[action] = key;
        else map.Add(action, key);
    }

    public static KeyCode GetKey(string action)
    {
        if (map.TryGetValue(action, out KeyCode k)) return k;
        return KeyCode.None;
    }
}

