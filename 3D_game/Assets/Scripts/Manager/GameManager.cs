using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System; // Action 사용을 위해

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // 대신 "게임 오버가 되었다"는 사실을 알리는 방송국(이벤트) 개설
    public event Action OnGameOverEvent; 

    private bool _isGameOver = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void GameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;

        Debug.Log("Game Over Logic Start");

        // ★ 1. 직접 UI를 켜는 대신, 이벤트를 발송함
        // "누구든 이 방송을 듣고 있는 녀석(UIManager)은 알아서 해라!"
        OnGameOverEvent?.Invoke();

        // 2. 게임 로직(재시작)은 여전히 여기서 담당
        StartCoroutine(RestartGame());
    }

    IEnumerator RestartGame()
    {
        yield return new WaitForSeconds(5.0f);
        _isGameOver = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}