using UnityEngine;
using TMPro;
using System;
using System.Collections;
using UnityEngine.InputSystem;

public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance;

    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI dialogueText;

    [Header("Settings")]
    public float typewriterSpeed = 0.03f;

    private string[] _lines;
    private bool _inputReceived;
    private float _inputCooldown;
    private Action _onComplete;
    private PlayerController _playerController;

    public bool IsOpen => canvasGroup.alpha > 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void Update()
    {
        if (canvasGroup.alpha == 0f) return;

        // 열린 직후 입력 무시 (상호작용 키가 즉시 잡히는 현상 방지)
        // unscaledDeltaTime 사용 — timeScale=0이어도 정상 동작
        if (_inputCooldown > 0f)
        {
            _inputCooldown -= Time.unscaledDeltaTime;
            return;
        }

        // ESC는 제외 — PauseMenuController 충돌 방지
        bool anyInput = false;
        if (Keyboard.current != null)
        {
            var kb = Keyboard.current;
            anyInput = kb.anyKey.wasPressedThisFrame && !kb.escapeKey.wasPressedThisFrame;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            anyInput = true;

        if (anyInput) _inputReceived = true;
    }

    public void Open(string speakerName, string[] lines, PlayerController player, Action onComplete = null)
    {
        _playerController = player;
        _onComplete = onComplete;
        _lines = lines;

        speakerNameText.text = speakerName;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        _inputCooldown = 0.15f; // 열리는 순간 입력 쿨다운

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        StartCoroutine(PlayDialogue());
    }

    private IEnumerator PlayDialogue()
    {
        for (int i = 0; i < _lines.Length; i++)
        {
            _inputReceived = false;
            dialogueText.text = "";

            // 타이프라이터 — WaitForSecondsRealtime: timeScale=0에서도 동작
            foreach (char c in _lines[i])
            {
                if (_inputReceived)
                {
                    dialogueText.text = _lines[i];
                    _inputReceived = false;
                    break;
                }
                dialogueText.text += c;
                yield return new WaitForSecondsRealtime(typewriterSpeed);
            }
            // 힌트 표시 후 다음 입력 대기
            bool isLast = (i == _lines.Length - 1);
            string hint = isLast ? "[ Press any key to close ]" : "[ Press any key ]";
            dialogueText.text = _lines[i] + $"\n\n<color=#FFFFFF60><size=60%>{hint}</size></color>";

            _inputReceived = false;
            yield return new WaitUntil(() => _inputReceived);
        }

        Close();
    }

    private void Close()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        _playerController?.ChangeState(PlayerState.Locomotion);
        _onComplete?.Invoke();
    }
}
