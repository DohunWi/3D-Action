using UnityEngine;
using UnityEngine.UI; // [필수] Image 컴포넌트 제어용

public class UIManager : MonoBehaviour
{
    [Header("Player Stats")]
    public PlayerStats playerStats; // 인스펙터에서 플레이어 드래그 앤 드롭

   [Header("HUD Bars")]
    public Image hpBarFill;
    public Image mpBarFill;
    public Image spBarFill;

    [Header("Game Over UI")] // [NEW] 여기에 패널을 가져옴
    public GameObject deathPanel;

    private void Start()
    {
        if (playerStats != null)
        {
            // 플레이어의 이벤트에 내 함수들을 연결(구독)함
            playerStats.OnHealthChanged += UpdateHP;
            playerStats.OnManaChanged   += UpdateMP;
            playerStats.OnStaminaChanged += UpdateSP;
        }
        // 2. 게임 매니저의 "게임 오버" 신호 구독
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOverEvent += ShowDeathPanel;
        }
    }

    // 이벤트 연결을 해제하는 습관 (메모리 누수 방지)
    private void OnDestroy()
    {
        if (playerStats != null)
        {
            playerStats.OnHealthChanged -= UpdateHP;
            playerStats.OnManaChanged   -= UpdateMP;
            playerStats.OnStaminaChanged -= UpdateSP;
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOverEvent -= ShowDeathPanel;
        }
    }
    // HUD Updata
    // 실제 UI를 움직이는 함수들
    private void UpdateHP(float current, float max)
    {
        // fillAmount는 0.0 ~ 1.0 사이의 값이어야 함
        hpBarFill.fillAmount = current / max;
    }

    private void UpdateMP(float current, float max)
    {
        mpBarFill.fillAmount = current / max;
    }

    private void UpdateSP(float current, float max)
    {
        spBarFill.fillAmount = current / max;
    }

    // GameOver UI
    private void ShowDeathPanel()
    {
        if (deathPanel != null)
        {
            deathPanel.SetActive(true);
            // 나중에 여기에 "YOU DIED" 글자가 서서히 나타나는 애니메이션 코드 등을 넣으면 됨
        }
    }
}