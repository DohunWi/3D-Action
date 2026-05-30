using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PauseMenuController : MonoBehaviour
{
    [Header("UI")]
    public GameObject pausePanel;
    public Button resumeButton;
    public Button mainMenuButton;

    private bool _isPaused = false;

    private void Awake()
    {
        pausePanel?.SetActive(false);
        resumeButton?.onClick.AddListener(Resume);
        mainMenuButton?.onClick.AddListener(GoToMainMenu);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Toggle();
    }

    private void Toggle()
    {
        if (_isPaused) Resume();
        else Pause();
    }

    private void Pause()
    {
        _isPaused = true;
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        pausePanel?.SetActive(true);
    }

    public void Resume()
    {
        _isPaused = false;
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        pausePanel?.SetActive(false);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        _isPaused = false;
        SceneManager.LoadScene("MainMenu");
    }
}
