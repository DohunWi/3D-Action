using UnityEngine;
using TMPro;
using System.Collections;

public class AreaNameUI : MonoBehaviour
{
    public static AreaNameUI Instance;

    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI areaNameText;

    [Header("Timing")]
    public float fadeInDuration = 1.2f;
    public float holdDuration = 2.5f;
    public float fadeOutDuration = 1.8f;

    private Coroutine _displayCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (canvasGroup != null) canvasGroup.alpha = 1f; // CanvasGroup이 alpha=0으로 고정되지 않도록
        gameObject.SetActive(false);
    }

    public void Show(string areaName)
    {
        if (_displayCoroutine != null) StopCoroutine(_displayCoroutine);
        areaNameText.text = areaName;
        gameObject.SetActive(true);
        _displayCoroutine = StartCoroutine(DisplayRoutine());
    }

    private IEnumerator DisplayRoutine()
    {
        // Fade in
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / fadeInDuration);
            areaNameText.alpha = a;
            yield return null;
        }
        areaNameText.alpha = 1f;

        // Hold
        yield return new WaitForSecondsRealtime(holdDuration);

        // Fade out
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(1f - t / fadeOutDuration);
            areaNameText.alpha = a;
            yield return null;
        }

        gameObject.SetActive(false);
    }
}
