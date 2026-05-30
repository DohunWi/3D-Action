using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Game Over UI")]
    public GameObject deathPanel;

    [Header("Pause UI")]
    public GameObject pausePanel;

    public static UIManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        deathPanel?.SetActive(false);
        pausePanel?.SetActive(false);
    }

    public void ShowDeathPanel()
    {
        deathPanel?.SetActive(true);
    }

    public void ShowPausePanel()
    {
        pausePanel?.SetActive(true);
    }

    public void HidePausePanel()
    {
        pausePanel?.SetActive(false);
    }
}