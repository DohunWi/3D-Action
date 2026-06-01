using UnityEngine;
using TMPro;
using System.Collections;

public class TutorialUI : MonoBehaviour
{
    public static TutorialUI Instance;

    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI contentText;

    private Coroutine _autoCloseCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        canvasGroup.alpha = 0f;
    }

    // 위치 트리거용 — 콜라이더 안에 있는 동안 유지
    public void Show(string title, string content)
    {
        if (_autoCloseCoroutine != null) StopCoroutine(_autoCloseCoroutine);
        titleText.text = title;
        contentText.text = content;
        canvasGroup.alpha = 1f;
    }

    // 이벤트 트리거용 — duration초 후 자동 닫힘
    public void Show(string title, string content, float duration)
    {
        Show(title, content);
        if (_autoCloseCoroutine != null) StopCoroutine(_autoCloseCoroutine);
        _autoCloseCoroutine = StartCoroutine(AutoClose(duration));
    }

    public void Close()
    {
        if (_autoCloseCoroutine != null) StopCoroutine(_autoCloseCoroutine);
        canvasGroup.alpha = 0f;
    }

    private IEnumerator AutoClose(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        canvasGroup.alpha = 0f;
    }
}
