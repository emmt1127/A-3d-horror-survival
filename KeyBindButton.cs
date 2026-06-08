using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class KeyBindButton : MonoBehaviour
{
    public string actionName = "Jump"; // unique action id
    public Button button;
    public Text labelText; // shows action name
    public Text keyText;   // shows current key
    public Color listeningColor = Color.yellow;
    public Color normalColor = Color.white;

    const string PREF_KEY_PREFIX = "keybind_";

    bool isListening = false;

    public void Init()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(StartListening);
        }
        if (labelText != null) labelText.text = actionName;
        LoadAndApply();
    }

    void LoadAndApply()
    {
        string pref = PREF_KEY_PREFIX + actionName;
        int saved = PlayerPrefs.GetInt(pref, (int)KeyCode.None);
        KeyCode kc = (KeyCode)saved;
        if (kc == KeyCode.None)
        {
            // set sensible defaults for common actions
            if (actionName.ToLower().Contains("jump")) kc = KeyCode.Space;
            else if (actionName.ToLower().Contains("interact")) kc = KeyCode.E;
            else kc = KeyCode.None;
        }
        KeyBindings.SetKey(actionName, kc);
        UpdateKeyText(kc);
    }

    void UpdateKeyText(KeyCode kc)
    {
        if (keyText != null) keyText.text = kc == KeyCode.None ? "Unbound" : kc.ToString();
    }

    public void StartListening()
    {
        if (isListening) return;
        isListening = true;
        if (keyText != null) keyText.color = listeningColor;
        StartCoroutine(WaitForKey());
    }

    IEnumerator WaitForKey()
    {
        // Wait until a key is pressed
        while (!Input.anyKeyDown)
        {
            yield return null;
        }

        // capture the key pressed
        foreach (KeyCode kc in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(kc))
            {
                ApplyNewKey(kc);
                break;
            }
        }

        isListening = false;
        if (keyText != null) keyText.color = normalColor;
    }

    void ApplyNewKey(KeyCode kc)
    {
        KeyBindings.SetKey(actionName, kc);
        PlayerPrefs.SetInt(PREF_KEY_PREFIX + actionName, (int)kc);
        PlayerPrefs.Save();
        UpdateKeyText(kc);
        Debug.Log($"Keybind: {actionName} -> {kc}");
    }
}

