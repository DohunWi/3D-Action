using UnityEngine;
using UnityEngine.UI; // [필수] Image 컴포넌트 제어용

public class UIManager : MonoBehaviour
{
    [Header("Game Over UI")] // 여기에 패널을 가져옴
    public GameObject deathPanel;

    public static UIManager Instance;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        
        // 시작할 때 확실하게 꺼두기
        if (deathPanel != null)
        {
            // deathPanel.alpha = 0;
            // deathPanel.blocksRaycasts = false;
        }
    }

    // GameOver UI
    public void ShowDeathPanel()
    {
        if (deathPanel != null)
        {
            deathPanel.SetActive(true);
            // 나중에 여기에 "YOU DIED" 글자가 서서히 나타나는 애니메이션 코드 등을 넣으면 됨
        }
    }
}