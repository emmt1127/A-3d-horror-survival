using UnityEngine;
using UnityEngine.UI;

public class StaminaUI : MonoBehaviour
{
    public Slider staminaSlider;      // assign in inspector
    public Text staminaText;          // optional

    void Start()
    {
        if (staminaSlider != null) staminaSlider.value = 1f;
    }

    // value between 0 and 1
    public void SetStamina(float normalized)
    {
        if (staminaSlider != null) staminaSlider.value = Mathf.Clamp01(normalized);
        if (staminaText != null) staminaText.text = Mathf.CeilToInt(normalized * 100f) + "%";
    }
}

