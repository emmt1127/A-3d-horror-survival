using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DamageFlash : MonoBehaviour
{
    public Image flashImage;
    public float flashDuration = 0.35f;
    public float maxAlpha = 0.6f;
    Coroutine running;

    void Reset()
    {
        // try to auto-assign if placed on the Image GameObject
        if (flashImage == null)
            flashImage = GetComponent<Image>();
    }

    void Start()
    {
        // fallback: find first Image in children or on the Canvas
        if (flashImage == null)
            flashImage = GetComponentInChildren<Image>();
    }

    public void Flash(float intensity)
    {
        if (flashImage == null) return;
        float alpha = Mathf.Clamp01(intensity) * maxAlpha;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(DoFlash(alpha));
    }

    IEnumerator DoFlash(float alpha)
    {
        Color c = flashImage.color;
        flashImage.color = new Color(c.r, c.g, c.b, alpha);
        float t = 0f;
        while (t < flashDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(alpha, 0f, t / flashDuration);
            flashImage.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }
        flashImage.color = new Color(c.r, c.g, c.b, 0f);
        running = null;
    }
}

