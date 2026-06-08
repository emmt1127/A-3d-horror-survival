using UnityEngine;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    public Slider slider;
    public Image fillImage;
    public Color fullColor = Color.green;
    public Color emptyColor = Color.red;

    void Reset()
    {
        slider = GetComponent<Slider>();
        if (slider != null && slider.fillRect != null)
            fillImage = slider.fillRect.GetComponent<Image>();
    }

    public void SetHealth(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);
        if (slider != null) slider.value = normalized;
        if (fillImage != null) fillImage.color = Color.Lerp(emptyColor, fullColor, normalized);
    }
}

