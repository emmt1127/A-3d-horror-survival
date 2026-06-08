using UnityEngine;
using UnityEngine.UI;

public class HungerUI : MonoBehaviour
{
    public Slider slider;
    public Image fillImage;
    public Color fullColor = Color.green;
    public Color emptyColor = Color.red;

    void Start()
    {
        if (slider == null) slider = GetComponent<Slider>();
    }

    public void SetHunger(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);
        if (slider != null) slider.value = normalized;
        if (fillImage != null) fillImage.color = Color.Lerp(emptyColor, fullColor, normalized);
    }
}

