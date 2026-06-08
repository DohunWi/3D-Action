using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class EndingSceneController : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;
    public Image fadeImage; // 검정 Image (풀스크린)

    [Header("Settings")]
    public string mainMenuSceneName = "MainMenu";
    public float fadeDuration = 1.5f;
    public float startDelay = 1.0f;

    private void Start()
    {
        fadeImage.color = new Color(0, 0, 0, 1);
        videoPlayer.loopPointReached += OnVideoEnd;
        StartCoroutine(FadeInAndPlay());
    }

    private IEnumerator FadeInAndPlay()
    {
        yield return new WaitForSeconds(startDelay);
        yield return StartCoroutine(Fade(1f, 0f));
        videoPlayer.Play();
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        StartCoroutine(FadeOutAndLoad());
    }

    private IEnumerator FadeOutAndLoad()
    {
        yield return StartCoroutine(Fade(0f, 1f));
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            fadeImage.color = new Color(0, 0, 0, alpha);
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, to);
    }

    private void OnDestroy()
    {
        videoPlayer.loopPointReached -= OnVideoEnd;
    }
}
