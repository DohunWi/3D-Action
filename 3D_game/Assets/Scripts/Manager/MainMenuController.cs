using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using System.IO;

public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    public Button newGameButton;
    public Button continueButton;
    public Button exitButton;

    [Header("Fade")]
    public Image fadeImage;
    public float fadeDuration = 1.0f;

    [Header("배경")]
    public GameObject backgroundImage; // ★ Inspector에서 배경 Image 오브젝트 연결

    [Header("BGM")]
    public AudioClip menuBGM;
    public float bgmFadeDuration = 2.0f;

    [Header("인트로 텍스트")]
    public TextMeshProUGUI introText;
    [TextArea(2, 5)]
    public string[] introLines = {
        "When the King fell asleep,\nthe sun closed its eyes with him.",
        "Since that day, the kingdom of Somnia\nhas been trapped in an eternal nightmare.",
        "Citizens wander as lost souls,\nor are devoured and twisted into monsters.",
        "Only one remained.\nOne who knew — this is a dream.",
        "Gather the fragments of memory.\nSustain your Ego.\nEnd this nightmare."
    };
    public float typewriterSpeed = 0.04f;

    private bool _inputReceived = false;

    private void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        string savePath = Path.Combine(Application.persistentDataPath, "save.json");
        if (continueButton != null)
            continueButton.interactable = File.Exists(savePath);

        if (menuBGM != null)
            SoundManager.Instance?.PlayBGM(menuBGM, bgmFadeDuration);

        if (introText != null)
            introText.gameObject.SetActive(false);

        StartCoroutine(FadeIn());
    }

    public void OnNewGame()
    {
        StartCoroutine(NewGameWithIntro());
    }

    public void OnContinue()
    {
        if (GameManager.Instance != null)
            StartCoroutine(FadeAndLoad(() => GameManager.Instance.ContinueGame()));
        else
            StartCoroutine(FadeAndLoad(() => UnityEngine.SceneManagement.SceneManager.LoadScene("Somnia")));
    }

    public void OnExit()
    {
        Application.Quit();
    }

    // --- 인트로 흐름 ---

    private IEnumerator NewGameWithIntro()
    {
        // 1. Fade Out
        yield return StartCoroutine(FadeOut());

        // 배경 이미지 숨기기 (인트로는 검은 화면에 텍스트만)
        if (backgroundImage != null) backgroundImage.SetActive(false);

        // 2. 인트로 텍스트 시퀀스
        if (introText != null && introLines.Length > 0)
        {
            SoundManager.Instance?.StopBGM(1.0f);
            yield return StartCoroutine(PlayIntroSequence());
        }

        // 3. 씬 로드
        if (GameManager.Instance != null)
            GameManager.Instance.StartNewGame();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("Somnia");
    }

    private IEnumerator PlayIntroSequence()
    {
        // Fade Panel 위에 텍스트가 보이도록 최상단으로 이동
        introText.transform.SetAsLastSibling();
        introText.gameObject.SetActive(true);

        for (int i = 0; i < introLines.Length; i++)
        {
            introText.text = "";
            introText.color = new Color(1, 1, 1, 1);

            // 타이프라이터
            foreach (char c in introLines[i])
            {
                introText.text += c;
                yield return new WaitForSeconds(typewriterSpeed);
            }

            // 마지막 줄이면 "Press any key" 힌트 표시 (흐린 색으로)
            bool isLast = (i == introLines.Length - 1);
            string hintLabel = isLast ? "[ Press any key to begin ]" : "[ Press any key ]";
            string hint = $"\n\n<color=#FFFFFF60><size=50%>{hintLabel}</size></color>";

            introText.text = introLines[i] + hint;

            // 사용자 입력 대기
            _inputReceived = false;
            yield return new WaitUntil(() => _inputReceived);

            // 페이드아웃 후 다음 줄로
            yield return StartCoroutine(FadeText(introText, 1f, 0f, 0.4f));
        }

        introText.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.3f);
    }

    private void Update()
    {
        if (introText != null && introText.gameObject.activeSelf)
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                _inputReceived = true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                _inputReceived = true;
        }
    }

    // --- 유틸 코루틴 ---

    private IEnumerator FadeText(TextMeshProUGUI tmp, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / duration);
            tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, a);
            yield return null;
        }
        tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, to);
    }

    private IEnumerator FadeIn()
    {
        if (fadeImage == null) yield break;
        fadeImage.raycastTarget = true;
        float t = 0f;
        fadeImage.color = new Color(0, 0, 0, 1);
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, 1f - t / fadeDuration);
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 0);
        fadeImage.raycastTarget = false;
    }

    private IEnumerator FadeOut()
    {
        if (fadeImage == null) yield break;
        fadeImage.raycastTarget = true;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, t / fadeDuration);
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 1);
    }

    private IEnumerator FadeAndLoad(System.Action loadAction)
    {
        yield return StartCoroutine(FadeOut());
        loadAction?.Invoke();
    }
}
